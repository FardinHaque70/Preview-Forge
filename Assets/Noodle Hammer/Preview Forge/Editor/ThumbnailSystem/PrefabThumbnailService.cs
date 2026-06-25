using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Coordinates shared prefab-thumbnail queuing, cache lifetime, render scheduling, and Project window drawing hooks.

namespace NoodleHammer.PreviewForge.Editor
{
	[InitializeOnLoad]
		public static class PrefabThumbnailService
		{
			internal enum ModalGenerateRequestResult
			{
				CacheHit = 0,
				Rendered = 1,
				Failed = 2,
			}

			private struct SupportCacheEntry
			{
				public string AssetPath;
			public string DependencyToken;
			public PrefabThumbnailSupportInfo SupportInfo;
			public IPrefabThumbnailRenderer Renderer;
		}

		private struct DeferredPersistentLoadRequest
		{
			public PrefabThumbnailRequest Request;
			public string RequestDependencyToken;
			public string CacheKey;
		}

		private static readonly Dictionary<PrefabThumbnailRequest, PrefabThumbnailRecord> Cache = new();
		private static readonly LinkedList<PrefabThumbnailRequest> CacheLru = new();
		private static readonly Dictionary<PrefabThumbnailRequest, LinkedListNode<PrefabThumbnailRequest>> CacheLruNodes = new();

			private static readonly Queue<PrefabThumbnailRequest> RenderQueue = new();
			private static readonly Queue<PrefabThumbnailRequest> PriorityRenderQueue = new();
			private static readonly HashSet<PrefabThumbnailRequest> Queued = new();
			private static readonly HashSet<PrefabThumbnailRequest> PriorityQueued = new();
			private static readonly Dictionary<PrefabThumbnailRequest, string> FailedDependencyByRequest = new();
			private static readonly Dictionary<PrefabThumbnailRequest, int> RequestLastSeenFrame = new();

			private static readonly Dictionary<string, SupportCacheEntry> SupportCache = new();
			private static readonly HashSet<string> KnownNonPrefabGuids = new(StringComparer.OrdinalIgnoreCase);
			private static readonly Queue<string> PendingSupportLookupQueue = new();
			private static readonly HashSet<string> PendingSupportLookupSet = new(StringComparer.OrdinalIgnoreCase);
			private static readonly Queue<DeferredPersistentLoadRequest> PendingPersistentLoadQueue = new();
			private static readonly HashSet<PrefabThumbnailRequest> PendingPersistentLoadSet = new();
			private static readonly HashSet<string> KnownPersistentCacheMisses = new(StringComparer.Ordinal);

			private const int SupportLookupMaxPerUpdate = 24;
			private const double SupportLookupBudgetMs = 2.0;
			private const int PersistentLoadMaxPerUpdate = 4;
			private const double PersistentLoadBudgetMs = 2.0;
			private const int StaleRequestFrameAge = 90;
			private static bool ProjectWindowHookRegistered;
			private static bool EditorUpdateHookRegistered;

			static PrefabThumbnailService()
			{
				PrefabThumbnailSettings.SettingsChanged += HandleSettingsChanged;
				RefreshRuntimeHooks();
			}

		public struct CacheStats
		{
			public int TotalEntries;
			public int PersistentEntryCount;
			public int GeneratingCount;
			public int FailedCount;
			public int QueueDepth;
			public long MemoryCacheBytes;
			public long DiskCacheBytes;
		}

		private static CacheStats CachedStats;
		private static bool CacheStatsInitialized;

		public static CacheStats GetCacheStats()
		{
			EnsureCacheStatsInitialized();
			return CachedStats;
		}

		public static void InvalidatePath(string assetPath)
		{
			InvalidatePath(assetPath, repaintProjectWindow: true);
		}

		internal static void InvalidatePath(string assetPath, bool repaintProjectWindow)
		{
			if (string.IsNullOrEmpty(assetPath))
				return;

			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			List<PrefabThumbnailRequest> toRemove = new List<PrefabThumbnailRequest>();

			foreach (KeyValuePair<PrefabThumbnailRequest, PrefabThumbnailRecord> kv in Cache)
			{
				if (kv.Key.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && kv.Key.Guid == guid))
					toRemove.Add(kv.Key);
			}

			for (int i = 0; i < toRemove.Count; i++)
				RemoveCacheEntry(toRemove[i]);

			RemoveFailedEntries(assetPath, guid);
			RemoveQueuedEntries(assetPath, guid);

			if (!string.IsNullOrEmpty(guid))
			{
				SupportCache.Remove(guid);
				KnownNonPrefabGuids.Remove(guid);
				PendingSupportLookupSet.Remove(guid);
				RemoveKnownPersistentMissesForGuid(guid);
				if (PrefabThumbnailSettings.EnablePersistentCache)
					PrefabThumbnailPersistentCache.InvalidateGuid(guid);
			}

			RefreshDiskStatsSnapshot();

			if (repaintProjectWindow)
				EditorApplication.RepaintProjectWindow();
		}

		public static void ClearMemoryCache()
		{
			foreach (PrefabThumbnailRecord record in Cache.Values)
			{
				if (record?.Texture != null)
					UnityEngine.Object.DestroyImmediate(record.Texture);
			}

			Cache.Clear();
			CacheLru.Clear();
			CacheLruNodes.Clear();
			PriorityRenderQueue.Clear();
			RenderQueue.Clear();
			PriorityQueued.Clear();
			Queued.Clear();
			RequestLastSeenFrame.Clear();
			FailedDependencyByRequest.Clear();
			SupportCache.Clear();
			KnownNonPrefabGuids.Clear();
				PendingSupportLookupQueue.Clear();
				PendingSupportLookupSet.Clear();
				PendingPersistentLoadQueue.Clear();
				PendingPersistentLoadSet.Clear();
				KnownPersistentCacheMisses.Clear();
				ResetCacheStatsSnapshot();
				RefreshRuntimeHooks();
				EditorApplication.RepaintProjectWindow();
		}

		internal static void InvalidateSupportCacheForPath(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath))
				return;

			if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
				return;

			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (!string.IsNullOrEmpty(guid))
			{
				SupportCache.Remove(guid);
				KnownNonPrefabGuids.Remove(guid);
				PendingSupportLookupSet.Remove(guid);
				return;
			}

			List<string> toRemove = new List<string>();
			foreach (KeyValuePair<string, SupportCacheEntry> kv in SupportCache)
			{
				if (string.Equals(kv.Value.AssetPath, assetPath, StringComparison.OrdinalIgnoreCase))
					toRemove.Add(kv.Key);
			}

			for (int i = 0; i < toRemove.Count; i++)
				SupportCache.Remove(toRemove[i]);
		}

		public static void ClearPersistentCache()
		{
			PrefabThumbnailPersistentCache.ClearAll();
			RefreshDiskStatsSnapshot();
			EditorApplication.RepaintProjectWindow();
		}

		public static void RebuildVisibleThumbnails()
		{
			ClearMemoryCache();
			EditorApplication.RepaintProjectWindow();
		}

		public static void RegenerateThumbnail(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath))
				return;

			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (string.IsNullOrEmpty(guid))
				return;

			InvalidatePath(assetPath);
			if (!TryGetSupportedPrefabInfo(guid, out string validatedPath, out _, out PrefabThumbnailAssetKind assetKind))
				return;

			EnqueueAllSurfaces(guid, validatedPath, assetKind);
			EditorApplication.RepaintProjectWindow();
		}

			public static void GenerateAllThumbnailsInProject()
			{
				GenerateAllThumbnailsModal();
			}

			public static void GenerateAllThumbnailsInProjectFromSettings()
			{
				GenerateAllThumbnailsModal();
			}

			private static void GenerateAllThumbnailsModal()
			{
				if (IsThumbnailWorkSuspended())
				{
					Debug.LogWarning("[PrefabThumbnail] Bulk thumbnail generation is unavailable during compile, update, or play mode transitions.");
					return;
				}

				FailedDependencyByRequest.Clear();
				RefreshVolatileStatsSnapshot();

				double startTime = EditorApplication.timeSinceStartup;
				bool canceledDuringPrepare;
				bool abortedDuringPrepare;
				int supportedAssetCount;
				int completedCount = 0;
				int renderedCount = 0;
				int cacheHitCount = 0;
				int failedCount = 0;
				bool canceledDuringRender = false;
				bool abortedDuringRender = false;
				string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab") ?? Array.Empty<string>();
				List<PrefabThumbnailRequest> requests = null;

				try
				{
					requests = CollectModalGenerateRequests(
						prefabGuids,
						out supportedAssetCount,
						out canceledDuringPrepare,
						out abortedDuringPrepare);

					if (canceledDuringPrepare)
					{
						LogModalGenerateSummary(
							"cancelled during preparation",
							startTime,
							supportedAssetCount,
							totalRequestCount: requests.Count,
							completedCount,
							renderedCount,
							cacheHitCount,
							failedCount);
						return;
					}

					if (abortedDuringPrepare)
					{
						Debug.LogWarning("[PrefabThumbnail] Bulk thumbnail generation aborted during preparation because the editor entered an unsafe transition.");
						return;
					}

					if (requests.Count == 0)
					{
						if (GetEnabledGenerationSurfaces().Count == 0)
						{
							Debug.LogWarning("[PrefabThumbnail] Bulk thumbnail generation skipped because both Project window thumbnail surfaces are disabled.");
						}
						else
						{
							Debug.Log("[PrefabThumbnail] No supported prefab thumbnails were found to generate.");
						}

						return;
					}

					for (int i = 0; i < requests.Count; i++)
					{
						if (IsThumbnailWorkSuspended())
						{
							abortedDuringRender = true;
							break;
						}

						PrefabThumbnailRequest request = requests[i];
						if (EditorUtility.DisplayCancelableProgressBar(
							    "Generating Prefab Thumbnails",
							    BuildGenerateAllProgressDetail(
								    completedCount,
								    requests.Count,
								    renderedCount,
								    cacheHitCount,
								    failedCount,
								    request.AssetPath),
							    GetGenerateAllProgress01(completedCount, requests.Count)))
						{
							canceledDuringRender = true;
							break;
						}

						switch (ResolveModalGenerateRequest(request))
						{
							case ModalGenerateRequestResult.CacheHit:
								cacheHitCount++;
								break;
							case ModalGenerateRequestResult.Rendered:
								renderedCount++;
								break;
							default:
								failedCount++;
								break;
						}

						completedCount++;
					}

					if (canceledDuringRender)
					{
						LogModalGenerateSummary(
							"cancelled",
							startTime,
							supportedAssetCount,
							requests.Count,
							completedCount,
							renderedCount,
							cacheHitCount,
							failedCount);
						return;
					}

					if (abortedDuringRender)
					{
						Debug.LogWarning(
							$"[PrefabThumbnail] Bulk thumbnail generation aborted during rendering because the editor entered an unsafe transition. " +
							BuildGenerateAllSummary(requests.Count, completedCount, renderedCount, cacheHitCount, failedCount, supportedAssetCount, startTime));
						return;
					}

					LogModalGenerateSummary(
						"complete",
						startTime,
						supportedAssetCount,
						requests.Count,
						completedCount,
						renderedCount,
						cacheHitCount,
						failedCount);
				}
				finally
				{
					EditorUtility.ClearProgressBar();
					EditorApplication.RepaintProjectWindow();
					RefreshRuntimeHooks();
				}
			}

		[MenuItem("Assets/Preview Forge/Regenerate Prefab Thumbnail", true)]
		private static bool MenuRegenerateSelectedValidate()
		{
			string[] guids = Selection.assetGUIDs;
			if (guids == null || guids.Length == 0)
				return false;

			for (int i = 0; i < guids.Length; i++)
			{
				if (TryGetSupportedPrefabInfo(guids[i], out _, out _, out _))
					return true;
			}

			return false;
		}

		[MenuItem("Assets/Preview Forge/Regenerate Prefab Thumbnail", false, 2000)]
		private static void MenuRegenerateSelected()
		{
			string[] guids = Selection.assetGUIDs;
			if (guids == null || guids.Length == 0)
				return;

			for (int i = 0; i < guids.Length; i++)
			{
				if (!TryGetSupportedPrefabInfo(guids[i], out string assetPath, out _, out _))
					continue;

				RegenerateThumbnail(assetPath);
			}
		}

			private static void OnProjectWindowItemGui(string guid, Rect selectionRect)
		{
			if (Event.current != null && Event.current.type != EventType.Repaint)
				return;

			if (!PrefabThumbnailSettings.Enabled)
				return;

			if (!TryGetSupportedPrefabInfo(guid, out string assetPath, out string dependencyToken, out PrefabThumbnailAssetKind assetKind, allowSynchronousResolve: false))
				return;

			if (PrefabThumbnailProjectWindowUi.ShouldSkipObjectSelectorContext())
				return;

			PrefabThumbnailSurface surface = PrefabThumbnailProjectWindowUi.GetSurface(selectionRect);
			if (!ShouldDrawOnSurface(surface))
				return;

			Rect contentRect = PrefabThumbnailProjectWindowUi.GetContentRect(selectionRect, surface);
			if (contentRect.width <= 1f || contentRect.height <= 1f)
				return;

			PrefabThumbnailRequest request = new PrefabThumbnailRequest(guid, assetPath, assetKind, surface);
			MarkRequestSeen(request);
			if (TryGetValidRecord(request, dependencyToken, out PrefabThumbnailRecord record, allowDeferredPersistentLoad: true))
			{
				DrawRecord(contentRect, record.Texture);
				DrawBadge(contentRect, record.AssetKind, surface);
				return;
			}

			if (IsThumbnailWorkSuspended())
				return;

			DrawLoadingPlaceholder(contentRect);

			if (!IsKnownFailed(request, dependencyToken))
				Enqueue(request, prioritize: true);
		}

			private static void OnEditorUpdate()
			{
				if (!HasPendingEditorWork())
				{
					RefreshRuntimeHooks();
					return;
				}

				ProcessPendingSupportLookups();
				ProcessPendingPersistentLoads();
				if (PrefabThumbnailSettings.Enabled)
					ProcessQueue();
				RefreshRuntimeHooks();
			}

		private static void ProcessPendingSupportLookups()
		{
			if (PendingSupportLookupQueue.Count == 0)
				return;

			double start = EditorApplication.timeSinceStartup;
			int processed = 0;
			bool anyResolved = false;

			while (PendingSupportLookupQueue.Count > 0 && processed < SupportLookupMaxPerUpdate)
			{
				double elapsedMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
				if (elapsedMs > SupportLookupBudgetMs)
					break;

				string guid = PendingSupportLookupQueue.Dequeue();
				PendingSupportLookupSet.Remove(guid);
				processed++;

				if (string.IsNullOrEmpty(guid))
					continue;

				if (SupportCache.ContainsKey(guid) || KnownNonPrefabGuids.Contains(guid))
					continue;

				if (TryResolveSupportCacheEntry(guid, out _))
					anyResolved = true;
			}

			if (anyResolved)
				EditorApplication.RepaintProjectWindow();
		}

		private static void ProcessPendingPersistentLoads()
		{
			if (!PrefabThumbnailSettings.EnablePersistentCache)
			{
				PendingPersistentLoadQueue.Clear();
				PendingPersistentLoadSet.Clear();
				RefreshVolatileStatsSnapshot();
				return;
			}

			if (PendingPersistentLoadQueue.Count == 0)
				return;

			double start = EditorApplication.timeSinceStartup;
			int processed = 0;
			bool anyLoaded = false;

			while (PendingPersistentLoadQueue.Count > 0 && processed < PersistentLoadMaxPerUpdate)
			{
				double elapsedMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
				if (elapsedMs > PersistentLoadBudgetMs)
					break;

				DeferredPersistentLoadRequest deferred = PendingPersistentLoadQueue.Dequeue();
				PendingPersistentLoadSet.Remove(deferred.Request);
				processed++;

				if (KnownPersistentCacheMisses.Contains(deferred.CacheKey))
					continue;

				if (!PrefabThumbnailPersistentCache.TryLoadTexture(deferred.Request, deferred.RequestDependencyToken, out Texture2D cachedTexture))
				{
					KnownPersistentCacheMisses.Add(deferred.CacheKey);
					continue;
				}

				StoreCacheRecord(deferred.Request, deferred.RequestDependencyToken, cachedTexture, persistToDisk: false);
				anyLoaded = true;
			}

			if (anyLoaded)
				EditorApplication.RepaintProjectWindow();
		}

			private static void ProcessQueue()
			{
				if (PriorityRenderQueue.Count == 0 && RenderQueue.Count == 0)
					return;

				if (IsThumbnailWorkSuspended())
					return;

				int maxRenders = PrefabThumbnailSettings.MaxRendersPerUpdate;
				double budgetMs = PrefabThumbnailSettings.RenderBudgetMs;
				double start = EditorApplication.timeSinceStartup;
				bool anyRendered = false;
				int rendered = 0;

			while ((PriorityRenderQueue.Count > 0 || RenderQueue.Count > 0) && rendered < maxRenders)
			{
				double elapsedMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
				if (elapsedMs > budgetMs)
					break;

				if (!TryDequeueRequest(out PrefabThumbnailRequest request))
					continue;

				if (ShouldDropStaleRequest(request))
				{
					Queued.Remove(request);
					PriorityQueued.Remove(request);
					RefreshVolatileStatsSnapshot();
					continue;
					}

					Queued.Remove(request);
					PriorityQueued.Remove(request);
					if (string.IsNullOrEmpty(request.AssetPath))
						continue;

					string dependencyToken = GetDependencyToken(request.AssetPath);
					if (TryGetValidRecord(request, dependencyToken, out _, allowDeferredPersistentLoad: false))
						continue;

					Texture2D texture = null;
					try
				{
					if (TryGetRendererForRequest(request, out IPrefabThumbnailRenderer renderer))
						texture = renderer.Render(request.AssetPath, request.Surface);
				}
				catch (Exception)
				{ }

					if (texture == null)
					{
						FailedDependencyByRequest[request] = dependencyToken;
						RefreshVolatileStatsSnapshot();
						continue;
					}

					StoreCacheRecord(request, dependencyToken, texture, persistToDisk: PrefabThumbnailSettings.EnablePersistentCache);
					rendered++;
					anyRendered = true;
				}

				if (anyRendered)
					EditorApplication.RepaintProjectWindow();
			}

			private static List<PrefabThumbnailRequest> CollectModalGenerateRequests(
				string[] prefabGuids,
				out int supportedAssetCount,
				out bool canceled,
				out bool abortedByTransition)
			{
				supportedAssetCount = 0;
				canceled = false;
				abortedByTransition = false;

				List<PrefabThumbnailSurface> surfaces = GetEnabledGenerationSurfaces();
				List<PrefabThumbnailRequest> requests = new List<PrefabThumbnailRequest>();
				if (surfaces.Count == 0)
					return requests;

				int totalGuids = prefabGuids?.Length ?? 0;
				for (int i = 0; i < totalGuids; i++)
				{
					if (IsThumbnailWorkSuspended())
					{
						abortedByTransition = true;
						break;
					}

					string guid = prefabGuids[i];
					string assetPathLabel = AssetDatabase.GUIDToAssetPath(guid);
					if (EditorUtility.DisplayCancelableProgressBar(
						    "Generating Prefab Thumbnails",
						    BuildPreparationProgressDetail(i, totalGuids, supportedAssetCount, assetPathLabel),
						    totalGuids > 0 ? Mathf.Clamp01((float) i / totalGuids) : 1f))
					{
						canceled = true;
						break;
					}

					if (!TryGetSupportedPrefabInfo(guid, out string assetPath, out _, out PrefabThumbnailAssetKind assetKind))
						continue;

					supportedAssetCount++;
					for (int s = 0; s < surfaces.Count; s++)
						requests.Add(new PrefabThumbnailRequest(guid, assetPath, assetKind, surfaces[s]));
				}

				return requests;
			}

			private static List<PrefabThumbnailSurface> GetEnabledGenerationSurfaces()
			{
				return GetEnabledGenerationSurfaces(PrefabThumbnailSettings.DrawInProjectGrid, PrefabThumbnailSettings.DrawInProjectList);
			}

			private static List<PrefabThumbnailSurface> GetEnabledGenerationSurfaces(bool drawInProjectGrid, bool drawInProjectList)
			{
				List<PrefabThumbnailSurface> surfaces = new List<PrefabThumbnailSurface>(2);
				if (drawInProjectGrid)
					surfaces.Add(PrefabThumbnailSurface.ProjectWindowGrid);
				if (drawInProjectList)
					surfaces.Add(PrefabThumbnailSurface.ProjectWindowList);
				return surfaces;
			}

			private static string BuildPreparationProgressDetail(int scannedCount, int totalCount, int supportedAssetCount, string assetPath)
			{
				string currentLabel = string.IsNullOrEmpty(assetPath) ? "(starting)" : assetPath;
				return $"Scanning supported prefabs {scannedCount}/{totalCount} | Supported {supportedAssetCount} | Current: {currentLabel}";
			}

			private static string BuildGenerateAllProgressDetail(
				int completedCount,
				int totalCount,
				int renderedCount,
				int cacheHitCount,
				int failedCount,
				string assetPath)
			{
				string currentLabel = string.IsNullOrEmpty(assetPath) ? "(starting)" : assetPath;
				return $"Completed {completedCount}/{totalCount} | Rendered {renderedCount} | Cache hits {cacheHitCount} | Failed {failedCount} | Current: {currentLabel}";
			}

			private static string BuildGenerateAllSummary(
				int totalRequestCount,
				int completedCount,
				int renderedCount,
				int cacheHitCount,
				int failedCount,
				int supportedAssetCount,
				double startTime)
			{
				double elapsedSec = EditorApplication.timeSinceStartup - startTime;
				if (elapsedSec < 0.0)
					elapsedSec = 0.0;

				return $"SupportedAssets={supportedAssetCount}, Requests={totalRequestCount}, Completed={completedCount}, Rendered={renderedCount}, CacheHits={cacheHitCount}, Failed={failedCount}, Time={elapsedSec:F2}s";
			}

			private static void LogModalGenerateSummary(
				string statusLabel,
				double startTime,
				int supportedAssetCount,
				int totalRequestCount,
				int completedCount,
				int renderedCount,
				int cacheHitCount,
				int failedCount)
			{
				string summary = BuildGenerateAllSummary(
					totalRequestCount,
					completedCount,
					renderedCount,
					cacheHitCount,
					failedCount,
					supportedAssetCount,
					startTime);

				if (string.Equals(statusLabel, "complete", StringComparison.Ordinal))
				{
					Debug.Log($"[PrefabThumbnail] Bulk thumbnail generation complete. {summary}");
					return;
				}

				Debug.LogWarning($"[PrefabThumbnail] Bulk thumbnail generation {statusLabel}. {summary}");
			}

			private static float GetGenerateAllProgress01(int completedCount, int totalCount)
			{
				if (totalCount <= 0)
					return 0f;

				return Mathf.Clamp01((float) completedCount / totalCount);
			}

			private static ModalGenerateRequestResult ResolveModalGenerateRequest(PrefabThumbnailRequest request)
			{
				if (string.IsNullOrEmpty(request.AssetPath))
					return ModalGenerateRequestResult.Failed;

				string dependencyToken = GetDependencyToken(request.AssetPath);
				if (TryGetValidRecord(request, dependencyToken, out _, allowDeferredPersistentLoad: false))
					return ModalGenerateRequestResult.CacheHit;

				Texture2D texture = null;
				try
				{
					if (TryGetRendererForRequest(request, out IPrefabThumbnailRenderer renderer))
						texture = renderer.Render(request.AssetPath, request.Surface);
				}
				catch (Exception)
				{
				}

				if (texture == null)
				{
					FailedDependencyByRequest[request] = dependencyToken;
					RefreshVolatileStatsSnapshot();
					return ModalGenerateRequestResult.Failed;
				}

				StoreCacheRecord(request, dependencyToken, texture, persistToDisk: PrefabThumbnailSettings.EnablePersistentCache);
				return ModalGenerateRequestResult.Rendered;
			}

		private static void DrawRecord(Rect contentRect, Texture texture)
		{
			if (texture == null)
				return;

			EditorGUI.DrawRect(contentRect, PrefabThumbnailSettings.BackgroundColor);
			GUI.DrawTexture(contentRect, texture, ScaleMode.ScaleToFit, true);
		}

		private static void DrawBadge(Rect contentRect, PrefabThumbnailAssetKind assetKind, PrefabThumbnailSurface surface)
		{
			PrefabThumbnailBadgeType badgeType = PrefabThumbnailBadgeResolver.Resolve(assetKind);
			PrefabThumbnailBadgeDrawer.Draw(contentRect, badgeType, surface);
		}

		private static void DrawLoadingPlaceholder(Rect contentRect)
		{
			EditorGUI.DrawRect(contentRect, PrefabThumbnailSettings.BackgroundColor);
			int spinIndex = (int) (EditorApplication.timeSinceStartup * 12f) % 12;
			GUIContent spinner = EditorGUIUtility.IconContent($"WaitSpin{spinIndex:00}");
			if (spinner?.image == null)
				return;

			float iconSize = Mathf.Clamp(Mathf.Min(contentRect.width, contentRect.height) * 0.45f, 10f, 28f);
			Rect iconRect = new Rect(
				contentRect.x + (contentRect.width - iconSize) * 0.5f,
				contentRect.y + (contentRect.height - iconSize) * 0.5f,
				iconSize,
				iconSize);
			GUI.DrawTexture(iconRect, spinner.image, ScaleMode.ScaleToFit, true);
		}

		private static bool ShouldDrawOnSurface(PrefabThumbnailSurface surface)
		{
			return surface == PrefabThumbnailSurface.ProjectWindowList
				? PrefabThumbnailSettings.DrawInProjectList
				: PrefabThumbnailSettings.DrawInProjectGrid;
		}

		private static bool TryGetSupportedPrefabInfo(
			string guid,
			out string assetPath,
			out string dependencyToken,
			out PrefabThumbnailAssetKind assetKind,
			bool allowSynchronousResolve = true)
		{
			assetPath = string.Empty;
			dependencyToken = string.Empty;
			assetKind = PrefabThumbnailAssetKind.Unsupported;

			if (string.IsNullOrEmpty(guid))
				return false;

			if (KnownNonPrefabGuids.Contains(guid))
				return false;

			if (SupportCache.TryGetValue(guid, out SupportCacheEntry cached)
			    && !string.IsNullOrEmpty(cached.AssetPath))
			{
				assetPath = cached.AssetPath;
				dependencyToken = cached.DependencyToken;
				assetKind = cached.SupportInfo.AssetKind;
				return cached.SupportInfo.Supported;
			}

			if (!allowSynchronousResolve)
			{
				EnqueueSupportLookup(guid);
				return false;
			}

			if (!TryResolveSupportCacheEntry(guid, out SupportCacheEntry resolved))
				return false;

			assetPath = resolved.AssetPath;
			dependencyToken = resolved.DependencyToken;
			assetKind = resolved.SupportInfo.AssetKind;
			return resolved.SupportInfo.Supported;
		}

		private static void EnqueueSupportLookup(string guid)
		{
			if (string.IsNullOrEmpty(guid))
				return;

			if (SupportCache.ContainsKey(guid) || KnownNonPrefabGuids.Contains(guid))
				return;

			if (!PendingSupportLookupSet.Add(guid))
				return;

			PendingSupportLookupQueue.Enqueue(guid);
			RefreshRuntimeHooks();
		}

		private static bool TryResolveSupportCacheEntry(string guid, out SupportCacheEntry entry)
		{
			entry = default;
			if (string.IsNullOrEmpty(guid))
				return false;

			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(assetPath))
				return false;

			if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
			{
				KnownNonPrefabGuids.Add(guid);
				return false;
			}

			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
			IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(prefab, guid, assetPath, out PrefabThumbnailSupportInfo supportInfo);
			string dependencyToken = supportInfo.Supported ? GetDependencyToken(assetPath) : string.Empty;

			entry = new SupportCacheEntry
			{
				AssetPath = assetPath,
				DependencyToken = dependencyToken,
				SupportInfo = supportInfo,
				Renderer = renderer,
			};

			SupportCache[guid] = entry;
			return true;
		}

		private static bool TryGetRendererForRequest(PrefabThumbnailRequest request, out IPrefabThumbnailRenderer renderer)
		{
			renderer = null;
			if (string.IsNullOrEmpty(request.Guid) || string.IsNullOrEmpty(request.AssetPath))
				return false;

			if (SupportCache.TryGetValue(request.Guid, out SupportCacheEntry cached)
			    && string.Equals(cached.AssetPath, request.AssetPath, StringComparison.OrdinalIgnoreCase)
			    && cached.SupportInfo.Supported
			    && cached.SupportInfo.AssetKind == request.AssetKind
			    && cached.Renderer != null)
			{
				renderer = cached.Renderer;
				return true;
			}

			if (!TryResolveSupportCacheEntry(request.Guid, out SupportCacheEntry resolved))
				return false;

			if (!resolved.SupportInfo.Supported || resolved.SupportInfo.AssetKind != request.AssetKind)
				return false;

			renderer = resolved.Renderer;
			return renderer != null;
		}

		private static string GetDependencyToken(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath))
				return "0";

			try
			{
				return AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
			}
			catch
			{
				return "0";
			}
		}

		private static bool TryGetValidRecord(PrefabThumbnailRequest request, string dependencyToken, out PrefabThumbnailRecord record, bool allowDeferredPersistentLoad)
		{
			if (Cache.TryGetValue(request, out record))
			{
				if (record != null && record.IsValid && record.DependencyToken == dependencyToken)
				{
					TouchLru(request);
					return true;
				}

				RemoveCacheEntry(request);
			}

			if (!PrefabThumbnailSettings.EnablePersistentCache)
				return false;

			string settingsToken = PrefabThumbnailSettings.GetPersistentSettingsToken();
			string cacheKey = PrefabThumbnailPersistentCache.BuildCacheKey(request, dependencyToken, settingsToken);
			if (KnownPersistentCacheMisses.Contains(cacheKey))
				return false;

			if (allowDeferredPersistentLoad)
			{
				EnqueuePersistentLoad(request, dependencyToken, cacheKey);
				return false;
			}

			if (!PrefabThumbnailPersistentCache.TryLoadTexture(request, dependencyToken, out Texture2D cachedTexture))
			{
				KnownPersistentCacheMisses.Add(cacheKey);
				return false;
			}

			StoreCacheRecord(request, dependencyToken, cachedTexture, persistToDisk: false);
			KnownPersistentCacheMisses.Remove(cacheKey);
			record = Cache[request];
			return true;
		}

		private static bool IsKnownFailed(PrefabThumbnailRequest request, string dependencyToken)
		{
			return FailedDependencyByRequest.TryGetValue(request, out string failedDependency)
			       && failedDependency == dependencyToken;
		}

			private static void EnqueueAllSurfaces(string guid, string assetPath, PrefabThumbnailAssetKind assetKind)
			{
				if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath) || assetKind == PrefabThumbnailAssetKind.Unsupported)
					return;

				List<PrefabThumbnailSurface> surfaces = GetEnabledGenerationSurfaces();
				for (int s = 0; s < surfaces.Count; s++)
				{
					PrefabThumbnailRequest request = new PrefabThumbnailRequest(guid, assetPath, assetKind, surfaces[s]);
					if (!Queued.Contains(request))
						Enqueue(request, prioritize: false);
				}
		}

		private static void Enqueue(PrefabThumbnailRequest request, bool prioritize)
		{
			if (Queued.Contains(request))
			{
				if (prioritize && !PriorityQueued.Contains(request))
				{
					PriorityQueued.Add(request);
					PriorityRenderQueue.Enqueue(request);
					RefreshVolatileStatsSnapshot();
					RefreshRuntimeHooks();
				}

				return;
			}

			Queued.Add(request);
			if (prioritize)
			{
				PriorityQueued.Add(request);
				PriorityRenderQueue.Enqueue(request);
			}
			else
			{
				RenderQueue.Enqueue(request);
			}

			RefreshVolatileStatsSnapshot();
			RefreshRuntimeHooks();
		}

		private static void EnqueuePersistentLoad(PrefabThumbnailRequest request, string dependencyToken, string cacheKey)
		{
			if (!PendingPersistentLoadSet.Add(request))
				return;

			DeferredPersistentLoadRequest deferred = new DeferredPersistentLoadRequest
			{
				Request = request,
				RequestDependencyToken = dependencyToken,
				CacheKey = cacheKey,
			};

			PendingPersistentLoadQueue.Enqueue(deferred);
			RefreshRuntimeHooks();
		}

		private static void StoreCacheRecord(PrefabThumbnailRequest request, string dependencyToken, Texture2D texture, bool persistToDisk)
		{
			if (texture == null)
				return;

			EnsureCacheStatsInitialized();
			long previousTextureBytes = 0L;
			if (Cache.TryGetValue(request, out PrefabThumbnailRecord existing)
			    && existing?.Texture != null
			    && existing.Texture != texture)
			{
				previousTextureBytes = GetTextureFootprintBytes(existing.Texture);
				UnityEngine.Object.DestroyImmediate(existing.Texture);
			}

			PrefabThumbnailRecord record = new PrefabThumbnailRecord
			{
				Guid = request.Guid,
				AssetPath = request.AssetPath,
				DependencyToken = dependencyToken,
				AssetKind = request.AssetKind,
				Surface = request.Surface,
				Texture = texture,
			};

			Cache[request] = record;
			CachedStats.TotalEntries = Cache.Count;
			CachedStats.MemoryCacheBytes += GetTextureFootprintBytes(texture) - previousTextureBytes;
			RequestLastSeenFrame.Remove(request);
			TouchLru(request);
			EnforceCacheLimit();

			FailedDependencyByRequest.Remove(request);
			RefreshVolatileStatsSnapshot();
			string settingsToken = PrefabThumbnailSettings.GetPersistentSettingsToken();
			KnownPersistentCacheMisses.Remove(PrefabThumbnailPersistentCache.BuildCacheKey(request, dependencyToken, settingsToken));

			if (persistToDisk)
			{
				PrefabThumbnailPersistentCache.SaveTexture(request, dependencyToken, texture);
				RefreshDiskStatsSnapshot();
			}
		}

		private static void TouchLru(PrefabThumbnailRequest request)
		{
			if (CacheLruNodes.TryGetValue(request, out LinkedListNode<PrefabThumbnailRequest> existingNode))
			{
				CacheLru.Remove(existingNode);
				CacheLru.AddFirst(existingNode);
				return;
			}

			LinkedListNode<PrefabThumbnailRequest> node = CacheLru.AddFirst(request);
			CacheLruNodes[request] = node;
		}

		private static void EnforceCacheLimit()
		{
			int max = PrefabThumbnailSettings.MemoryCacheMaxEntries;
			while (CacheLru.Count > max)
			{
				LinkedListNode<PrefabThumbnailRequest> tail = CacheLru.Last;
				if (tail == null)
					break;

				RemoveCacheEntry(tail.Value);
			}
		}

		private static void RemoveCacheEntry(PrefabThumbnailRequest request)
		{
			if (Cache.TryGetValue(request, out PrefabThumbnailRecord record))
			{
				EnsureCacheStatsInitialized();
				CachedStats.TotalEntries = Math.Max(0, CachedStats.TotalEntries - 1);
				if (record?.Texture != null)
				{
					CachedStats.MemoryCacheBytes = Math.Max(0L, CachedStats.MemoryCacheBytes - GetTextureFootprintBytes(record.Texture));
					UnityEngine.Object.DestroyImmediate(record.Texture);
				}

				Cache.Remove(request);
			}

			if (CacheLruNodes.TryGetValue(request, out LinkedListNode<PrefabThumbnailRequest> node))
			{
				CacheLru.Remove(node);
				CacheLruNodes.Remove(request);
			}

			RequestLastSeenFrame.Remove(request);
			RefreshVolatileStatsSnapshot();
		}

		private static void RemoveFailedEntries(string assetPath, string guid)
		{
			List<PrefabThumbnailRequest> toRemove = new List<PrefabThumbnailRequest>();
			foreach (KeyValuePair<PrefabThumbnailRequest, string> kv in FailedDependencyByRequest)
			{
				if (kv.Key.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && kv.Key.Guid == guid))
					toRemove.Add(kv.Key);
			}

			for (int i = 0; i < toRemove.Count; i++)
				FailedDependencyByRequest.Remove(toRemove[i]);

			if (toRemove.Count > 0)
				RefreshVolatileStatsSnapshot();
		}

		private static void RemoveQueuedEntries(string assetPath, string guid)
		{
			if (PriorityRenderQueue.Count == 0 && RenderQueue.Count == 0 && PendingPersistentLoadQueue.Count == 0)
				return;

			bool anyRemoved = false;
			Queue<PrefabThumbnailRequest> retainedPriority = new Queue<PrefabThumbnailRequest>(PriorityRenderQueue.Count);
			while (PriorityRenderQueue.Count > 0)
			{
				PrefabThumbnailRequest request = PriorityRenderQueue.Dequeue();
				bool remove = request.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && request.Guid == guid);
				if (remove)
				{
					Queued.Remove(request);
					PriorityQueued.Remove(request);
					RequestLastSeenFrame.Remove(request);
					anyRemoved = true;
					continue;
				}

				retainedPriority.Enqueue(request);
			}

			while (retainedPriority.Count > 0)
				PriorityRenderQueue.Enqueue(retainedPriority.Dequeue());

			Queue<PrefabThumbnailRequest> retained = new Queue<PrefabThumbnailRequest>(RenderQueue.Count);
			while (RenderQueue.Count > 0)
			{
				PrefabThumbnailRequest request = RenderQueue.Dequeue();
				bool remove = request.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && request.Guid == guid);
				if (remove)
				{
					Queued.Remove(request);
					RequestLastSeenFrame.Remove(request);
					anyRemoved = true;
					continue;
				}

				retained.Enqueue(request);
			}

			while (retained.Count > 0)
				RenderQueue.Enqueue(retained.Dequeue());

			if (PendingPersistentLoadQueue.Count == 0)
			{
				if (anyRemoved)
					RefreshVolatileStatsSnapshot();
				return;
			}

			Queue<DeferredPersistentLoadRequest> retainedLoads = new Queue<DeferredPersistentLoadRequest>(PendingPersistentLoadQueue.Count);
			while (PendingPersistentLoadQueue.Count > 0)
			{
				DeferredPersistentLoadRequest deferred = PendingPersistentLoadQueue.Dequeue();
				PrefabThumbnailRequest request = deferred.Request;
				bool remove = request.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && request.Guid == guid);
				if (remove)
				{
					PendingPersistentLoadSet.Remove(request);
					RequestLastSeenFrame.Remove(request);
					anyRemoved = true;
					continue;
				}

				retainedLoads.Enqueue(deferred);
			}

			while (retainedLoads.Count > 0)
				PendingPersistentLoadQueue.Enqueue(retainedLoads.Dequeue());

			if (anyRemoved)
				RefreshVolatileStatsSnapshot();
		}

		private static void HandleSettingsChanged()
		{
			ClearMemoryCache();
			RefreshRuntimeHooks();
		}

		private static void MarkRequestSeen(PrefabThumbnailRequest request)
		{
			RequestLastSeenFrame[request] = Time.frameCount;
		}

		private static bool TryDequeueRequest(out PrefabThumbnailRequest request)
		{
			while (PriorityRenderQueue.Count > 0)
			{
				request = PriorityRenderQueue.Dequeue();
				if (!Queued.Contains(request))
					continue;

				RefreshVolatileStatsSnapshot();
				return true;
			}

			while (RenderQueue.Count > 0)
			{
				request = RenderQueue.Dequeue();
				if (!Queued.Contains(request))
					continue;

				RefreshVolatileStatsSnapshot();
				return true;
			}

			request = default;
			return false;
		}

		internal static void ResetCacheStatsStateForTests()
		{
			CacheStatsInitialized = false;
			CachedStats = default;
		}

		internal static void StoreCacheRecordForTests(PrefabThumbnailRequest request, string dependencyToken, Texture2D texture)
		{
			StoreCacheRecord(request, dependencyToken, texture, persistToDisk: false);
		}

		internal static void RemoveCacheEntryForTests(PrefabThumbnailRequest request)
		{
			RemoveCacheEntry(request);
		}

		internal static void EnqueueForTests(PrefabThumbnailRequest request, bool prioritize)
		{
			Enqueue(request, prioritize);
		}

		internal static PrefabThumbnailSurface[] GetEnabledGenerationSurfacesForTests(bool drawInProjectGrid, bool drawInProjectList)
		{
			return GetEnabledGenerationSurfaces(drawInProjectGrid, drawInProjectList).ToArray();
		}

		internal static float GetGenerateAllProgressForTests(int completedCount, int totalCount)
		{
			return GetGenerateAllProgress01(completedCount, totalCount);
		}

		internal static string BuildGenerateAllProgressDetailForTests(
			int completedCount,
			int totalCount,
			int renderedCount,
			int cacheHitCount,
			int failedCount,
			string assetPath)
		{
			return BuildGenerateAllProgressDetail(
				completedCount,
				totalCount,
				renderedCount,
				cacheHitCount,
				failedCount,
				assetPath);
		}

		internal static ModalGenerateRequestResult ResolveModalGenerateRequestForTests(PrefabThumbnailRequest request)
		{
			return ResolveModalGenerateRequest(request);
		}

		internal static int GetPendingPersistentLoadCountForTests()
		{
			return PendingPersistentLoadQueue.Count;
		}

		internal static void MarkFailedRequestForTests(PrefabThumbnailRequest request, string dependencyToken)
		{
			FailedDependencyByRequest[request] = dependencyToken ?? string.Empty;
			RefreshVolatileStatsSnapshot();
		}

		private static void EnsureCacheStatsInitialized()
		{
			if (CacheStatsInitialized)
				return;

			PrefabThumbnailPersistentCache.GetCachedDiskStats(out int persistentCount, out long diskBytes);

			CachedStats = new CacheStats
			{
				TotalEntries = Cache.Count,
				PersistentEntryCount = persistentCount,
				GeneratingCount = Queued.Count + PendingPersistentLoadQueue.Count,
				FailedCount = FailedDependencyByRequest.Count,
				QueueDepth = PriorityRenderQueue.Count + RenderQueue.Count,
				DiskCacheBytes = diskBytes,
			};

			foreach (PrefabThumbnailRecord record in Cache.Values)
			{
				if (record?.Texture != null)
					CachedStats.MemoryCacheBytes += GetTextureFootprintBytes(record.Texture);
			}

			CacheStatsInitialized = true;
		}

		private static void RefreshVolatileStatsSnapshot()
		{
			EnsureCacheStatsInitialized();
			CachedStats.TotalEntries = Cache.Count;
			CachedStats.GeneratingCount = Queued.Count + PendingPersistentLoadQueue.Count;
			CachedStats.FailedCount = FailedDependencyByRequest.Count;
			CachedStats.QueueDepth = PriorityRenderQueue.Count + RenderQueue.Count;
		}

		private static void RefreshDiskStatsSnapshot()
		{
			EnsureCacheStatsInitialized();
			PrefabThumbnailPersistentCache.GetCachedDiskStats(out int persistentCount, out long diskBytes);
			CachedStats.PersistentEntryCount = persistentCount;
			CachedStats.DiskCacheBytes = diskBytes;
		}

		private static void ResetCacheStatsSnapshot()
		{
			CacheStatsInitialized = true;
			CachedStats = default;
			RefreshDiskStatsSnapshot();
			RefreshVolatileStatsSnapshot();
		}

		private static long GetTextureFootprintBytes(Texture2D texture)
		{
			return texture == null ? 0L : (long)texture.width * texture.height * 4L;
		}

		private static bool ShouldDropStaleRequest(PrefabThumbnailRequest request)
		{
			if (!RequestLastSeenFrame.TryGetValue(request, out int lastSeenFrame))
				return false;

			int frameAge = Time.frameCount - lastSeenFrame;
			if (frameAge <= StaleRequestFrameAge)
				return false;

			RequestLastSeenFrame.Remove(request);
			return true;
		}

		private static void RemoveKnownPersistentMissesForGuid(string guid)
		{
			if (string.IsNullOrEmpty(guid) || KnownPersistentCacheMisses.Count == 0)
				return;

			string prefix = guid + "_";
			List<string> removeKeys = null;
			foreach (string cacheKey in KnownPersistentCacheMisses)
			{
				if (!cacheKey.StartsWith(prefix, StringComparison.Ordinal))
					continue;

				removeKeys ??= new List<string>();
				removeKeys.Add(cacheKey);
			}

			if (removeKeys == null)
				return;

			for (int i = 0; i < removeKeys.Count; i++)
				KnownPersistentCacheMisses.Remove(removeKeys[i]);
		}

		private static bool ShouldProjectWindowHookBeActive()
		{
			return PrefabThumbnailSettings.Enabled
			       && (PrefabThumbnailSettings.DrawInProjectGrid || PrefabThumbnailSettings.DrawInProjectList);
		}

		private static bool HasPendingEditorWork()
		{
			if (PendingSupportLookupQueue.Count > 0)
				return true;

			if (PendingPersistentLoadQueue.Count > 0)
				return true;

			return PriorityRenderQueue.Count > 0 || RenderQueue.Count > 0;
		}

		private static bool IsThumbnailWorkSuspended()
		{
			return PreviewEditorTransitionGuard.IsUnsafeTransition();
		}

		private static void RefreshRuntimeHooks()
		{
			bool shouldProjectWindowHookBeActive = ShouldProjectWindowHookBeActive();
			if (shouldProjectWindowHookBeActive != ProjectWindowHookRegistered)
			{
				if (shouldProjectWindowHookBeActive)
					EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGui;
				else
					EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGui;

				ProjectWindowHookRegistered = shouldProjectWindowHookBeActive;
			}

			bool shouldEditorUpdateHookBeActive = HasPendingEditorWork();
			if (shouldEditorUpdateHookBeActive == EditorUpdateHookRegistered)
				return;

			if (shouldEditorUpdateHookBeActive)
				EditorApplication.update += OnEditorUpdate;
			else
				EditorApplication.update -= OnEditorUpdate;

			EditorUpdateHookRegistered = shouldEditorUpdateHookBeActive;
		}
	}
}
