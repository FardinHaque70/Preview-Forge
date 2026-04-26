using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

[InitializeOnLoad]
public static class PrefabThumbnailService
{
    [Flags]
    public enum SettingsApplyImpact
    {
        None = 0,
        Repaint = 1 << 0,
        ClearInMemoryCache = 1 << 1,
        VersionPersistentCache = 1 << 2,
    }

    private sealed class MarkerRecord : ThumbnailCacheRecord { }

    private struct SupportCacheEntry
    {
        public string assetPath;
        public long dependencyHash;
        public ThumbnailSupportInfo supportInfo;
        public ThumbnailProviderBase provider;
    }

    private struct AssetUiInfoCacheEntry
    {
        public string assetPath;
        public long dependencyHash;
        public bool hasExpandableSubAssets;
        public int mainAssetInstanceId;
    }

    private struct RowTrackingState
    {
        public float minPrimaryX;
        public int lastSeenFrame;
    }

    private readonly struct RowTrackingKey : IEquatable<RowTrackingKey>
    {
        public readonly string guid;
        public readonly ThumbnailSurface surface;
        public readonly int widthBucket;
        public readonly int heightBucket;

        public RowTrackingKey(string guid, ThumbnailSurface surface, int widthBucket, int heightBucket)
        {
            this.guid = guid;
            this.surface = surface;
            this.widthBucket = widthBucket;
            this.heightBucket = heightBucket;
        }

        public bool Equals(RowTrackingKey other)
        {
            return string.Equals(guid, other.guid, StringComparison.Ordinal)
                && surface == other.surface
                && widthBucket == other.widthBucket
                && heightBucket == other.heightBucket;
        }

        public override bool Equals(object obj)
        {
            return obj is RowTrackingKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = guid != null ? guid.GetHashCode() : 0;
                hash = (hash * 397) ^ (int)surface;
                hash = (hash * 397) ^ widthBucket;
                hash = (hash * 397) ^ heightBucket;
                return hash;
            }
        }
    }

    private enum ProjectWindowItemKind : byte
    {
        Skip = 0,
        ThumbnailSource = 1,
        OverlayCandidate = 2,
    }

    private enum ObjectPickerItemKind : byte
    {
        Skip = 0,
        ThumbnailSource = 1,
        OverlayCandidate = 2,
    }

    private struct ProjectWindowItemCacheEntry
    {
        public string assetPath;
        public ProjectWindowItemKind kind;
        public bool overlayBadgeResolved;
        public bool hasOverlayBadge;
        public ThumbnailBadgeType overlayBadgeType;
        public bool overlayUiInfoResolved;
        public bool overlayHasExpandableSubAssets;
        public int overlayMainAssetInstanceId;
    }

    private struct ObjectPickerItemCacheEntry
    {
        public string assetPath;
        public ObjectPickerItemKind kind;
    }

    private static readonly ThumbnailCacheRecord s_generating = new MarkerRecord();
    private static readonly ThumbnailCacheRecord s_failed = new MarkerRecord();
    private static readonly Dictionary<ThumbnailRequest, ThumbnailCacheRecord> s_cache = new();
    private static readonly LinkedList<ThumbnailRequest> s_cacheLruOrder = new();
    private static readonly Dictionary<ThumbnailRequest, LinkedListNode<ThumbnailRequest>> s_cacheLruNodes = new();
    private static readonly Dictionary<string, SupportCacheEntry> s_supportCache = new();
    private static readonly Dictionary<string, AssetUiInfoCacheEntry> s_assetUiInfoCache = new();
    private static readonly Dictionary<string, ProjectWindowItemCacheEntry> s_projectWindowItemCache = new();
    private static readonly Dictionary<string, ObjectPickerItemCacheEntry> s_objectPickerItemCache = new();
    private static readonly Queue<ThumbnailRequest> s_generateQueue = new();
    private static readonly HashSet<ThumbnailRequest> s_queued = new();
    private static readonly Queue<ThumbnailRequest> s_prefetchQueue = new();
    private static readonly HashSet<ThumbnailRequest> s_prefetchQueued = new();
    private static readonly HashSet<ThumbnailRequest> s_retryPending = new();
    private static readonly Dictionary<ThumbnailRequest, int> s_retryCounts = new();
    private static readonly Dictionary<RowTrackingKey, RowTrackingState> s_primaryRowStateByKey = new();
    private static readonly HashSet<string> s_foldersThisRepaint = new();
    private static readonly HashSet<int> s_expandedProjectWindowItemIds = new();
    private static readonly HashSet<int> s_lastExpandedProjectWindowItemIds = new();
    private static readonly ThumbnailRenderContext s_renderContext = new();
    private static readonly HashSet<string> s_selectedAssetGuids = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] s_overlayBadgeTextureExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".tga",
        ".gif",
        ".bmp",
        ".psd",
        ".exr",
        ".hdr",
        ".iff",
        ".pict",
        ".dds",
        ".ktx",
        ".ktx2",
        ".cubemap",
    };
    private const float SelectionFrameBorderThickness = 2f;
    private const float SelectionFrameNotchCutoutWidth = 18f;
    private const float SelectionFrameNotchGapHeight = 22f;
    private const float SelectionFrameNotchGapOffset = 0f;
    private const int MaxObjectSelectorEnqueuesPerFrame = 1;
    private const int MaxObjectSelectorPendingGenerateRequests = 8;
    private static bool s_guiCallbackRegistered;
    private static bool s_retryFlushScheduled;
    private static bool s_tickRegistered;
    private static bool s_expansionRepaintScheduled;
    private static double s_lastForcedRepaintTime;
    private static double s_lastObjectSelectorSpinnerRepaintTime;
    private static int s_lastSearchModeFrame = -1;
    private static bool s_cachedSearchModeResult;
    private static int s_lastFolderPrefetchFrame = -1;
    private static int s_lastBrowserHeightFrame = -1;
    private static int s_lastExpandedStatePollFrame = -1;
    private static int s_lastPrimaryRowTrackingFrame = -1;
    private static int s_lastObjectSelectorEnqueueFrame = -1;
    private static int s_objectSelectorEnqueuesThisFrame;
    private static float s_cachedBrowserHeight;
    private static string s_lastSearchPrefetchSignature = string.Empty;
    private static string s_singleSelectedAssetGuid = string.Empty;

    public readonly struct ThumbnailPreviewResult
    {
        public readonly bool Supported;
        public readonly bool IsLoading;
        public readonly Texture Texture;
        public readonly ThumbnailBadgeType BadgeType;

        public ThumbnailPreviewResult(bool supported, bool isLoading, Texture texture, ThumbnailBadgeType badgeType)
        {
            Supported = supported;
            IsLoading = isLoading;
            Texture = texture;
            BadgeType = badgeType;
        }
    }

    static PrefabThumbnailService()
    {
        ThumbnailProviderRegistry.Register(new ParticlePrefabThumbnailProvider());
        ThumbnailProviderRegistry.Register(new UiPrefabThumbnailProvider());
        ThumbnailProviderRegistry.Register(new SpritePrefabThumbnailProvider());
        ThumbnailProviderRegistry.Register(new GeneralPrefabThumbnailProvider());
        ThumbnailProviderRegistry.Register(new ModelThumbnailProvider());
        ThumbnailProviderRegistry.Register(new MaterialThumbnailProvider());

        AssemblyReloadEvents.beforeAssemblyReload -= CleanupAll;
        AssemblyReloadEvents.beforeAssemblyReload += CleanupAll;
        EditorApplication.quitting -= CleanupAll;
        EditorApplication.quitting += CleanupAll;
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
        RefreshSelectedAssetGuidCache();

        if (ImprovedThumbnailSettings.Active)
            RegisterGuiCallback();

        MaterialThumbnailChangeWatcher.RefreshSubscriptions();
    }

    public static void ClearAllCaches()
    {
        ClearInMemoryCaches();
        ThumbnailPersistentCacheUtility.ClearAll();
        EditorApplication.RepaintProjectWindow();
    }

    public static void ClearInMemoryCacheOnly()
    {
        ClearInMemoryCaches();
        EditorApplication.RepaintProjectWindow();
    }

    public static void ClearProjectWindowItemCache()
    {
        s_projectWindowItemCache.Clear();
        s_objectPickerItemCache.Clear();
    }

    public static void SetActive(bool active)
    {
        if (active)
        {
            RegisterGuiCallback();
            MaterialThumbnailChangeWatcher.RefreshSubscriptions();
            EditorApplication.RepaintProjectWindow();
        }
        else
        {
            StopTicking();
            ClearInMemoryCaches();
            // Disabling the system should not wipe persistent disk cache.
            // Keep disk thumbnails so re-enabling is instant and avoids forced rebuild.
            UnregisterGuiCallback();
            MaterialThumbnailChangeWatcher.RefreshSubscriptions();
            s_renderContext.Dispose();
            EditorApplication.RepaintProjectWindow();
        }
    }

    private static void RegisterGuiCallback()
    {
        if (s_guiCallbackRegistered)
            return;
        s_guiCallbackRegistered = true;
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }

    private static void UnregisterGuiCallback()
    {
        if (!s_guiCallbackRegistered)
            return;
        s_guiCallbackRegistered = false;
        EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
    }

    public static void ApplySettings(SettingsApplyImpact impact)
    {
        MaterialThumbnailChangeWatcher.RefreshSubscriptions();
        StopTicking();
        ClearPendingGenerationState();

        // VersionPersistentCache means rendered output has changed — in-memory thumbnails are
        // equally stale, so always clear memory when versioning the persistent cache.
        if ((impact & (SettingsApplyImpact.ClearInMemoryCache | SettingsApplyImpact.VersionPersistentCache)) != 0)
            ClearInMemoryCaches();

        if ((impact & SettingsApplyImpact.VersionPersistentCache) != 0)
            LogVerbose("Thumbnail settings apply requested versioned persistent cache invalidation.");

        if ((impact & SettingsApplyImpact.Repaint) != 0 || impact == SettingsApplyImpact.None)
            EditorApplication.RepaintProjectWindow();
    }

    private static void ClearInMemoryCaches()
    {
        foreach (ThumbnailCacheRecord record in s_cache.Values)
            DestroyRecordTextures(record);

        s_cache.Clear();
        s_cacheLruOrder.Clear();
        s_cacheLruNodes.Clear();
        s_queued.Clear();
        s_generateQueue.Clear();
        s_prefetchQueued.Clear();
        s_prefetchQueue.Clear();
        s_retryPending.Clear();
        s_retryCounts.Clear();
        s_retryFlushScheduled = false;
        EditorApplication.delayCall -= FlushPendingRetries;
        s_lastForcedRepaintTime = 0;
        s_supportCache.Clear();
        s_assetUiInfoCache.Clear();
        s_projectWindowItemCache.Clear();
        s_objectPickerItemCache.Clear();
        s_primaryRowStateByKey.Clear();
        s_lastPrimaryRowTrackingFrame = -1;
        s_foldersThisRepaint.Clear();
        s_expandedProjectWindowItemIds.Clear();
        s_lastExpandedProjectWindowItemIds.Clear();
        s_lastExpandedStatePollFrame = -1;
        s_expansionRepaintScheduled = false;
        EditorApplication.delayCall -= RepaintProjectWindowAfterExpansionChange;
        s_lastSearchPrefetchSignature = string.Empty;
        s_lastObjectSelectorEnqueueFrame = -1;
        s_objectSelectorEnqueuesThisFrame = 0;
    }

    public static void RegeneratePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        InvalidatePath(assetPath);
        EditorApplication.RepaintProjectWindow();
    }

    public static void RegeneratePaths(IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
            return;

        foreach (string assetPath in assetPaths)
            RegeneratePath(assetPath);
    }

    public static void GenerateAllThumbnailsWithProgress()
    {
        if (!ImprovedThumbnailSettings.Active)
        {
            Debug.LogWarning("[ImprovedThumbnail] Thumbnail generation is disabled. Enable Improved Thumbnail first to generate thumbnails.");
            return;
        }

        List<ThumbnailRequest> requests = CollectWarmupRequests();
        if (requests.Count == 0)
        {
            Debug.Log("[ImprovedThumbnail] No supported prefab, model, or material assets were found to warm.");
            return;
        }

        StopTicking();
        ClearPendingGenerationState();
        // "Generate All" is expected to rebuild thumbnails from scratch.
        // Clear both memory and disk cache first so stale thumbnails are never reused.
        ClearInMemoryCaches();
        ThumbnailPersistentCacheUtility.ClearAll();

        int generatedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        bool cancelled = false;

        try
        {
            for (int i = 0; i < requests.Count; i++)
            {
                ThumbnailRequest request = requests[i];
                string assetName = System.IO.Path.GetFileNameWithoutExtension(request.AssetPath);
                float progress = requests.Count > 0 ? i / (float)requests.Count : 1f;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Generating Improved Thumbnails",
                    $"{i + 1}/{requests.Count}: {assetName}",
                    progress))
                {
                    cancelled = true;
                    break;
                }

                ThumbnailCacheRecord existing = null;
                bool hasCache = TryGetValidCache(request, out existing);
                if (hasCache
                    && existing != s_failed
                    && existing != s_generating
                    && existing?.Frames != null)
                {
                    skippedCount++;
                    continue;
                }

                if (existing == s_failed || existing == s_generating)
                    RemoveCacheEntry(request);

                RenderRequest(request);

                if (TryGetValidCache(request, out ThumbnailCacheRecord generated)
                    && generated != s_failed
                    && generated != s_generating
                    && generated?.Frames != null)
                {
                    ThumbnailPersistentCacheUtility.SaveRecord(request, generated);
                    generatedCount++;
                }
                else
                {
                    failedCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            EditorApplication.RepaintProjectWindow();
        }

        if (cancelled)
        {
            if (ImprovedThumbnailSettings.VerboseLogging)
                Debug.LogWarning($"[ImprovedThumbnail] Thumbnail generation cancelled. Generated {generatedCount} thumbnails before cancellation. Skipped {skippedCount}.");
            return;
        }

        LogVerbose($"Generated {generatedCount} thumbnails. Skipped {skippedCount}. Failed {failedCount}. Disk cache: {ThumbnailPersistentCacheUtility.GetCachedFileCount()}.");
    }

    public static void InvalidatePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid))
        {
            s_supportCache.Remove(guid);
            s_assetUiInfoCache.Remove(guid);
            s_projectWindowItemCache.Remove(guid);
            s_objectPickerItemCache.Remove(guid);
            ThumbnailPersistentCacheUtility.InvalidateGuid(guid);
        }

        List<ThumbnailRequest> toRemove = new List<ThumbnailRequest>();
        foreach (KeyValuePair<ThumbnailRequest, ThumbnailCacheRecord> kv in s_cache)
        {
            bool guidMatches = !string.IsNullOrEmpty(guid) && kv.Key.Guid == guid;
            if (guidMatches || kv.Key.AssetPath == assetPath)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            RemoveCacheEntry(toRemove[i]);

        EditorApplication.RepaintProjectWindow();
    }

    public static bool SupportsPath(string assetPath)
    {
        if (!ImprovedThumbnailSettings.Active)
            return false;

        if (!ThumbnailAssetPathUtility.IsThumbnailSourceAssetPath(assetPath))
            return false;

        if (string.IsNullOrEmpty(assetPath))
            return false;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
            return false;

        ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
        return provider != null && supportInfo.Supported;
    }

    /// <summary>
    /// Called by the HarmonyPatcher via reflection from ObjectListArea.postAssetIconDrawCallback.
    /// <paramref name="iconRect"/> is the exact rect Unity drew the icon into — use it directly
    /// as the content rect (no GetContentRect transform needed).
    /// </summary>
    public static void DrawObjectSelectorItem(Rect iconRect, string guid, bool isListMode)
    {
        if (!ImprovedThumbnailSettings.Active) return;
        if (!ImprovedThumbnailSettings.ThumbnailDrawInObjectPicker) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Event.current?.type != EventType.Repaint) return;

        if (!TryGetObjectPickerItemInfo(guid, out string assetPath, out ObjectPickerItemKind itemKind))
            return;

        // Use a single cache surface for Object Picker to avoid grid/list cache key churn.
        // Object Picker warmup already targets ProjectWindowGrid.
        ThumbnailSurface surface = ThumbnailSurface.ProjectWindowGrid;
        if (!ShouldDrawOnSurface(surface)) return;

        float minDim = Mathf.Min(iconRect.width, iconRect.height);
        if (minDim < ImprovedThumbnailSettings.ThumbnailMinProjectIconSize && !isListMode) return;

        if (itemKind == ObjectPickerItemKind.OverlayCandidate)
        {
            if (!ImprovedThumbnailSettings.ShowThumbnailBadges || isListMode)
                return;

            if (TryGetOverlayOnlyBadge(assetPath, out ThumbnailBadgeType overlayBadge))
                DrawObjectSelectorBadge(iconRect, overlayBadge, assetPath, isListMode);
            return;
        }

        ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
        if (provider == null || !supportInfo.Supported)
            return;

        ThumbnailRequest request = new ThumbnailRequest(guid, assetPath, supportInfo.AssetKind, surface);

        if (!TryGetValidCache(request, out ThumbnailCacheRecord record))
        {
            if (TryEnqueueObjectSelectorRequest(request))
                DrawObjectSelectorSpinner(iconRect);
            return;
        }

        if (record == s_generating)
        {
            DrawObjectSelectorSpinner(iconRect);
            return;
        }

        if (record == s_failed || record?.Frames == null) return;

        Texture drawTexture = GetCurrentTexture(record);
        if (drawTexture == null) return;

        // iconRect IS the content area — draw directly without GetContentRect.
        EditorGUI.DrawRect(iconRect, ImprovedThumbnailSettings.ThumbnailBackgroundColor);
        GUI.DrawTexture(iconRect, drawTexture, ScaleMode.ScaleToFit, true);
        DrawObjectSelectorBadge(iconRect, GetBadgeType(record.AssetKind), assetPath, isListMode);
    }

    private static void DrawObjectSelectorSpinner(Rect iconRect)
    {
        EditorGUI.DrawRect(iconRect, ImprovedThumbnailSettings.ThumbnailBackgroundColor);
        int spinIndex = (int)(EditorApplication.timeSinceStartup * 12) % 12;
        GUIContent spinIcon = EditorGUIUtility.IconContent($"WaitSpin{spinIndex:D2}");
        if (spinIcon?.image == null) return;
        float sz = Mathf.Clamp(Mathf.Min(iconRect.width, iconRect.height) * 0.4f, 12f, 32f);
        Rect spinRect = new Rect(
            iconRect.x + (iconRect.width  - sz) * 0.5f,
            iconRect.y + (iconRect.height - sz) * 0.5f,
            sz, sz);
        GUI.DrawTexture(spinRect, spinIcon.image, ScaleMode.ScaleToFit);
    }

    private static bool TryGetObjectPickerItemInfo(
        string guid,
        out string assetPath,
        out ObjectPickerItemKind itemKind)
    {
        assetPath = string.Empty;
        itemKind = ObjectPickerItemKind.Skip;

        if (string.IsNullOrEmpty(guid))
            return false;

        if (s_objectPickerItemCache.TryGetValue(guid, out ObjectPickerItemCacheEntry cached))
        {
            assetPath = cached.assetPath;
            itemKind = cached.kind;
            return itemKind != ObjectPickerItemKind.Skip && !string.IsNullOrEmpty(assetPath);
        }

        assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        // Sub-assets share the same path but have different GUIDs — skip them.
        string mainAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.Equals(guid, mainAssetGuid, StringComparison.OrdinalIgnoreCase))
        {
            s_objectPickerItemCache[guid] = new ObjectPickerItemCacheEntry
            {
                assetPath = assetPath,
                kind = ObjectPickerItemKind.Skip,
            };
            return false;
        }

        if (ThumbnailAssetPathUtility.IsThumbnailSourceAssetPath(assetPath))
        {
            itemKind = ObjectPickerItemKind.ThumbnailSource;
        }
        else if (IsLikelyOverlayBadgeTexturePath(assetPath))
        {
            itemKind = ObjectPickerItemKind.OverlayCandidate;
        }
        else
        {
            itemKind = ObjectPickerItemKind.Skip;
        }

        s_objectPickerItemCache[guid] = new ObjectPickerItemCacheEntry
        {
            assetPath = assetPath,
            kind = itemKind,
        };

        return itemKind != ObjectPickerItemKind.Skip;
    }

    private static void DrawObjectSelectorBadge(Rect iconRect, ThumbnailBadgeType badgeType, string assetPath, bool isListMode)
    {
        if (!ImprovedThumbnailSettings.ShowThumbnailBadges) return;
        if (isListMode) return;
        // iconRect IS the content area — use DrawAtContentRect to skip GetContentRect,
        // which would apply grid padding a second time and shrink the badge incorrectly.
        ThumbnailBadgeUtility.DrawAtContentRect(iconRect, badgeType, assetPath);
    }

    private static bool TryEnqueueObjectSelectorRequest(ThumbnailRequest request)
    {
        int frame = Time.frameCount;
        if (s_lastObjectSelectorEnqueueFrame != frame)
        {
            s_lastObjectSelectorEnqueueFrame = frame;
            s_objectSelectorEnqueuesThisFrame = 0;
        }

        if (s_objectSelectorEnqueuesThisFrame >= MaxObjectSelectorEnqueuesPerFrame)
            return false;

        // Prevent large burst queues while fast-scrolling Object Picker.
        if (s_generateQueue.Count >= MaxObjectSelectorPendingGenerateRequests)
            return false;

        Enqueue(request);
        s_objectSelectorEnqueuesThisFrame++;
        return true;
    }

    public static ThumbnailPreviewResult GetStaticPreview(string assetPath, ThumbnailSurface surface, bool enqueueIfMissing = true)
    {
        if (!ImprovedThumbnailSettings.Active || string.IsNullOrEmpty(assetPath))
            return default;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
            return default;

        ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
        if (provider == null || !supportInfo.Supported)
            return default;

        ThumbnailRequest request = new ThumbnailRequest(
            guid,
            assetPath,
            supportInfo.AssetKind,
            surface);

        if (!TryGetValidCache(request, out ThumbnailCacheRecord record))
        {
            if (enqueueIfMissing)
                SchedulePrefetch(request);

            return new ThumbnailPreviewResult(
                supported: true,
                isLoading: enqueueIfMissing,
                texture: null,
                badgeType: GetBadgeType(supportInfo.AssetKind));
        }

        if (record == s_generating)
        {
            return new ThumbnailPreviewResult(
                supported: true,
                isLoading: true,
                texture: null,
                badgeType: GetBadgeType(supportInfo.AssetKind));
        }

        if (record == s_failed || record?.Frames == null)
        {
            return new ThumbnailPreviewResult(
                supported: true,
                isLoading: false,
                texture: null,
                badgeType: GetBadgeType(supportInfo.AssetKind));
        }

        return new ThumbnailPreviewResult(
            supported: true,
            isLoading: false,
            texture: record.Frames.StaticFrame ?? GetCurrentTexture(record),
            badgeType: GetBadgeType(record.AssetKind));
    }

    public static void PrefetchStaticPreviews(IEnumerable<string> assetPaths, ThumbnailSurface surface)
    {
        if (!ImprovedThumbnailSettings.Active || assetPaths == null)
            return;

        foreach (string assetPath in assetPaths)
        {
            if (string.IsNullOrEmpty(assetPath))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                continue;

            ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
            if (provider == null || !supportInfo.Supported)
                continue;

            ThumbnailRequest request = new ThumbnailRequest(
                guid,
                assetPath,
                supportInfo.AssetKind,
                surface);

            if (!TryGetValidCache(request, out _))
                SchedulePrefetch(request);
        }
    }

    private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        if (!ImprovedThumbnailSettings.Active)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Event evt = Event.current;
        if (evt == null)
            return;

        EventType eventType = evt.type;
        if (eventType != EventType.Repaint)
            return;

        if (!TryGetProjectWindowItemInfo(guid, out string assetPath, out ProjectWindowItemKind itemKind))
            return;

        ThumbnailSurface surface = GetSurface(selectionRect);
        if (!ShouldDrawOnSurface(surface))
            return;

        float minDimension = surface == ThumbnailSurface.ProjectWindowList ? selectionRect.height : Mathf.Min(selectionRect.width, selectionRect.height);
        if (minDimension < ImprovedThumbnailSettings.ThumbnailMinProjectIconSize && surface != ThumbnailSurface.ProjectWindowList)
            return;

        if (itemKind == ProjectWindowItemKind.OverlayCandidate
            && (surface == ThumbnailSurface.ProjectWindowList || !ImprovedThumbnailSettings.ShowThumbnailBadges))
            return;

        if (itemKind == ProjectWindowItemKind.OverlayCandidate)
        {
            if (!TryGetCachedOverlayBadge(guid, assetPath, out ThumbnailBadgeType overlayBadge))
                return;

            bool overlayReserveExpandArrow = false;
            bool overlayIsSubAssetRow;
            if (TryGetCachedOverlayUiInfo(guid, assetPath, out bool overlayHasExpandableSubAssets, out int overlayMainAssetInstanceId)
                && overlayHasExpandableSubAssets)
            {
                overlayReserveExpandArrow = true;
                PollExpandedProjectWindowState();
                BeginPrimaryRowTrackingFrame();
                bool overlayIsMainAssetExpanded = IsMainAssetExpanded(overlayMainAssetInstanceId);
                if (ShouldSuppressSubAssetRow(guid, selectionRect, surface, overlayReserveExpandArrow, overlayIsMainAssetExpanded, out overlayIsSubAssetRow))
                    return;
            }
            else
            {
                overlayIsSubAssetRow = false;
            }

            ThumbnailDrawingUtility.DrawBadgeOnly(selectionRect, overlayBadge, surface, overlayReserveExpandArrow, drawBadge: !overlayIsSubAssetRow, assetPath: assetPath);
            return;
        }

        ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
        if (provider == null || !supportInfo.Supported)
            return;

        PollExpandedProjectWindowState();
        BeginPrimaryRowTrackingFrame();

        AssetUiInfoCacheEntry assetUiInfo = GetAssetUiInfo(guid, assetPath);
        bool reserveExpandArrow = assetUiInfo.hasExpandableSubAssets;
        bool isMainAssetExpanded = reserveExpandArrow && IsMainAssetExpanded(assetUiInfo.mainAssetInstanceId);
        bool isSubAssetRow = false;
        bool useDefaultModelFrameScale = reserveExpandArrow && supportInfo.AssetKind == ThumbnailAssetKind.ModelAsset;

        if (ShouldSuppressSubAssetRow(guid, selectionRect, surface, reserveExpandArrow, isMainAssetExpanded, out isSubAssetRow))
            return;

        bool shouldDrawSelectionFrame = ShouldDrawGridSelectionFrameForGuid(surface, guid);
        ThumbnailRequest request = new ThumbnailRequest(guid, assetPath, supportInfo.AssetKind, surface);

        PrewarmFolderContentsIfNeeded(assetPath, surface);

        if (!TryGetValidCache(request, out ThumbnailCacheRecord record))
        {
            Enqueue(request);
            DrawLoadingSpinner(selectionRect, surface, reserveExpandArrow);
            if (shouldDrawSelectionFrame)
                DrawGridSelectionFrame(selectionRect, reserveExpandArrow, useDefaultModelFrameScale);
            return;
        }

        if (record == s_generating)
        {
            DrawLoadingSpinner(selectionRect, surface, reserveExpandArrow);
            if (shouldDrawSelectionFrame)
                DrawGridSelectionFrame(selectionRect, reserveExpandArrow, useDefaultModelFrameScale);
            return;
        }

        if (record == s_failed || record?.Frames == null)
        {
            if (shouldDrawSelectionFrame)
                DrawGridSelectionFrame(selectionRect, reserveExpandArrow, useDefaultModelFrameScale);
            return;
        }

        Texture drawTexture = GetCurrentTexture(record);
        if (drawTexture == null)
        {
            if (shouldDrawSelectionFrame)
                DrawGridSelectionFrame(selectionRect, reserveExpandArrow, useDefaultModelFrameScale);
            return;
        }

        ThumbnailDrawingUtility.DrawThumbnail(selectionRect, drawTexture, GetBadgeType(record.AssetKind), surface, reserveExpandArrow, drawBadge: !isSubAssetRow, assetPath: assetPath);
        if (shouldDrawSelectionFrame)
            DrawGridSelectionFrame(selectionRect, reserveExpandArrow, useDefaultModelFrameScale);
    }

    private static void EnsureTickRegistered()
    {
        if (s_tickRegistered)
            return;

        s_tickRegistered = true;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (!ImprovedThumbnailSettings.Active)
        {
            StopTicking();
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        int maxRenders = Mathf.Max(1, ImprovedThumbnailSettings.ThumbnailRenderPerUpdate);
        double budgetMs = ImprovedThumbnailSettings.ThumbnailRenderBudgetMs;
        double budgetStart = EditorApplication.timeSinceStartup * 1000.0;
        int rendered = 0;

        while (s_generateQueue.Count > 0 && rendered < maxRenders)
        {
            ThumbnailRequest request = s_generateQueue.Dequeue();
            s_queued.Remove(request);
            RenderRequest(request);
            rendered++;

            if (rendered > 0 && (EditorApplication.timeSinceStartup * 1000.0 - budgetStart) >= budgetMs)
                break;
        }

        while (s_prefetchQueue.Count > 0 && rendered < maxRenders)
        {
            ThumbnailRequest request = s_prefetchQueue.Dequeue();
            s_prefetchQueued.Remove(request);

            if (TryGetValidCache(request, out _))
                continue;

            MarkGenerating(request);
            RenderRequest(request);
            rendered++;

            if (rendered > 0 && (EditorApplication.timeSinceStartup * 1000.0 - budgetStart) >= budgetMs)
                break;
        }

        // Repaint after the render batch to show newly generated thumbnails, capped at 5/sec.
        if (rendered > 0)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - s_lastForcedRepaintTime >= 0.2)
            {
                s_lastForcedRepaintTime = now;
                EditorApplication.RepaintProjectWindow();
                if (ImprovedThumbnailSettings.ThumbnailDrawInObjectPicker)
                    EditorCompatibilityUtility.RepaintObjectSelectorWindows();
            }
        }

        // Keep ObjectSelector spinner animating while items are waiting in the queue,
        // independent of whether any render completed this tick.
        if (ImprovedThumbnailSettings.ThumbnailDrawInObjectPicker &&
            (s_generateQueue.Count > 0 || s_retryPending.Count > 0))
        {
            double spinNow = EditorApplication.timeSinceStartup;
            if (spinNow - s_lastObjectSelectorSpinnerRepaintTime >= 1.0 / 12.0)
            {
                s_lastObjectSelectorSpinnerRepaintTime = spinNow;
                EditorCompatibilityUtility.RepaintObjectSelectorWindows();
            }
        }

        if (s_generateQueue.Count == 0 && s_prefetchQueue.Count == 0)
            StopTicking();
    }

    private static void RenderRequest(ThumbnailRequest request)
    {
        double renderStart = EditorApplication.timeSinceStartup * 1000.0;
        try
        {
            ThumbnailProviderBase provider = GetProviderForAsset(request.Guid, request.AssetPath, out ThumbnailSupportInfo supportInfo);
            if (provider == null || !supportInfo.Supported)
            {
                ClearRetryState(request);
                s_cache[request] = s_failed;
                return;
            }

            UnityEngine.Object asset = provider.LoadAssetObject(request.AssetPath);
            if (asset == null)
            {
                ClearRetryState(request);
                s_cache[request] = s_failed;
                return;
            }

            ThumbnailProviderContext context = new ThumbnailProviderContext(request, asset);

            try
            {
                ThumbnailCacheRecord record = provider.Render(context, s_renderContext);
                StoreCacheRecord(request, record ?? s_failed);
            }
            catch (ThumbnailRenderPendingException)
            {
                QueueRetry(request);
            }
            catch (Exception e)
            {
                ClearRetryState(request);
                s_cache[request] = s_failed;
                throw new Exception($"[ImprovedThumbnail] Render failed for '{request.AssetPath}': {e.Message}", e);
            }
            finally
            {
                s_renderContext.SetInstance(null);
            }

            double elapsed = EditorApplication.timeSinceStartup * 1000.0 - renderStart;
            LogVerbose($"Rendered '{request.AssetPath}' in {elapsed:F1}ms");
        }
        catch (Exception e)
        {
            ClearRetryState(request);
            s_cache[request] = s_failed;
            Debug.LogWarning($"[ImprovedThumbnail] Failed to render thumbnail for '{request.AssetPath}': {e.Message}");
        }
    }

    private static void Enqueue(ThumbnailRequest request)
    {
        if (s_queued.Contains(request))
            return;

        MarkGenerating(request);
        s_queued.Add(request);
        s_generateQueue.Enqueue(request);
        EnsureTickRegistered();
        LogVerbose($"Enqueued '{request.AssetPath}' (queue depth: {s_generateQueue.Count})");
    }

    private static void QueueRetry(ThumbnailRequest request)
    {
        const int maxRetries = 120;

        int retryCount = s_retryCounts.TryGetValue(request, out int currentCount) ? currentCount + 1 : 1;
        if (retryCount > maxRetries)
        {
            s_retryCounts.Remove(request);
            s_retryPending.Remove(request);
            s_cache[request] = s_failed;
            Debug.LogWarning($"[ImprovedThumbnail] Timed out waiting for thumbnail render for '{request.AssetPath}'.");
            return;
        }

        s_retryCounts[request] = retryCount;
        MarkGenerating(request);
        s_retryPending.Add(request);

        if (s_retryFlushScheduled)
            return;

        s_retryFlushScheduled = true;
        EditorApplication.delayCall -= FlushPendingRetries;
        EditorApplication.delayCall += FlushPendingRetries;
        EnsureTickRegistered();
    }

    private static void FlushPendingRetries()
    {
        s_retryFlushScheduled = false;

        if (s_retryPending.Count == 0)
            return;

        foreach (ThumbnailRequest request in s_retryPending)
        {
            if (s_queued.Contains(request))
                continue;

            s_queued.Add(request);
            s_generateQueue.Enqueue(request);
        }

        s_retryPending.Clear();
        EnsureTickRegistered();
    }

    private static void MarkGenerating(ThumbnailRequest request)
    {
        if (s_cache.TryGetValue(request, out ThumbnailCacheRecord existing)
            && existing != null
            && existing != s_generating
            && existing != s_failed)
            return;

        s_cache[request] = s_generating;
    }

    private static void ClearPendingGenerationState()
    {
        s_queued.Clear();
        s_generateQueue.Clear();
        s_prefetchQueued.Clear();
        s_prefetchQueue.Clear();

        List<ThumbnailRequest> toRemove = new List<ThumbnailRequest>();
        foreach (KeyValuePair<ThumbnailRequest, ThumbnailCacheRecord> kv in s_cache)
        {
            if (kv.Value == s_generating)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            RemoveCacheEntry(toRemove[i]);
    }

    private static bool TryGetValidCache(ThumbnailRequest request, out ThumbnailCacheRecord record)
    {
        if (!s_cache.TryGetValue(request, out record))
        {
            long dependencyHash = GetDependencyHash(request.AssetPath);
            if (!ThumbnailPersistentCacheUtility.TryLoadRecord(request, dependencyHash, out record))
                return false;

            StoreCacheRecord(request, record, persistToDisk: false);
        }

        if (record == s_generating || record == s_failed)
            return true;

        if (record.AssetPath != request.AssetPath)
        {
            RemoveCacheEntry(request);
            record = null;
            return false;
        }

        // Promote to front of LRU
        if (s_cacheLruNodes.TryGetValue(request, out LinkedListNode<ThumbnailRequest> node))
        {
            s_cacheLruOrder.Remove(node);
            s_cacheLruOrder.AddFirst(node);
        }

        return true;
    }

    private static ThumbnailProviderBase GetProviderForAsset(string guid, string assetPath, out ThumbnailSupportInfo supportInfo)
    {
        if (s_supportCache.TryGetValue(guid, out SupportCacheEntry cached)
            && cached.assetPath == assetPath)
        {
            supportInfo = cached.supportInfo;
            return cached.provider;
        }

        ThumbnailProviderBase provider = ThumbnailProviderRegistry.FindBestProvider(guid, assetPath, out supportInfo);
        s_supportCache[guid] = new SupportCacheEntry
        {
            assetPath = assetPath,
            dependencyHash = 0,
            supportInfo = supportInfo,
            provider = provider,
        };
        return provider;
    }

    private static ThumbnailRequest BuildRequest(string guid, string assetPath, ThumbnailSupportInfo supportInfo, ThumbnailSurface surface)
        => new ThumbnailRequest(guid, assetPath, supportInfo.AssetKind, surface);

    private static bool IsProjectWindowSearchActive()
    {
        int frameCount = Time.frameCount;
        if (s_lastSearchModeFrame == frameCount)
            return s_cachedSearchModeResult;

        s_lastSearchModeFrame = frameCount;
        s_cachedSearchModeResult = EditorCompatibilityUtility.IsProjectWindowSearchActive();
        return s_cachedSearchModeResult;
    }

    private static bool IsProjectWindowBroadResultsMode()
    {
        if (IsProjectWindowSearchActive())
            return true;

        return HasMultipleFoldersSelectedInProjectWindow();
    }

    private static bool HasMultipleFoldersSelectedInProjectWindow()
    {
        if (!EditorCompatibilityUtility.TryGetProjectWindowFolderScopes(out string[] folders) || folders == null)
            return false;

        int folderCount = 0;
        for (int i = 0; i < folders.Length; i++)
        {
            string folderPath = folders[i];
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                continue;

            folderCount++;
            if (folderCount > 1)
                return true;
        }

        return false;
    }

    private static void PrewarmSearchResultsIfNeeded(ThumbnailSurface surface)
    {
        if (!EditorCompatibilityUtility.TryGetProjectWindowSearchCriteria(out string nameFilter, out string[] classNames, out string[] assetLabels))
        {
            s_lastSearchPrefetchSignature = string.Empty;
            return;
        }

        string signature = BuildSearchPrefetchSignature(surface, nameFilter, classNames, assetLabels);
        if (signature == s_lastSearchPrefetchSignature)
            return;

        s_lastSearchPrefetchSignature = signature;
        QueueSearchPrefabs(surface, nameFilter, classNames, assetLabels);
    }

    private static string BuildSearchPrefetchSignature(ThumbnailSurface surface, string nameFilter, string[] classNames, string[] assetLabels)
    {
        string classes = classNames == null || classNames.Length == 0
            ? string.Empty
            : string.Join("|", classNames);
        string labels = assetLabels == null || assetLabels.Length == 0
            ? string.Empty
            : string.Join("|", assetLabels);

        return $"{surface}|{nameFilter}|{classes}|{labels}";
    }

    private static void QueueSearchPrefabs(ThumbnailSurface surface, string nameFilter, string[] classNames, string[] assetLabels)
    {
        string[] assetGuids = FindSearchPrefabGuids(nameFilter, classNames, assetLabels);
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string guid = assetGuids[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
            if (provider == null || !supportInfo.Supported)
                continue;

            ThumbnailRequest request = BuildPrefetchRequest(guid, assetPath, supportInfo, surface);
            if (!TryGetValidCache(request, out _))
                SchedulePrefetch(request);
        }
    }

    private static ThumbnailRequest BuildPrefetchRequest(string guid, string assetPath, ThumbnailSupportInfo supportInfo, ThumbnailSurface surface)
        => BuildRequest(guid, assetPath, supportInfo, surface);

    private static string[] FindSearchPrefabGuids(string nameFilter, string[] classNames, string[] assetLabels)
    {
        HashSet<string> assetGuids = new HashSet<string>();

        bool hasTypeTerms = HasAnyClassNames(classNames);
        if (hasTypeTerms)
        {
            AppendAssetSearchResults(assetGuids, BuildSearchPrefabQuery(nameFilter, classNames, assetLabels, useClassNames: true));
            if (assetGuids.Count > 0 || !ContainsPrefabTypeToken(classNames))
                return new List<string>(assetGuids).ToArray();
        }

        AppendAssetSearchResults(assetGuids, BuildSearchQueryForType(nameFilter, assetLabels, "GameObject"));
        AppendAssetSearchResults(assetGuids, BuildSearchQueryForType(nameFilter, assetLabels, "VisualEffectAsset"));
        AppendAssetSearchResults(assetGuids, BuildSearchQueryForType(nameFilter, assetLabels, "Material"));
        return new List<string>(assetGuids).ToArray();
    }

    private static string BuildSearchPrefabQuery(string nameFilter, string[] classNames, string[] assetLabels, bool useClassNames)
    {
        List<string> terms = new List<string>();

        if (!string.IsNullOrWhiteSpace(nameFilter))
            terms.Add(nameFilter);

        if (assetLabels != null)
        {
            for (int i = 0; i < assetLabels.Length; i++)
            {
                string label = assetLabels[i];
                if (!string.IsNullOrWhiteSpace(label))
                    terms.Add($"l:{label}");
            }
        }

        bool addedTypeTerm = false;
        if (useClassNames && classNames != null)
        {
            for (int i = 0; i < classNames.Length; i++)
            {
                string className = classNames[i];
                if (string.IsNullOrWhiteSpace(className))
                    continue;

                terms.Add($"t:{className}");
                addedTypeTerm = true;
            }
        }

        if (!addedTypeTerm)
            terms.Add("t:GameObject");

        return string.Join(" ", terms);
    }

    private static string BuildSearchQueryForType(string nameFilter, string[] assetLabels, string typeName)
    {
        List<string> terms = new List<string>();

        if (!string.IsNullOrWhiteSpace(nameFilter))
            terms.Add(nameFilter);

        if (assetLabels != null)
        {
            for (int i = 0; i < assetLabels.Length; i++)
            {
                string label = assetLabels[i];
                if (!string.IsNullOrWhiteSpace(label))
                    terms.Add($"l:{label}");
            }
        }

        if (!string.IsNullOrWhiteSpace(typeName))
            terms.Add($"t:{typeName}");

        return string.Join(" ", terms);
    }

    private static void AppendAssetSearchResults(HashSet<string> destination, string query, string[] searchInFolders = null)
    {
        if (destination == null || string.IsNullOrWhiteSpace(query))
            return;

        string[] guids = searchInFolders == null
            ? AssetDatabase.FindAssets(query)
            : AssetDatabase.FindAssets(query, searchInFolders);

        for (int i = 0; i < guids.Length; i++)
            destination.Add(guids[i]);
    }

    private static bool HasAnyClassNames(string[] classNames)
    {
        if (classNames == null)
            return false;

        for (int i = 0; i < classNames.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(classNames[i]))
                return true;
        }

        return false;
    }

    private static bool ContainsPrefabTypeToken(string[] classNames)
    {
        if (classNames == null)
            return false;

        for (int i = 0; i < classNames.Length; i++)
        {
            string className = classNames[i];
            if (string.IsNullOrWhiteSpace(className))
                continue;

            if (string.Equals(className, "Prefab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "GameObject", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void PrewarmFolderContentsIfNeeded(string assetPath, ThumbnailSurface surface)
    {
        if (string.IsNullOrEmpty(assetPath) || IsProjectWindowBroadResultsMode())
            return;

        int frameCount = Time.frameCount;
        if (s_lastFolderPrefetchFrame != frameCount)
        {
            s_lastFolderPrefetchFrame = frameCount;
            s_foldersThisRepaint.Clear();
        }

        string folder = GetFolderFromPath(assetPath);
        string key = $"{(int)surface}|{folder}";
        if (!s_foldersThisRepaint.Add(key))
            return;

        string[] assetGuids = FindCandidateAssetGuids(new[] { folder });
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string guid = assetGuids[i];
            string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(candidatePath))
                continue;

            ThumbnailProviderBase provider = GetProviderForAsset(guid, candidatePath, out ThumbnailSupportInfo supportInfo);
            if (provider == null || !supportInfo.Supported)
                continue;

            ThumbnailRequest request = BuildPrefetchRequest(guid, candidatePath, supportInfo, surface);
            if (!TryGetValidCache(request, out _))
                SchedulePrefetch(request);
        }
    }

    private static void SchedulePrefetch(ThumbnailRequest request)
    {
        if (s_queued.Contains(request) || s_prefetchQueued.Contains(request))
            return;

        s_prefetchQueued.Add(request);
        s_prefetchQueue.Enqueue(request);
        EnsureTickRegistered();
    }

    private static List<ThumbnailRequest> CollectWarmupRequests()
    {
        List<ThumbnailRequest> requests = new List<ThumbnailRequest>();
        ThumbnailSurface[] surfaces = GetWarmupSurfaces();
        string[] assetGuids = FindCandidateAssetGuids();

        for (int i = 0; i < assetGuids.Length; i++)
        {
            string guid = assetGuids[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            ThumbnailProviderBase provider = GetProviderForAsset(guid, assetPath, out ThumbnailSupportInfo supportInfo);
            if (provider == null || !supportInfo.Supported)
                continue;

            for (int surfaceIndex = 0; surfaceIndex < surfaces.Length; surfaceIndex++)
                requests.Add(BuildWarmupRequest(guid, assetPath, supportInfo, surfaces[surfaceIndex]));
        }

        return requests;
    }

    private static string[] FindCandidateAssetGuids(string[] searchFolders = null)
    {
        HashSet<string> guids = new HashSet<string>();
        AppendAssetSearchResults(guids, "t:GameObject", searchFolders);
        AppendAssetSearchResults(guids, "t:VisualEffectAsset", searchFolders);
        AppendAssetSearchResults(guids, "t:Material", searchFolders);
        return new List<string>(guids).ToArray();
    }

    private static ThumbnailRequest BuildWarmupRequest(string guid, string assetPath, ThumbnailSupportInfo supportInfo, ThumbnailSurface surface)
    {
        return new ThumbnailRequest(guid, assetPath, supportInfo.AssetKind, surface);
    }

    private static ThumbnailSurface[] GetWarmupSurfaces()
    {
        List<ThumbnailSurface> surfaces = new List<ThumbnailSurface>();
        if (ImprovedThumbnailSettings.ThumbnailDrawInProjectGrid)
            surfaces.Add(ThumbnailSurface.ProjectWindowGrid);
        if (ImprovedThumbnailSettings.ThumbnailDrawInProjectList)
            surfaces.Add(ThumbnailSurface.ProjectWindowList);

        if (surfaces.Count == 0)
            surfaces.Add(ThumbnailSurface.ProjectWindowGrid);

        return surfaces.ToArray();
    }

    private static bool IsItemVisibleInProjectWindow(Rect selectionRect)
    {
        int frameCount = Time.frameCount;
        if (s_lastBrowserHeightFrame != frameCount)
        {
            s_lastBrowserHeightFrame = frameCount;
            if (!EditorCompatibilityUtility.TryGetProjectBrowserClientHeight(out s_cachedBrowserHeight))
                s_cachedBrowserHeight = 0f;
        }

        if (s_cachedBrowserHeight <= 0f)
            return true; // Fail open — can't determine, allow enqueue

        // selectionRect is in the project window's GUI coordinate space (0 = top of client area).
        // Items scrolled above have yMax < 0; items scrolled below have y > window height.
        return selectionRect.yMax >= 0f && selectionRect.y <= s_cachedBrowserHeight;
    }

    private static bool IsInObjectSelectorContext()
    {
        EditorWindow focused = EditorWindow.focusedWindow;
        if (focused != null && EditorCompatibilityUtility.IsObjectSelectorWindow(focused))
            return true;
        EditorWindow hovered = EditorWindow.mouseOverWindow;
        return hovered != null && EditorCompatibilityUtility.IsObjectSelectorWindow(hovered);
    }

    private static ThumbnailSurface GetSurface(Rect selectionRect)
    {
        // Geometry is the ground truth for every draw call. The reflection-based cache
        // (TryGetProjectBrowserListModeCached) is keyed by Time.frameCount, which does
        // NOT increment between GUI repaints — only between EditorApplication.update
        // cycles. Dragging the zoom slider triggers new repaints within the same frame,
        // so the cached mode lags one repaint behind and produces the wrong surface.
        //
        // Reliable geometric rules:
        //   - Grid cells:  Unity adds a ~16 px label row below the thumbnail, so
        //                  selectionRect is portrait (width < height). Any rect where
        //                  width < height * 1.2 is unambiguously a grid cell.
        //   - List rows:   rows span the full column width, so width >> height.
        //                  width > height * 2 is unambiguously a list row.
        //
        // Only the narrow band between those two thresholds is genuinely ambiguous and
        // needs the slower reflection-based lookup.

        if (selectionRect.width > selectionRect.height * 2f)
            return ThumbnailSurface.ProjectWindowList;

        if (selectionRect.width < selectionRect.height * 1.2f)
            return ThumbnailSurface.ProjectWindowGrid;

        // Ambiguous aspect ratio — fall back to reflection-based detection.
        // Skip when the ObjectSelector is active: it reads from the background
        // ProjectBrowser and would return the wrong surface for the selector layout.
        if (!IsInObjectSelectorContext() &&
            EditorCompatibilityUtility.TryGetProjectBrowserListModeCached(out bool isListMode))
            return isListMode ? ThumbnailSurface.ProjectWindowList : ThumbnailSurface.ProjectWindowGrid;

        return selectionRect.height <= 24f
            ? ThumbnailSurface.ProjectWindowList
            : ThumbnailSurface.ProjectWindowGrid;
    }

    private static bool ShouldDrawOnSurface(ThumbnailSurface surface)
    {
        switch (surface)
        {
            case ThumbnailSurface.ProjectWindowList:
                return ImprovedThumbnailSettings.ThumbnailDrawInProjectList;
            default:
                return ImprovedThumbnailSettings.ThumbnailDrawInProjectGrid;
        }
    }

    private static bool ShouldDrawGridSelectionFrameForGuid(ThumbnailSurface surface, string guid)
    {
        if (surface != ThumbnailSurface.ProjectWindowGrid)
            return false;

        if (!ImprovedThumbnailSettings.ShowSelectionFrame)
            return false;

        return IsGuidSelectedInProjectWindow(guid);
    }

    private static bool IsGuidSelectedInProjectWindow(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return false;

        if (!string.IsNullOrEmpty(s_singleSelectedAssetGuid))
            return string.Equals(s_singleSelectedAssetGuid, guid, StringComparison.OrdinalIgnoreCase);

        return s_selectedAssetGuids.Contains(guid);
    }

    private static void StoreCacheRecord(ThumbnailRequest request, ThumbnailCacheRecord record, bool persistToDisk = true)
    {
        ClearRetryState(request);

        if (s_cache.TryGetValue(request, out ThumbnailCacheRecord existingRecord)
            && existingRecord != null
            && existingRecord != s_generating
            && existingRecord != s_failed
            && !ReferenceEquals(existingRecord, record))
        {
            DestroyRecordTextures(existingRecord);
        }

        // Evict based on true cacheable thumbnail entries only (LRU list), not transient
        // marker entries like s_generating/s_failed. Counting marker entries here causes
        // aggressive churn during Object Picker queue bursts and forces re-generation.
        int maxSize = Mathf.Max(10, ImprovedThumbnailSettings.ThumbnailCacheMaxSize);
        bool isCacheableRecord = record != s_generating && record != s_failed;
        bool requestAlreadyInLru = s_cacheLruNodes.ContainsKey(request);
        int incomingLruCount = s_cacheLruOrder.Count + (isCacheableRecord && !requestAlreadyInLru ? 1 : 0);

        while (incomingLruCount > maxSize && s_cacheLruOrder.Count > 0)
        {
            LinkedListNode<ThumbnailRequest> tail = s_cacheLruOrder.Last;
            ThumbnailRequest tailRequest = tail.Value;
            RemoveCacheEntry(tailRequest);
            incomingLruCount = s_cacheLruOrder.Count + (isCacheableRecord && !requestAlreadyInLru ? 1 : 0);
        }

        s_cache[request] = record;

        if (record != s_generating && record != s_failed)
        {
            if (s_cacheLruNodes.TryGetValue(request, out LinkedListNode<ThumbnailRequest> existing))
            {
                s_cacheLruOrder.Remove(existing);
                s_cacheLruOrder.AddFirst(existing);
            }
            else
            {
                LinkedListNode<ThumbnailRequest> node = s_cacheLruOrder.AddFirst(request);
                s_cacheLruNodes[request] = node;
            }

            if (persistToDisk)
                ThumbnailPersistentCacheUtility.SaveRecord(request, record);
        }
    }

    private static void RemoveCacheEntry(ThumbnailRequest request)
    {
        s_queued.Remove(request);
        s_prefetchQueued.Remove(request);
        s_retryPending.Remove(request);
        s_retryCounts.Remove(request);
        if (!s_cache.TryGetValue(request, out ThumbnailCacheRecord record))
            return;

        DestroyRecordTextures(record);
        s_cache.Remove(request);

        if (s_cacheLruNodes.TryGetValue(request, out LinkedListNode<ThumbnailRequest> node))
        {
            s_cacheLruOrder.Remove(node);
            s_cacheLruNodes.Remove(request);
        }
    }

    private static void DestroyRecordTextures(ThumbnailCacheRecord record)
    {
        if (record == null || record == s_generating || record == s_failed)
            return;

        ThumbnailCacheUtility.DestroyRecordTextures(record);
    }

    private static void ClearRetryState(ThumbnailRequest request)
    {
        s_retryPending.Remove(request);
        s_retryCounts.Remove(request);
    }

    private static Texture GetCurrentTexture(ThumbnailCacheRecord record)
    {
        if (record == null || record == s_generating || record == s_failed || record.Frames == null)
            return null;

        return record.Frames.StaticFrame;
    }

    private static ThumbnailBadgeType GetBadgeType(ThumbnailAssetKind assetKind)
    {
        switch (assetKind)
        {
            case ThumbnailAssetKind.ParticlePrefab:
                return ThumbnailBadgeType.ParticlePrefab;
            case ThumbnailAssetKind.SpritePrefab:
                return ThumbnailBadgeType.SpritePrefab;
            case ThumbnailAssetKind.TmpUiPrefab:
                return ThumbnailBadgeType.TmpPrefab;
            case ThumbnailAssetKind.UiPrefab:
                return ThumbnailBadgeType.UiPrefab;
            case ThumbnailAssetKind.ModelAsset:
                return ThumbnailBadgeType.ModelPrefab;
            case ThumbnailAssetKind.TextureAsset:
                return ThumbnailBadgeType.TextureAsset;
            case ThumbnailAssetKind.MaterialAsset:
                return ThumbnailBadgeType.MaterialAsset;
            case ThumbnailAssetKind.PrefabVariant:
                return ThumbnailBadgeType.PrefabVariant;
            default:
                return ThumbnailBadgeType.GeneralPrefab;
        }
    }

    private static bool TryGetOverlayOnlyBadge(string assetPath, out ThumbnailBadgeType badgeType)
    {
        badgeType = default;
        if (!IsLikelyOverlayBadgeTexturePath(assetPath))
            return false;

        System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (assetType == null)
            return false;

        if (typeof(Texture).IsAssignableFrom(assetType))
        {
            if (typeof(Cubemap).IsAssignableFrom(assetType))
            {
                badgeType = ThumbnailBadgeType.CubemapAsset;
                return true;
            }

            badgeType = ThumbnailBadgeType.TextureAsset;
            return true;
        }

        return false;
    }

    private static bool TryGetCachedOverlayBadge(string guid, string assetPath, out ThumbnailBadgeType badgeType)
    {
        badgeType = default;
        if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
            return false;

        if (!s_projectWindowItemCache.TryGetValue(guid, out ProjectWindowItemCacheEntry entry)
            || !string.Equals(entry.assetPath, assetPath, StringComparison.Ordinal))
        {
            return TryGetOverlayOnlyBadge(assetPath, out badgeType);
        }

        if (entry.overlayBadgeResolved)
        {
            if (!entry.hasOverlayBadge)
                return false;

            badgeType = entry.overlayBadgeType;
            return true;
        }

        bool hasBadge = TryGetOverlayOnlyBadge(assetPath, out ThumbnailBadgeType resolvedBadgeType);
        entry.overlayBadgeResolved = true;
        entry.hasOverlayBadge = hasBadge;
        entry.overlayBadgeType = resolvedBadgeType;
        s_projectWindowItemCache[guid] = entry;

        if (!hasBadge)
            return false;

        badgeType = resolvedBadgeType;
        return true;
    }

    private static bool IsLikelyOverlayBadgeTexturePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        for (int i = 0; i < s_overlayBadgeTextureExtensions.Length; i++)
        {
            if (assetPath.EndsWith(s_overlayBadgeTextureExtensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryGetCachedOverlayUiInfo(
        string guid,
        string assetPath,
        out bool hasExpandableSubAssets,
        out int mainAssetInstanceId)
    {
        hasExpandableSubAssets = false;
        mainAssetInstanceId = 0;
        if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
            return false;

        if (!s_projectWindowItemCache.TryGetValue(guid, out ProjectWindowItemCacheEntry entry)
            || !string.Equals(entry.assetPath, assetPath, StringComparison.Ordinal))
        {
            return false;
        }

        if (entry.overlayUiInfoResolved)
        {
            hasExpandableSubAssets = entry.overlayHasExpandableSubAssets;
            mainAssetInstanceId = entry.overlayMainAssetInstanceId;
            return true;
        }

        bool hasExpandable = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).Length > 0;
        int mainId = 0;
        if (hasExpandable)
        {
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null)
                mainId = mainAsset.GetInstanceID();
        }

        entry.overlayUiInfoResolved = true;
        entry.overlayHasExpandableSubAssets = hasExpandable;
        entry.overlayMainAssetInstanceId = mainId;
        s_projectWindowItemCache[guid] = entry;

        hasExpandableSubAssets = hasExpandable;
        mainAssetInstanceId = mainId;
        return true;
    }

    private static bool TryGetProjectWindowItemInfo(
        string guid,
        out string assetPath,
        out ProjectWindowItemKind itemKind)
    {
        assetPath = string.Empty;
        itemKind = ProjectWindowItemKind.Skip;

        if (string.IsNullOrEmpty(guid))
            return false;

        if (s_projectWindowItemCache.TryGetValue(guid, out ProjectWindowItemCacheEntry cached))
        {
            assetPath = cached.assetPath;
            itemKind = cached.kind;
            return itemKind != ProjectWindowItemKind.Skip && !string.IsNullOrEmpty(assetPath);
        }

        assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string mainAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.Equals(guid, mainAssetGuid, StringComparison.OrdinalIgnoreCase))
        {
            s_projectWindowItemCache[guid] = new ProjectWindowItemCacheEntry
            {
                assetPath = assetPath,
                kind = ProjectWindowItemKind.Skip,
                overlayBadgeResolved = true,
                hasOverlayBadge = false,
                overlayUiInfoResolved = true,
                overlayHasExpandableSubAssets = false,
                overlayMainAssetInstanceId = 0,
            };
            return false;
        }

        if (ThumbnailAssetPathUtility.IsThumbnailSourceAssetPath(assetPath))
        {
            itemKind = ProjectWindowItemKind.ThumbnailSource;
        }
        else if (IsLikelyOverlayBadgeTexturePath(assetPath))
        {
            itemKind = ProjectWindowItemKind.OverlayCandidate;
        }
        else
        {
            itemKind = ProjectWindowItemKind.Skip;
        }

        s_projectWindowItemCache[guid] = new ProjectWindowItemCacheEntry
        {
            assetPath = assetPath,
            kind = itemKind,
            overlayBadgeResolved = false,
            hasOverlayBadge = false,
            overlayUiInfoResolved = false,
            overlayHasExpandableSubAssets = false,
            overlayMainAssetInstanceId = 0,
        };

        return itemKind != ProjectWindowItemKind.Skip;
    }

    private static AssetUiInfoCacheEntry GetAssetUiInfo(string guid, string assetPath)
    {
        if (s_assetUiInfoCache.TryGetValue(guid, out AssetUiInfoCacheEntry cached)
            && cached.assetPath == assetPath)
        {
            return cached;
        }

        int mainAssetInstanceId = 0;
        if (!string.IsNullOrEmpty(assetPath))
        {
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null)
                mainAssetInstanceId = mainAsset.GetInstanceID();
        }

        AssetUiInfoCacheEntry entry = new AssetUiInfoCacheEntry
        {
            assetPath = assetPath,
            dependencyHash = 0,
            hasExpandableSubAssets = !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).Length > 0,
            mainAssetInstanceId = mainAssetInstanceId,
        };

        s_assetUiInfoCache[guid] = entry;
        return entry;
    }

    private static void BeginPrimaryRowTrackingFrame()
    {
        int frameCount = Time.frameCount;
        if (s_lastPrimaryRowTrackingFrame == frameCount)
            return;

        s_lastPrimaryRowTrackingFrame = frameCount;
    }

    private static bool ShouldSuppressSubAssetRow(
        string guid,
        Rect selectionRect,
        ThumbnailSurface surface,
        bool hasExpandableSubAssets,
        bool isMainAssetExpanded,
        out bool isSubAssetRow)
    {
        isSubAssetRow = false;
        bool shouldTrackSubAssetRows = hasExpandableSubAssets
            && (isMainAssetExpanded || surface == ThumbnailSurface.ProjectWindowGrid);
        if (!shouldTrackSubAssetRows)
            return false;

        RowTrackingKey trackingKey = BuildPrimaryRowTrackingKey(guid, surface, selectionRect);
        isSubAssetRow = IsSubAssetRowForKey(trackingKey, selectionRect.x);
        if (!isSubAssetRow)
            return false;

        return true;
    }

    private static RowTrackingKey BuildPrimaryRowTrackingKey(string guid, ThumbnailSurface surface, Rect selectionRect)
    {
        int widthBucket = Mathf.RoundToInt(selectionRect.width);
        int heightBucket = Mathf.RoundToInt(selectionRect.height);
        return new RowTrackingKey(guid, surface, widthBucket, heightBucket);
    }

    private static bool IsSubAssetRowForKey(RowTrackingKey trackingKey, float rowX)
    {
        if (string.IsNullOrEmpty(trackingKey.guid))
            return false;

        int frameCount = Time.frameCount;
        if (!s_primaryRowStateByKey.TryGetValue(trackingKey, out RowTrackingState state))
        {
            s_primaryRowStateByKey[trackingKey] = new RowTrackingState
            {
                minPrimaryX = rowX,
                lastSeenFrame = frameCount,
            };
            return false;
        }

        // If this key hasn't been seen in a while, treat the next seen row as primary
        // so layout/view changes don't keep stale X anchors forever.
        if (frameCount - state.lastSeenFrame > 120)
        {
            state.minPrimaryX = rowX;
            state.lastSeenFrame = frameCount;
            s_primaryRowStateByKey[trackingKey] = state;
            return false;
        }

        // If we discover a row with a smaller X, treat it as the new primary row.
        if (rowX < state.minPrimaryX - 0.5f)
        {
            state.minPrimaryX = rowX;
            state.lastSeenFrame = frameCount;
            s_primaryRowStateByKey[trackingKey] = state;
            return false;
        }

        state.lastSeenFrame = frameCount;
        s_primaryRowStateByKey[trackingKey] = state;
        return rowX > state.minPrimaryX + 0.5f;
    }

    private static bool IsMainAssetExpanded(int mainAssetInstanceId)
    {
        if (mainAssetInstanceId == 0)
            return false;

        return s_expandedProjectWindowItemIds.Contains(mainAssetInstanceId);
    }

    private static void PollExpandedProjectWindowState()
    {
        int frameCount = Time.frameCount;
        if (s_lastExpandedStatePollFrame == frameCount)
            return;

        s_lastExpandedStatePollFrame = frameCount;
        s_expandedProjectWindowItemIds.Clear();
        EditorCompatibilityUtility.GetExpandedProjectWindowItemInstanceIds(s_expandedProjectWindowItemIds);

        bool expansionStateChanged = !s_expandedProjectWindowItemIds.SetEquals(s_lastExpandedProjectWindowItemIds);
        if (!expansionStateChanged)
            return;

        s_lastExpandedProjectWindowItemIds.Clear();
        foreach (int id in s_expandedProjectWindowItemIds)
            s_lastExpandedProjectWindowItemIds.Add(id);

        ResetPrimaryRowTracking();
        ScheduleRepaintAfterExpansionChange();
    }

    private static void ResetPrimaryRowTracking()
    {
        s_primaryRowStateByKey.Clear();
        s_lastPrimaryRowTrackingFrame = -1;
    }

    private static void ScheduleRepaintAfterExpansionChange()
    {
        if (s_expansionRepaintScheduled)
            return;

        s_expansionRepaintScheduled = true;
        EditorApplication.delayCall += RepaintProjectWindowAfterExpansionChange;
    }

    private static void RepaintProjectWindowAfterExpansionChange()
    {
        EditorApplication.delayCall -= RepaintProjectWindowAfterExpansionChange;
        s_expansionRepaintScheduled = false;

        if (!ImprovedThumbnailSettings.Active)
            return;

        EditorApplication.RepaintProjectWindow();
    }

    private static string GetFolderFromPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return string.Empty;

        int slash = assetPath.LastIndexOf('/');
        return slash >= 0 ? assetPath.Substring(0, slash) : "Assets";
    }

    private static long GetDependencyHash(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return 0;

        try
        {
            return AssetDatabase.GetAssetDependencyHash(assetPath).GetHashCode();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to compute dependency hash for '{assetPath}': {e.Message}");
            return 0;
        }
    }

    private static void DrawLoadingSpinner(Rect selectionRect, ThumbnailSurface surface, bool reserveExpandArrow)
    {
        Rect contentRect = ThumbnailDrawingUtility.GetContentRect(selectionRect, surface, reserveExpandArrow);
        EditorGUI.DrawRect(contentRect, ImprovedThumbnailSettings.ThumbnailBackgroundColor);

        int spinIndex = (int)(EditorApplication.timeSinceStartup * 12) % 12;
        string spinIconName = $"WaitSpin{spinIndex:D2}";
        GUIContent spinIcon = EditorGUIUtility.IconContent(spinIconName);
        if (spinIcon?.image != null)
        {
            float iconSize = Mathf.Min(contentRect.width, contentRect.height) * 0.4f;
            iconSize = Mathf.Clamp(iconSize, 12f, 32f);
            Rect iconRect = new Rect(
                contentRect.x + (contentRect.width - iconSize) * 0.5f,
                contentRect.y + (contentRect.height - iconSize) * 0.5f,
                iconSize,
                iconSize);
            GUI.DrawTexture(iconRect, spinIcon.image, ScaleMode.ScaleToFit);
        }
    }

    private static void DrawGridSelectionFrame(Rect selectionRect, bool reserveExpandArrow, bool useDefaultScaleRect)
    {
        Rect contentRect = ThumbnailDrawingUtility.GetContentRect(
            selectionRect,
            ThumbnailSurface.ProjectWindowGrid,
            reserveExpandArrow && !useDefaultScaleRect);
        if (contentRect.width <= 0f || contentRect.height <= 0f)
            return;

        DrawSelectionFrameBorder(
            contentRect,
            ImprovedThumbnailSettings.SelectionFrameColor,
            SelectionFrameBorderThickness,
            reserveExpandArrow);
    }

    private static void DrawSelectionFrameBorder(Rect rect, Color color, float thickness, bool reserveExpandArrow)
    {
        float borderThickness = Mathf.Max(1f, Mathf.Round(thickness));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, borderThickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - borderThickness, rect.width, borderThickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, borderThickness, rect.height), color);

        bool useNotch = reserveExpandArrow
            && rect.width > SelectionFrameNotchCutoutWidth
            && rect.height > borderThickness * 2f;
        if (!useNotch)
        {
            EditorGUI.DrawRect(new Rect(rect.xMax - borderThickness, rect.y, borderThickness, rect.height), color);
            return;
        }

        float maxGapHeight = rect.height - borderThickness * 2f;
        float gapHeight = Mathf.Round(Mathf.Min(SelectionFrameNotchGapHeight, maxGapHeight));
        if (gapHeight <= 0f)
        {
            EditorGUI.DrawRect(new Rect(rect.xMax - borderThickness, rect.y, borderThickness, rect.height), color);
            return;
        }

        float minGapY = rect.y + borderThickness;
        float maxGapY = rect.yMax - borderThickness - gapHeight;
        float gapY = rect.y + (rect.height - gapHeight) * 0.5f + SelectionFrameNotchGapOffset;
        gapY = Mathf.Round(Mathf.Clamp(gapY, minGapY, maxGapY));

        float topHeight = gapY - rect.y;
        if (topHeight > 0f)
            EditorGUI.DrawRect(new Rect(rect.xMax - borderThickness, rect.y, borderThickness, topHeight), color);

        float bottomY = gapY + gapHeight;
        float bottomHeight = rect.yMax - bottomY;
        if (bottomHeight > 0f)
            EditorGUI.DrawRect(new Rect(rect.xMax - borderThickness, bottomY, borderThickness, bottomHeight), color);
    }

    private static void RefreshSelectedAssetGuidCache()
    {
        s_singleSelectedAssetGuid = string.Empty;
        s_selectedAssetGuids.Clear();

        string[] selectedAssetGuids = Selection.assetGUIDs;
        if (selectedAssetGuids == null || selectedAssetGuids.Length == 0)
            return;

        if (selectedAssetGuids.Length == 1)
        {
            s_singleSelectedAssetGuid = selectedAssetGuids[0] ?? string.Empty;
            return;
        }

        for (int i = 0; i < selectedAssetGuids.Length; i++)
        {
            string selectedGuid = selectedAssetGuids[i];
            if (!string.IsNullOrEmpty(selectedGuid))
                s_selectedAssetGuids.Add(selectedGuid);
        }
    }

    private static void OnSelectionChanged()
    {
        RefreshSelectedAssetGuidCache();
        if (ImprovedThumbnailSettings.Active
            && ImprovedThumbnailSettings.ShowSelectionFrame
            && ImprovedThumbnailSettings.ThumbnailDrawInProjectGrid)
        {
            EditorApplication.RepaintProjectWindow();
        }
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

    public static CacheStats GetCacheStats()
    {
        CacheStats stats = new CacheStats
        {
            TotalEntries = s_cache.Count,
            PersistentEntryCount = ThumbnailPersistentCacheUtility.GetCachedFileCount(),
            QueueDepth = s_generateQueue.Count + s_prefetchQueue.Count,
            DiskCacheBytes = ThumbnailPersistentCacheUtility.GetCachedDiskBytes(),
        };

        foreach (ThumbnailCacheRecord record in s_cache.Values)
        {
            if (record == s_generating) stats.GeneratingCount++;
            else if (record == s_failed) stats.FailedCount++;
            else if (record?.Frames?.StaticFrame is Texture2D tex)
                stats.MemoryCacheBytes += (long)tex.width * tex.height * 4;
        }

        return stats;
    }

    private static void LogVerbose(string message)
    {
        if (ImprovedThumbnailSettings.VerboseLogging)
            Debug.Log($"[ImprovedThumbnail] {message}");
    }

    private static void CleanupAll()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupAll;
        EditorApplication.quitting -= CleanupAll;
        Selection.selectionChanged -= OnSelectionChanged;
        StopTicking();
        ClearInMemoryCaches();
        s_renderContext.Dispose();
        s_foldersThisRepaint.Clear();
    }

    private static void StopTicking()
    {
        if (!s_tickRegistered)
            return;

        EditorApplication.update -= OnEditorUpdate;
        s_tickRegistered = false;
    }
}

}
