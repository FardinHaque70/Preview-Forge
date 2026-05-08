using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Coordinates thumbnail request queuing, cache lifetime, render scheduling, and Project window drawing hooks for particle prefabs.

namespace ParticleThumbnailAndPreview.Editor
{
	[InitializeOnLoad]
	public static class ParticleThumbnailService
	{
		private struct SupportCacheEntry
		{
			public string AssetPath;
			public string DependencyToken;
			public bool IsParticlePrefab;
		}

		private struct DeferredPersistentLoadRequest
		{
			public ParticleThumbnailRequest Request;
			public string RequestDependencyToken;
			public string CacheKey;
		}

		private static readonly Dictionary<ParticleThumbnailRequest, ParticleThumbnailRecord> Cache = new();
		private static readonly LinkedList<ParticleThumbnailRequest> CacheLru = new();
		private static readonly Dictionary<ParticleThumbnailRequest, LinkedListNode<ParticleThumbnailRequest>> CacheLruNodes = new();

		private static readonly Queue<ParticleThumbnailRequest> RenderQueue = new();
		private static readonly Queue<ParticleThumbnailRequest> PriorityRenderQueue = new();
		private static readonly HashSet<ParticleThumbnailRequest> Queued = new();
		private static readonly HashSet<ParticleThumbnailRequest> PriorityQueued = new();
		private static readonly Dictionary<ParticleThumbnailRequest, string> FailedDependencyByRequest = new();
		private static readonly Dictionary<ParticleThumbnailRequest, int> RequestLastSeenFrame = new();
		private static readonly HashSet<string> SelectedAssetGuids = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<ParticleThumbnailRequest> GenerateAllPendingRequests = new();

		private static readonly Dictionary<string, SupportCacheEntry> SupportCache = new();
		private static readonly HashSet<string> KnownNonPrefabGuids = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Queue<string> PendingSupportLookupQueue = new();
		private static readonly HashSet<string> PendingSupportLookupSet = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Queue<DeferredPersistentLoadRequest> PendingPersistentLoadQueue = new();
		private static readonly HashSet<ParticleThumbnailRequest> PendingPersistentLoadSet = new();
		private static readonly HashSet<string> KnownPersistentCacheMisses = new(StringComparer.Ordinal);
		private static string SingleSelectedAssetGuid = string.Empty;
		private static int GenerateAllTotalCount;
		private static int GenerateAllCompletedCount;
		private static int GenerateAllSucceededCount;
		private static int GenerateAllFailedCount;
		private static bool GenerateAllIsPreparing;
		private static int GenerateAllPreparingTotal;
		private static int GenerateAllPreparingIndex;
		private static string GenerateAllCurrentAssetPath = string.Empty;
		private static bool GenerateAllProgressWindowDismissedByUser;
		private static string[] GenerateAllScanGuids = Array.Empty<string>();
		private static int GenerateAllScanIndex;
		private static bool GenerateAllScanInProgress;
		private static bool GenerateAllUseUnthrottledProcessing;
		private static bool GenerateAllRunActive;
		private static double GenerateAllStartTime;
		private static bool GenerateAllWarmupFramePending;

		private const float SelectionOutlineThickness = 2f;
		private const float SelectionOutlineInset = 2f;
		private const double GenerateAllScanBudgetMs = 6.0;
		private const int SupportLookupMaxPerUpdate = 24;
		private const double SupportLookupBudgetMs = 2.0;
		private const int PersistentLoadMaxPerUpdate = 4;
		private const double PersistentLoadBudgetMs = 2.0;
		private const int StaleRequestFrameAge = 90;
		private const int FastModeMaxRendersPerUpdate = 64;
		private const double FastModeRenderBudgetMs = 200.0;
		private const double FastModeScanBudgetMs = 40.0;
		private static readonly Color SelectionOutlineColor = new Color(0.11f, 0.84f, 0.39f, 1f);
		private static bool ProjectWindowHookRegistered;
		private static bool EditorUpdateHookRegistered;
		private static bool SelectionChangedHookRegistered;

		static ParticleThumbnailService()
		{
			EditorApplication.quitting += SafeClearProgressWindow;
			AssemblyReloadEvents.beforeAssemblyReload += SafeClearProgressWindow;
			ParticleThumbnailSettings.SettingsChanged += HandleSettingsChanged;
			RefreshRuntimeHooks(refreshSelectionCacheWhenEnabled: true);
		}

		public static bool IsGenerateAllInProgress
		{
			get
			{
				if (GenerateAllIsPreparing || GenerateAllScanInProgress)
					return true;

				if (GenerateAllPendingRequests.Count > 0)
					return true;

				if (GenerateAllTotalCount <= 0)
					return false;

				return GenerateAllCompletedCount < GenerateAllTotalCount;
			}
		}

		public static bool TryGetGenerateAllProgress(
			out float progress01,
			out int completed,
			out int total,
			out int succeeded,
			out int failed)
		{
			total = GenerateAllTotalCount;
			completed = GenerateAllCompletedCount;
			succeeded = GenerateAllSucceededCount;
			failed = GenerateAllFailedCount;

			if (total <= 0)
			{
				progress01 = 0f;
				return false;
			}

			progress01 = Mathf.Clamp01((float) completed / total);
			return true;
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
			List<ParticleThumbnailRequest> toRemove = new List<ParticleThumbnailRequest>();

			foreach (KeyValuePair<ParticleThumbnailRequest, ParticleThumbnailRecord> kv in Cache)
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
				if (ParticleThumbnailSettings.EnablePersistentCache)
					ParticleThumbnailPersistentCache.InvalidateGuid(guid);
			}

			RefreshDiskStatsSnapshot();

			if (repaintProjectWindow)
				EditorApplication.RepaintProjectWindow();
		}

		public static void ClearMemoryCache()
		{
			foreach (ParticleThumbnailRecord record in Cache.Values)
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
			ResetGenerateAllProgress();
			SafeClearProgressWindow();
			ResetCacheStatsSnapshot();
			RefreshRuntimeHooks(refreshSelectionCacheWhenEnabled: true);
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
			ParticleThumbnailPersistentCache.ClearAll();
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
			if (!TryGetParticlePrefabInfo(guid, out string validatedPath, out _))
				return;

			EnqueueAllSurfaces(guid, validatedPath);
			EditorApplication.RepaintProjectWindow();
		}

		public static void GenerateAllThumbnailsInProject()
		{
			GenerateAllThumbnailsInProject(unthrottledProcessing: false);
		}

		public static void GenerateAllThumbnailsInProjectFromSettings()
		{
			GenerateAllThumbnailsInProject(unthrottledProcessing: true);
		}

		private static void GenerateAllThumbnailsInProject(bool unthrottledProcessing)
		{
			FailedDependencyByRequest.Clear();
			RefreshVolatileStatsSnapshot();
			ResetGenerateAllProgress();
			GenerateAllUseUnthrottledProcessing = unthrottledProcessing;
			GenerateAllRunActive = true;
			GenerateAllStartTime = EditorApplication.timeSinceStartup;
			ShowImmediatePreparingPopup();

			GenerateAllScanGuids = AssetDatabase.FindAssets("t:Prefab") ?? Array.Empty<string>();
			GenerateAllScanIndex = 0;
			GenerateAllScanInProgress = true;
			GenerateAllPreparingTotal = GenerateAllScanGuids.Length;
			GenerateAllPreparingIndex = 0;
			GenerateAllCurrentAssetPath = string.Empty;
			GenerateAllWarmupFramePending = GenerateAllUseUnthrottledProcessing;
			UpdateGenerateAllProgressWindow();
			RepaintAllRelevantWindows();
			RefreshRuntimeHooks();

			if (GenerateAllScanGuids.Length == 0)
				FinalizeGenerateAllPreparation();
		}

		[MenuItem("Assets/Particle Thumbnail/Regenerate Thumbnail", true)]
		private static bool MenuRegenerateSelectedValidate()
		{
			string[] guids = Selection.assetGUIDs;
			if (guids == null || guids.Length == 0)
				return false;

			for (int i = 0; i < guids.Length; i++)
			{
				if (TryGetParticlePrefabInfo(guids[i], out _, out _))
					return true;
			}

			return false;
		}

		[MenuItem("Assets/Particle Thumbnail/Regenerate Thumbnail", false, 2000)]
		private static void MenuRegenerateSelected()
		{
			string[] guids = Selection.assetGUIDs;
			if (guids == null || guids.Length == 0)
				return;

			for (int i = 0; i < guids.Length; i++)
			{
				if (!TryGetParticlePrefabInfo(guids[i], out string assetPath, out _))
					continue;

				RegenerateThumbnail(assetPath);
			}
		}

		private static readonly ParticleThumbnailSurface[] GenerationSurfaces =
		{
			ParticleThumbnailSurface.ProjectWindowGrid,
			ParticleThumbnailSurface.ProjectWindowList,
		};

		private static void OnProjectWindowItemGui(string guid, Rect selectionRect)
		{
			if (Event.current != null && Event.current.type != EventType.Repaint)
				return;

			if (!ParticleThumbnailSettings.Enabled)
				return;

			if (!TryGetParticlePrefabInfo(guid, out string assetPath, out string dependencyToken, allowSynchronousResolve: false))
				return;

			if (ParticleThumbnailProjectWindowUi.ShouldSkipObjectSelectorContext())
				return;

			ParticleThumbnailSurface surface = ParticleThumbnailProjectWindowUi.GetSurface(selectionRect);
			if (!ShouldDrawOnSurface(surface))
				return;

			Rect contentRect = ParticleThumbnailProjectWindowUi.GetContentRect(selectionRect, surface);
			if (contentRect.width <= 1f || contentRect.height <= 1f)
				return;

			bool shouldDrawSelectionOutline = ShouldDrawSelectionOutline(surface, guid);
			Rect outlineRect = shouldDrawSelectionOutline
				? ParticleThumbnailProjectWindowUi.GetOutlineRect(selectionRect, surface)
				: default;
			ParticleThumbnailRequest request = new ParticleThumbnailRequest(guid, assetPath, surface);
			MarkRequestSeen(request);
			if (TryGetValidRecord(request, dependencyToken, out ParticleThumbnailRecord record, allowDeferredPersistentLoad: true))
			{
				DrawRecord(contentRect, record.Texture);
				if (shouldDrawSelectionOutline)
					DrawSelectionOutline(outlineRect);
				return;
			}

			DrawLoadingPlaceholder(contentRect);
			if (shouldDrawSelectionOutline)
				DrawSelectionOutline(outlineRect);

			if (!IsKnownFailed(request, dependencyToken))
				Enqueue(request, prioritize: true);
		}

		private static void OnEditorUpdate()
		{
			if (!HasPendingEditorWork())
			{
				UpdateGenerateAllProgressWindow();
				RefreshRuntimeHooks();
				return;
			}

			if (GenerateAllWarmupFramePending)
			{
				GenerateAllWarmupFramePending = false;
				RepaintAllRelevantWindows();
				RefreshRuntimeHooks();
				return;
			}

			if (GenerateAllScanInProgress)
				ProcessGenerateAllScan();

			ProcessPendingSupportLookups();
			ProcessPendingPersistentLoads();
			ResolveDanglingGenerateAllRequests();

			if (ParticleThumbnailSettings.Enabled || GenerateAllPendingRequests.Count > 0)
				ProcessQueue();

			TryLogGenerateAllCompletion();
			UpdateGenerateAllProgressWindow();
			RefreshRuntimeHooks();
		}

		private static void ResolveDanglingGenerateAllRequests()
		{
			if (GenerateAllScanInProgress || GenerateAllIsPreparing)
				return;

			if (GenerateAllPendingRequests.Count == 0 || PriorityRenderQueue.Count > 0 || RenderQueue.Count > 0 || PendingPersistentLoadQueue.Count > 0)
				return;

			List<ParticleThumbnailRequest> dangling = new List<ParticleThumbnailRequest>(GenerateAllPendingRequests.Count);
			foreach (ParticleThumbnailRequest request in GenerateAllPendingRequests)
				dangling.Add(request);

			bool anyResolved = false;
			for (int i = 0; i < dangling.Count; i++)
				anyResolved |= CompleteGenerateAllRequest(dangling[i], success: false);

			if (!anyResolved)
				return;

			GenerateAllCurrentAssetPath = string.Empty;
			RepaintAllRelevantWindows();
		}

		private static void TryLogGenerateAllCompletion()
		{
			if (!GenerateAllRunActive)
				return;

			if (GenerateAllIsPreparing || GenerateAllScanInProgress)
				return;

			if (GenerateAllTotalCount <= 0)
				return;

			if (GenerateAllCompletedCount < GenerateAllTotalCount)
				return;

			LogGenerateAllCompletion(GenerateAllTotalCount, GenerateAllSucceededCount, GenerateAllFailedCount);
		}

		private static void LogGenerateAllCompletion(int total, int succeeded, int failed)
		{
			double elapsedSec = EditorApplication.timeSinceStartup - GenerateAllStartTime;
			if (elapsedSec < 0.0)
				elapsedSec = 0.0;

			string modeLabel = GenerateAllUseUnthrottledProcessing ? "fast mode" : "throttled mode";
			Debug.Log(
				$"[ParticleThumbnail] Generate-all complete ({modeLabel}). Total={total}, Succeeded={succeeded}, Failed={failed}, Time={elapsedSec:F2}s");

			GenerateAllRunActive = false;
			GenerateAllUseUnthrottledProcessing = false;
		}

		private static void ProcessGenerateAllScan()
		{
			if (!GenerateAllScanInProgress)
				return;

			double start = EditorApplication.timeSinceStartup;
			int total = GenerateAllScanGuids?.Length ?? 0;

			while (GenerateAllScanIndex < total)
			{
				double elapsedMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
				double scanBudgetMs = GenerateAllUseUnthrottledProcessing ? FastModeScanBudgetMs : GenerateAllScanBudgetMs;
				if (elapsedMs > scanBudgetMs)
					break;

				string guid = GenerateAllScanGuids[GenerateAllScanIndex];
				string previewAssetPath = AssetDatabase.GUIDToAssetPath(guid);
				GenerateAllPreparingIndex = GenerateAllScanIndex + 1;
				GenerateAllCurrentAssetPath = previewAssetPath ?? string.Empty;

				if (TryGetParticlePrefabInfo(guid, out string assetPath, out string dependencyToken))
				{
					for (int s = 0; s < GenerationSurfaces.Length; s++)
					{
						ParticleThumbnailRequest request = new ParticleThumbnailRequest(guid, assetPath, GenerationSurfaces[s]);
						if (TryGetValidRecord(request, dependencyToken, out _, allowDeferredPersistentLoad: false))
							continue;

						TrackGenerateAllRequest(request);
						if (!Queued.Contains(request))
							Enqueue(request, prioritize: false);
					}
				}

				GenerateAllScanIndex++;
			}

			if (GenerateAllScanIndex >= total)
				FinalizeGenerateAllPreparation();
		}

		private static void FinalizeGenerateAllPreparation()
		{
			GenerateAllScanInProgress = false;
			GenerateAllScanGuids = Array.Empty<string>();
			GenerateAllScanIndex = 0;
			GenerateAllIsPreparing = false;
			GenerateAllPreparingIndex = 0;
			GenerateAllPreparingTotal = 0;

			if (GenerateAllTotalCount > 0)
			{
				GenerateAllCurrentAssetPath = "Queued. Starting generation...";
				RepaintAllRelevantWindows();
			}
			else
			{
				LogGenerateAllCompletion(total: 0, succeeded: 0, failed: 0);
				ResetGenerateAllProgress();
				GenerateAllCurrentAssetPath = string.Empty;
				EditorApplication.RepaintProjectWindow();
			}
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
			if (!ParticleThumbnailSettings.EnablePersistentCache)
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

				if (!ParticleThumbnailPersistentCache.TryLoadTexture(deferred.Request, deferred.RequestDependencyToken, out Texture2D cachedTexture))
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

			int maxRenders = GenerateAllUseUnthrottledProcessing ? FastModeMaxRendersPerUpdate : ParticleThumbnailSettings.MaxRendersPerUpdate;
			double budgetMs = GenerateAllUseUnthrottledProcessing ? FastModeRenderBudgetMs : ParticleThumbnailSettings.RenderBudgetMs;
			double start = EditorApplication.timeSinceStartup;
			bool anyRendered = false;
			bool anyProgressUpdated = false;
			int rendered = 0;

			while ((PriorityRenderQueue.Count > 0 || RenderQueue.Count > 0) && rendered < maxRenders)
			{
				double elapsedMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
				if (elapsedMs > budgetMs)
					break;

				if (!TryDequeueRequest(out ParticleThumbnailRequest request))
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
				bool trackedByGenerateAll = GenerateAllPendingRequests.Contains(request);
				if (trackedByGenerateAll)
				{
					GenerateAllCurrentAssetPath = request.AssetPath ?? string.Empty;
					UpdateGenerateAllProgressWindow();
				}

				if (string.IsNullOrEmpty(request.AssetPath))
				{
					if (trackedByGenerateAll)
						anyProgressUpdated |= CompleteGenerateAllRequest(request, success: false);
					continue;
				}

				string dependencyToken = GetDependencyToken(request.AssetPath);
				if (TryGetValidRecord(request, dependencyToken, out _, allowDeferredPersistentLoad: false))
				{
					if (trackedByGenerateAll)
						anyProgressUpdated |= CompleteGenerateAllRequest(request, success: true);
					continue;
				}

				Texture2D texture = null;
				try
				{
					texture = ParticleThumbnailRenderer.Render(request.AssetPath, request.Surface);
				}
				catch (Exception)
				{ }

				if (texture == null)
				{
					FailedDependencyByRequest[request] = dependencyToken;
					RefreshVolatileStatsSnapshot();
					if (trackedByGenerateAll)
						anyProgressUpdated |= CompleteGenerateAllRequest(request, success: false);
					continue;
				}

				StoreCacheRecord(request, dependencyToken, texture, persistToDisk: ParticleThumbnailSettings.EnablePersistentCache);
				rendered++;
				anyRendered = true;
				if (trackedByGenerateAll)
					anyProgressUpdated |= CompleteGenerateAllRequest(request, success: true);
			}

			if (anyRendered)
				EditorApplication.RepaintProjectWindow();

			if (anyProgressUpdated)
				RepaintAllRelevantWindows();
		}

		private static void DrawRecord(Rect contentRect, Texture texture)
		{
			if (texture == null)
				return;

			EditorGUI.DrawRect(contentRect, ParticleThumbnailSettings.BackgroundColor);
			GUI.DrawTexture(contentRect, texture, ScaleMode.ScaleToFit, true);
		}

		private static void DrawLoadingPlaceholder(Rect contentRect)
		{
			EditorGUI.DrawRect(contentRect, ParticleThumbnailSettings.BackgroundColor);
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

		private static bool ShouldDrawOnSurface(ParticleThumbnailSurface surface)
		{
			return surface == ParticleThumbnailSurface.ProjectWindowList
				? ParticleThumbnailSettings.DrawInProjectList
				: ParticleThumbnailSettings.DrawInProjectGrid;
		}

		private static bool TryGetParticlePrefabInfo(string guid, out string assetPath, out string dependencyToken, bool allowSynchronousResolve = true)
		{
			assetPath = string.Empty;
			dependencyToken = string.Empty;

			if (string.IsNullOrEmpty(guid))
				return false;

			if (KnownNonPrefabGuids.Contains(guid))
				return false;

			if (SupportCache.TryGetValue(guid, out SupportCacheEntry cached)
			    && !string.IsNullOrEmpty(cached.AssetPath))
			{
				assetPath = cached.AssetPath;
				dependencyToken = cached.DependencyToken;
				return cached.IsParticlePrefab;
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
			return resolved.IsParticlePrefab;
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
			bool isParticlePrefab = ParticleThumbnailDetection.IsParticlePrefab(prefab);
			string dependencyToken = isParticlePrefab ? GetDependencyToken(assetPath) : string.Empty;

			entry = new SupportCacheEntry
			{
				AssetPath = assetPath,
				DependencyToken = dependencyToken,
				IsParticlePrefab = isParticlePrefab,
			};

			SupportCache[guid] = entry;
			return true;
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

		private static bool TryGetValidRecord(ParticleThumbnailRequest request, string dependencyToken, out ParticleThumbnailRecord record, bool allowDeferredPersistentLoad)
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

			if (!ParticleThumbnailSettings.EnablePersistentCache)
				return false;

			string settingsToken = ParticleThumbnailSettings.GetPersistentSettingsToken();
			string cacheKey = ParticleThumbnailPersistentCache.BuildCacheKey(request, dependencyToken, settingsToken);
			if (KnownPersistentCacheMisses.Contains(cacheKey))
				return false;

			if (allowDeferredPersistentLoad)
			{
				EnqueuePersistentLoad(request, dependencyToken, cacheKey);
				return false;
			}

			if (!ParticleThumbnailPersistentCache.TryLoadTexture(request, dependencyToken, out Texture2D cachedTexture))
			{
				KnownPersistentCacheMisses.Add(cacheKey);
				return false;
			}

			StoreCacheRecord(request, dependencyToken, cachedTexture, persistToDisk: false);
			KnownPersistentCacheMisses.Remove(cacheKey);
			record = Cache[request];
			return true;
		}

		private static bool IsKnownFailed(ParticleThumbnailRequest request, string dependencyToken)
		{
			return FailedDependencyByRequest.TryGetValue(request, out string failedDependency)
			       && failedDependency == dependencyToken;
		}

		private static bool ShouldDrawSelectionOutline(ParticleThumbnailSurface surface, string guid)
		{
			if (surface != ParticleThumbnailSurface.ProjectWindowGrid)
				return false;

			return IsGuidSelectedInProjectWindow(guid);
		}

		private static bool IsGuidSelectedInProjectWindow(string guid)
		{
			if (string.IsNullOrEmpty(guid))
				return false;

			if (!string.IsNullOrEmpty(SingleSelectedAssetGuid))
				return string.Equals(SingleSelectedAssetGuid, guid, StringComparison.OrdinalIgnoreCase);

			return SelectedAssetGuids.Contains(guid);
		}

		private static void DrawSelectionOutline(Rect contentRect)
		{
			if (contentRect.width <= 2f || contentRect.height <= 2f)
				return;

			float maxInset = Mathf.Max(0f, Mathf.Min(contentRect.width, contentRect.height) * 0.25f);
			float inset = Mathf.Clamp(SelectionOutlineInset, 0f, maxInset);
			Rect insetRect = new Rect(
				contentRect.x + inset,
				contentRect.y + inset,
				Mathf.Max(0f, contentRect.width - inset * 2f),
				Mathf.Max(0f, contentRect.height - inset * 2f));

			if (insetRect.width <= 2f || insetRect.height <= 2f)
				return;

			float thickness = Mathf.Clamp(Mathf.Round(SelectionOutlineThickness), 1f, Mathf.Min(insetRect.width, insetRect.height) * 0.5f);
			EditorGUI.DrawRect(new Rect(insetRect.x, insetRect.y, insetRect.width, thickness), SelectionOutlineColor);
			EditorGUI.DrawRect(new Rect(insetRect.x, insetRect.yMax - thickness, insetRect.width, thickness), SelectionOutlineColor);
			EditorGUI.DrawRect(new Rect(insetRect.x, insetRect.y, thickness, insetRect.height), SelectionOutlineColor);
			EditorGUI.DrawRect(new Rect(insetRect.xMax - thickness, insetRect.y, thickness, insetRect.height), SelectionOutlineColor);
		}

		private static void EnqueueAllSurfaces(string guid, string assetPath)
		{
			if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
				return;

			for (int s = 0; s < GenerationSurfaces.Length; s++)
			{
				ParticleThumbnailRequest request = new ParticleThumbnailRequest(guid, assetPath, GenerationSurfaces[s]);
				if (!Queued.Contains(request))
					Enqueue(request, prioritize: false);
			}
		}

		private static void Enqueue(ParticleThumbnailRequest request, bool prioritize)
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

		private static void EnqueuePersistentLoad(ParticleThumbnailRequest request, string dependencyToken, string cacheKey)
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

		private static void StoreCacheRecord(ParticleThumbnailRequest request, string dependencyToken, Texture2D texture, bool persistToDisk)
		{
			if (texture == null)
				return;

			EnsureCacheStatsInitialized();
			long previousTextureBytes = 0L;
			if (Cache.TryGetValue(request, out ParticleThumbnailRecord existing)
			    && existing?.Texture != null
			    && existing.Texture != texture)
			{
				previousTextureBytes = GetTextureFootprintBytes(existing.Texture);
				UnityEngine.Object.DestroyImmediate(existing.Texture);
			}

			ParticleThumbnailRecord record = new ParticleThumbnailRecord
			{
				Guid = request.Guid,
				AssetPath = request.AssetPath,
				DependencyToken = dependencyToken,
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
			string settingsToken = ParticleThumbnailSettings.GetPersistentSettingsToken();
			KnownPersistentCacheMisses.Remove(ParticleThumbnailPersistentCache.BuildCacheKey(request, dependencyToken, settingsToken));

			if (persistToDisk)
			{
				ParticleThumbnailPersistentCache.SaveTexture(request, dependencyToken, texture);
				RefreshDiskStatsSnapshot();
			}
		}

		private static void TouchLru(ParticleThumbnailRequest request)
		{
			if (CacheLruNodes.TryGetValue(request, out LinkedListNode<ParticleThumbnailRequest> existingNode))
			{
				CacheLru.Remove(existingNode);
				CacheLru.AddFirst(existingNode);
				return;
			}

			LinkedListNode<ParticleThumbnailRequest> node = CacheLru.AddFirst(request);
			CacheLruNodes[request] = node;
		}

		private static void EnforceCacheLimit()
		{
			int max = ParticleThumbnailSettings.MemoryCacheMaxEntries;
			while (CacheLru.Count > max)
			{
				LinkedListNode<ParticleThumbnailRequest> tail = CacheLru.Last;
				if (tail == null)
					break;

				RemoveCacheEntry(tail.Value);
			}
		}

		private static void RemoveCacheEntry(ParticleThumbnailRequest request)
		{
			if (Cache.TryGetValue(request, out ParticleThumbnailRecord record))
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

			if (CacheLruNodes.TryGetValue(request, out LinkedListNode<ParticleThumbnailRequest> node))
			{
				CacheLru.Remove(node);
				CacheLruNodes.Remove(request);
			}

			RequestLastSeenFrame.Remove(request);
			RefreshVolatileStatsSnapshot();
		}

		private static void RemoveFailedEntries(string assetPath, string guid)
		{
			List<ParticleThumbnailRequest> toRemove = new List<ParticleThumbnailRequest>();
			foreach (KeyValuePair<ParticleThumbnailRequest, string> kv in FailedDependencyByRequest)
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
			Queue<ParticleThumbnailRequest> retainedPriority = new Queue<ParticleThumbnailRequest>(PriorityRenderQueue.Count);
			while (PriorityRenderQueue.Count > 0)
			{
				ParticleThumbnailRequest request = PriorityRenderQueue.Dequeue();
				bool remove = request.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && request.Guid == guid);
				if (remove)
				{
					Queued.Remove(request);
					PriorityQueued.Remove(request);
					RequestLastSeenFrame.Remove(request);
					CompleteGenerateAllRequest(request, success: false);
					anyRemoved = true;
					continue;
				}

				retainedPriority.Enqueue(request);
			}

			while (retainedPriority.Count > 0)
				PriorityRenderQueue.Enqueue(retainedPriority.Dequeue());

			Queue<ParticleThumbnailRequest> retained = new Queue<ParticleThumbnailRequest>(RenderQueue.Count);
			while (RenderQueue.Count > 0)
			{
				ParticleThumbnailRequest request = RenderQueue.Dequeue();
				bool remove = request.AssetPath == assetPath || (!string.IsNullOrEmpty(guid) && request.Guid == guid);
				if (remove)
				{
					Queued.Remove(request);
					RequestLastSeenFrame.Remove(request);
					CompleteGenerateAllRequest(request, success: false);
					anyRemoved = true;
					continue;
				}

				retained.Enqueue(request);
			}

			while (retained.Count > 0)
				RenderQueue.Enqueue(retained.Dequeue());

			if (PendingPersistentLoadQueue.Count == 0)
				return;

			Queue<DeferredPersistentLoadRequest> retainedLoads = new Queue<DeferredPersistentLoadRequest>(PendingPersistentLoadQueue.Count);
			while (PendingPersistentLoadQueue.Count > 0)
			{
				DeferredPersistentLoadRequest deferred = PendingPersistentLoadQueue.Dequeue();
				ParticleThumbnailRequest request = deferred.Request;
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
			RefreshRuntimeHooks(refreshSelectionCacheWhenEnabled: true);
		}

		private static void TrackGenerateAllRequest(ParticleThumbnailRequest request)
		{
			if (!GenerateAllPendingRequests.Add(request))
				return;

			GenerateAllTotalCount++;
			RefreshVolatileStatsSnapshot();
		}

		private static bool CompleteGenerateAllRequest(ParticleThumbnailRequest request, bool success)
		{
			if (!GenerateAllPendingRequests.Remove(request))
				return false;

			GenerateAllCompletedCount++;
			if (success)
				GenerateAllSucceededCount++;
			else
				GenerateAllFailedCount++;

			if (GenerateAllCompletedCount >= GenerateAllTotalCount)
				GenerateAllCurrentAssetPath = string.Empty;

			RequestLastSeenFrame.Remove(request);
			RefreshVolatileStatsSnapshot();
			return true;
		}

		private static void ResetGenerateAllProgress()
		{
			GenerateAllPendingRequests.Clear();
			GenerateAllTotalCount = 0;
			GenerateAllCompletedCount = 0;
			GenerateAllSucceededCount = 0;
			GenerateAllFailedCount = 0;
			GenerateAllIsPreparing = false;
			GenerateAllPreparingTotal = 0;
			GenerateAllPreparingIndex = 0;
			GenerateAllCurrentAssetPath = string.Empty;
			GenerateAllProgressWindowDismissedByUser = false;
			GenerateAllScanGuids = Array.Empty<string>();
			GenerateAllScanIndex = 0;
			GenerateAllScanInProgress = false;
			GenerateAllUseUnthrottledProcessing = false;
			GenerateAllRunActive = false;
			GenerateAllStartTime = 0.0;
			GenerateAllWarmupFramePending = false;
			RefreshVolatileStatsSnapshot();
		}

		private static void MarkRequestSeen(ParticleThumbnailRequest request)
		{
			RequestLastSeenFrame[request] = Time.frameCount;
		}

		private static bool TryDequeueRequest(out ParticleThumbnailRequest request)
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

		internal static void StoreCacheRecordForTests(ParticleThumbnailRequest request, string dependencyToken, Texture2D texture)
		{
			StoreCacheRecord(request, dependencyToken, texture, persistToDisk: false);
		}

		internal static void RemoveCacheEntryForTests(ParticleThumbnailRequest request)
		{
			RemoveCacheEntry(request);
		}

		internal static void EnqueueForTests(ParticleThumbnailRequest request, bool prioritize)
		{
			Enqueue(request, prioritize);
		}

		internal static void TrackGenerateAllRequestForTests(ParticleThumbnailRequest request)
		{
			TrackGenerateAllRequest(request);
		}

		internal static void CompleteGenerateAllRequestForTests(ParticleThumbnailRequest request, bool success)
		{
			CompleteGenerateAllRequest(request, success);
		}

		internal static void MarkFailedRequestForTests(ParticleThumbnailRequest request, string dependencyToken)
		{
			FailedDependencyByRequest[request] = dependencyToken ?? string.Empty;
			RefreshVolatileStatsSnapshot();
		}

		private static void EnsureCacheStatsInitialized()
		{
			if (CacheStatsInitialized)
				return;

			ParticleThumbnailPersistentCache.GetCachedDiskStats(out int persistentCount, out long diskBytes);

			CachedStats = new CacheStats
			{
				TotalEntries = Cache.Count,
				PersistentEntryCount = persistentCount,
				GeneratingCount = GenerateAllPendingRequests.Count,
				FailedCount = FailedDependencyByRequest.Count,
				QueueDepth = PriorityRenderQueue.Count + RenderQueue.Count,
				DiskCacheBytes = diskBytes,
			};

			foreach (ParticleThumbnailRecord record in Cache.Values)
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
			CachedStats.GeneratingCount = GenerateAllPendingRequests.Count;
			CachedStats.FailedCount = FailedDependencyByRequest.Count;
			CachedStats.QueueDepth = PriorityRenderQueue.Count + RenderQueue.Count;
		}

		private static void RefreshDiskStatsSnapshot()
		{
			EnsureCacheStatsInitialized();
			ParticleThumbnailPersistentCache.GetCachedDiskStats(out int persistentCount, out long diskBytes);
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
			return texture == null ? 0L : (long) texture.width * texture.height * 4L;
		}

		private static bool ShouldDropStaleRequest(ParticleThumbnailRequest request)
		{
			if (GenerateAllPendingRequests.Contains(request))
				return false;

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

		private static void UpdateGenerateAllProgressWindow()
		{
			if (TryGetGenerateAllProgressWindowState(out _, out _, out _))
			{
				if (GenerateAllProgressWindowDismissedByUser)
					return;

				ParticleThumbnailGenerateProgressWindow.ShowOrRefresh();
				return;
			}

			GenerateAllProgressWindowDismissedByUser = false;
			SafeClearProgressWindow();
		}

		private static void ShowImmediatePreparingPopup()
		{
			GenerateAllProgressWindowDismissedByUser = false;
			GenerateAllIsPreparing = true;
			GenerateAllPreparingTotal = 0;
			GenerateAllPreparingIndex = 0;
			GenerateAllCurrentAssetPath = string.Empty;
			UpdateGenerateAllProgressWindow();
		}

		internal static void NotifyProgressWindowClosedByUser()
		{
			if (TryGetGenerateAllProgressWindowState(out _, out _, out _))
				GenerateAllProgressWindowDismissedByUser = true;
		}

		private static string GetProgressAssetLabel(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath))
				return "Current: (starting)";

			return $"Current: {assetPath}";
		}

		internal static bool TryGetGenerateAllProgressWindowState(out string headline, out string detail, out float progress01)
		{
			if (GenerateAllIsPreparing)
			{
				progress01 = GenerateAllPreparingTotal > 0
					? Mathf.Clamp01((float) GenerateAllPreparingIndex / GenerateAllPreparingTotal)
					: 0f;
				headline = GenerateAllPreparingTotal > 0
					? $"Preparing queue... {GenerateAllPreparingIndex}/{GenerateAllPreparingTotal}"
					: "Preparing queue...";
				detail = GetProgressAssetLabel(GenerateAllCurrentAssetPath);
				return true;
			}

			if (!TryGetGenerateAllProgress(
				    out progress01,
				    out int completed,
				    out int total,
				    out int succeeded,
				    out int failed))
			{
				headline = string.Empty;
				detail = string.Empty;
				return false;
			}

			if (!IsGenerateAllInProgress)
			{
				headline = string.Empty;
				detail = string.Empty;
				return false;
			}

			headline = $"Generated {completed}/{total} (Succeeded: {succeeded}, Failed: {failed})";
			detail = GetProgressAssetLabel(GenerateAllCurrentAssetPath);
			return true;
		}

		private static void SafeClearProgressWindow()
		{
			ParticleThumbnailGenerateProgressWindow.CloseIfOpen();
		}

		private static void RepaintAllRelevantWindows()
		{
			EditorApplication.RepaintProjectWindow();
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}

		private static void RefreshSelectedAssetGuidCache()
		{
			SingleSelectedAssetGuid = string.Empty;
			SelectedAssetGuids.Clear();

			string[] selectedAssetGuids = Selection.assetGUIDs;
			if (selectedAssetGuids == null || selectedAssetGuids.Length == 0)
				return;

			if (selectedAssetGuids.Length == 1)
			{
				SingleSelectedAssetGuid = selectedAssetGuids[0] ?? string.Empty;
				return;
			}

			for (int i = 0; i < selectedAssetGuids.Length; i++)
			{
				string selectedGuid = selectedAssetGuids[i];
				if (!string.IsNullOrEmpty(selectedGuid))
					SelectedAssetGuids.Add(selectedGuid);
			}
		}

		private static void OnSelectionChanged()
		{
			RefreshSelectedAssetGuidCache();
			if (ParticleThumbnailSettings.Enabled && ParticleThumbnailSettings.DrawInProjectGrid)
				EditorApplication.RepaintProjectWindow();
		}

		private static bool ShouldProjectWindowHookBeActive()
		{
			return ParticleThumbnailSettings.Enabled
			       && (ParticleThumbnailSettings.DrawInProjectGrid || ParticleThumbnailSettings.DrawInProjectList);
		}

		private static bool HasPendingEditorWork()
		{
			if (GenerateAllWarmupFramePending || GenerateAllIsPreparing || GenerateAllScanInProgress)
				return true;

			if (GenerateAllPendingRequests.Count > 0)
				return true;

			if (PendingSupportLookupQueue.Count > 0)
				return true;

			if (PendingPersistentLoadQueue.Count > 0)
				return true;

			return PriorityRenderQueue.Count > 0 || RenderQueue.Count > 0;
		}

		private static void RefreshRuntimeHooks(bool refreshSelectionCacheWhenEnabled = false)
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

			bool shouldSelectionHookBeActive = shouldProjectWindowHookBeActive;
			if (shouldSelectionHookBeActive != SelectionChangedHookRegistered)
			{
				if (shouldSelectionHookBeActive)
					Selection.selectionChanged += OnSelectionChanged;
				else
					Selection.selectionChanged -= OnSelectionChanged;

				SelectionChangedHookRegistered = shouldSelectionHookBeActive;
			}

			if (shouldSelectionHookBeActive && refreshSelectionCacheWhenEnabled)
				RefreshSelectedAssetGuidCache();

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

	internal sealed class ParticleThumbnailGenerateProgressWindow : EditorWindow
	{
		private static readonly Vector2 WindowSize = new Vector2(560f, 190f);
		private static readonly Color Background = new Color(0.055f, 0.06f, 0.07f, 1f);
		private static readonly Color Accent = new Color(0.11f, 0.84f, 0.39f, 1f);
		private static readonly Color Border = new Color(1f, 1f, 1f, 0.12f);
		private static readonly Color ProgressTrack = new Color(0.17f, 0.19f, 0.22f, 1f);
		private static readonly Color ProgressFill = new Color(0.11f, 0.84f, 0.39f, 1f);
		private static readonly Color ProgressFillBright = new Color(0.18f, 0.95f, 0.46f, 1f);
		private static readonly Color TitleText = new Color(0.90f, 0.92f, 0.95f, 1f);
		private static readonly Color MutedText = new Color(0.68f, 0.70f, 0.74f, 1f);
		private static ParticleThumbnailGenerateProgressWindow instance;
		private static bool closeRequestedByCode;
		private static GUIStyle titleStyle;
		private static GUIStyle headlineStyle;
		private static GUIStyle detailStyle;
		private static GUIStyle percentStyle;

		internal static void ShowOrRefresh()
		{
			if (instance == null)
			{
				ParticleThumbnailGenerateProgressWindow window = CreateInstance<ParticleThumbnailGenerateProgressWindow>();
				if (window == null)
					return;

				window.titleContent = new GUIContent("");
				window.minSize = WindowSize;
				window.maxSize = WindowSize;

				Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
				Vector2 center = mainWindow.center;
				window.position = new Rect(
					center.x - WindowSize.x * 0.5f,
					center.y - WindowSize.y * 0.5f,
					WindowSize.x,
					WindowSize.y);

				instance = window;
				window.ShowUtility();
			}

			if (instance != null)
				instance.Repaint();
		}

		internal static void CloseIfOpen()
		{
			if (instance == null)
				return;

			ParticleThumbnailGenerateProgressWindow existing = instance;
			instance = null;
			closeRequestedByCode = true;
			try
			{
				existing.Close();
			}
			finally
			{
				closeRequestedByCode = false;
			}
		}

		private void OnEnable()
		{
			EditorApplication.update += HandleEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= HandleEditorUpdate;
			bool shouldNotifyDismiss = !closeRequestedByCode;
			if (instance == this)
				instance = null;

			if (shouldNotifyDismiss)
				ParticleThumbnailService.NotifyProgressWindowClosedByUser();
		}

		private void HandleEditorUpdate()
		{
			if (instance != this)
				return;

			if (!ParticleThumbnailService.TryGetGenerateAllProgressWindowState(out _, out _, out _))
			{
				CloseIfOpen();
				return;
			}

			Repaint();
		}

		private void OnGUI()
		{
			if (!ParticleThumbnailService.TryGetGenerateAllProgressWindowState(out string headline, out string detail, out float progress01))
			{
				CloseIfOpen();
				return;
			}

			EnsureStyles();

			Rect fullRect = new Rect(0f, 0f, position.width, position.height);
			EditorGUI.DrawRect(fullRect, Background);
			EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 4f), Accent);
			DrawFrameBorder(fullRect);

			float pad = 24f;
			float contentWidth = position.width - pad * 2f;
			float y = 20f;

			EditorGUI.LabelField(new Rect(pad, y, contentWidth, 28f), "Generating Particle Thumbnails", titleStyle);
			y += 34f;

			EditorGUI.LabelField(new Rect(pad, y, contentWidth, 24f), headline, headlineStyle);
			y += 26f;

			float detailHeight = detailStyle.CalcHeight(new GUIContent(detail), contentWidth);
			EditorGUI.LabelField(new Rect(pad, y, contentWidth, detailHeight), detail, detailStyle);
			y += detailHeight + 14f;

			Rect trackRect = new Rect(pad, y, contentWidth, 16f);
			DrawProgressBar(trackRect, progress01);

			Rect percentRect = new Rect(trackRect.x, trackRect.y - 1f, trackRect.width, trackRect.height);
			EditorGUI.LabelField(percentRect, $"{Mathf.RoundToInt(progress01 * 100f)}%", percentStyle);
		}

		private static void DrawProgressBar(Rect rect, float progress01)
		{
			float progress = Mathf.Clamp01(progress01);
			EditorGUI.DrawRect(rect, ProgressTrack);
			if (progress <= 0f)
				return;

			float fillWidth = rect.width * progress;
			Rect fillRect = new Rect(rect.x, rect.y, fillWidth, rect.height);
			EditorGUI.DrawRect(fillRect, ProgressFill);

			float glowWidth = Mathf.Min(8f, fillRect.width);
			if (glowWidth > 0f)
			{
				Rect glowRect = new Rect(fillRect.xMax - glowWidth, fillRect.y, glowWidth, fillRect.height);
				EditorGUI.DrawRect(glowRect, ProgressFillBright);
			}
		}

		private static void EnsureStyles()
		{
			if (titleStyle == null)
			{
				titleStyle = new GUIStyle(EditorStyles.boldLabel)
				{
					fontSize = 15,
					alignment = TextAnchor.MiddleCenter
				};
				titleStyle.normal.textColor = TitleText;
			}

			if (headlineStyle == null)
			{
				headlineStyle = new GUIStyle(EditorStyles.label)
				{
					fontSize = 12,
					alignment = TextAnchor.MiddleLeft
				};
				headlineStyle.normal.textColor = TitleText;
			}

			if (detailStyle == null)
			{
				detailStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
				{
					fontSize = 11,
					wordWrap = true
				};
				detailStyle.normal.textColor = MutedText;
			}

			if (percentStyle == null)
			{
				percentStyle = new GUIStyle(EditorStyles.miniBoldLabel)
				{
					alignment = TextAnchor.MiddleCenter
				};
				percentStyle.normal.textColor = TitleText;
			}
		}

		private static void DrawFrameBorder(Rect fullRect)
		{
			EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.y, fullRect.width, 1f), Border);
			EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.yMax - 1f, fullRect.width, 1f), Border);
			EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.y, 1f, fullRect.height), Border);
			EditorGUI.DrawRect(new Rect(fullRect.xMax - 1f, fullRect.y, 1f, fullRect.height), Border);
		}
	}

}
