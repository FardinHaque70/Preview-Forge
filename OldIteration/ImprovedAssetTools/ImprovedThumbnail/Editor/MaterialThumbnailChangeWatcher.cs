using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FardinHaque.ImprovedAssetTools.Editor
{

[InitializeOnLoad]
internal static class MaterialThumbnailChangeWatcher
{
    private const double FlushDelaySeconds = 0.2;
    private const double ScanIntervalSeconds = 0.33;

    private static readonly HashSet<string> s_pendingMaterialPaths = new();
    private static readonly Dictionary<string, int> s_materialHashByPath = new();
    private static readonly List<Material> s_selectedMaterials = new();
    private static readonly HashSet<string> s_seenPathsThisScan = new();
    private static readonly List<string> s_pathsToRemove = new();
    private static readonly MethodInfo s_computeCrcMethod = typeof(Material).GetMethod(
        "ComputeCRC",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        null,
        Type.EmptyTypes,
        null);

    private static double s_nextScanTime;
    private static double s_nextFlushTime;
    private static bool s_updateSubscribed;
    private static bool s_callbacksSubscribed;

    static MaterialThumbnailChangeWatcher()
    {
        RefreshSubscriptions();
    }

    internal static void RefreshSubscriptions()
    {
        bool shouldSubscribeCallbacks = ImprovedThumbnailSettings.Active && ImprovedThumbnailSettings.EnableMaterialThumbnailProvider;
        if (shouldSubscribeCallbacks == s_callbacksSubscribed)
            return;

        if (shouldSubscribeCallbacks)
            SubscribeCallbacks();
        else
            UnsubscribeCallbacks();
    }

    private static void SubscribeCallbacks()
    {
        if (s_callbacksSubscribed)
            return;

        Undo.postprocessModifications += OnPostprocessModifications;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        Selection.selectionChanged += OnSelectionChanged;
        s_callbacksSubscribed = true;

        RebuildSelectedMaterials();
        s_nextScanTime = 0.0;
        UpdateSubscriptionState();
    }

    private static void UnsubscribeCallbacks()
    {
        if (!s_callbacksSubscribed)
            return;

        Undo.postprocessModifications -= OnPostprocessModifications;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Selection.selectionChanged -= OnSelectionChanged;
        s_callbacksSubscribed = false;

        EditorApplication.update -= OnEditorUpdate;
        s_updateSubscribed = false;
        s_pendingMaterialPaths.Clear();
        s_materialHashByPath.Clear();
        s_selectedMaterials.Clear();
        s_seenPathsThisScan.Clear();
        s_pathsToRemove.Clear();
        s_nextScanTime = 0.0;
        s_nextFlushTime = 0.0;
    }

    private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
    {
        if (modifications == null || modifications.Length == 0)
            return modifications;

        if (!ImprovedThumbnailSettings.Active || !ImprovedThumbnailSettings.EnableMaterialThumbnailProvider)
        {
            RebuildSelectedMaterials();
            UpdateSubscriptionState();
            return modifications;
        }

        bool queuedAny = false;
        for (int i = 0; i < modifications.Length; i++)
        {
            Material material = modifications[i].currentValue.target as Material;
            if (material == null)
                continue;

            queuedAny |= QueueMaterialPath(material);
        }

        if (queuedAny)
        {
            ScheduleFlush();
        }

        RebuildSelectedMaterials();
        UpdateSubscriptionState();

        return modifications;
    }

    private static void OnUndoRedoPerformed()
    {
        if (!ImprovedThumbnailSettings.Active || !ImprovedThumbnailSettings.EnableMaterialThumbnailProvider)
            return;

        UObject[] selected = Selection.objects;
        bool queuedAny = false;
        for (int i = 0; i < selected.Length; i++)
        {
            Material material = selected[i] as Material;
            if (material == null)
                continue;

            queuedAny |= QueueMaterialPath(material);
        }

        if (queuedAny)
        {
            ScheduleFlush();
            UpdateSubscriptionState();
        }

        // Ensure undo/redo changes are picked up promptly.
        s_nextScanTime = 0.0;
    }

    private static void OnSelectionChanged()
    {
        if (!s_callbacksSubscribed)
            return;

        RebuildSelectedMaterials();
        // Scan immediately after selection changes so edits are detected quickly.
        s_nextScanTime = 0.0;
        UpdateSubscriptionState();
    }

    private static bool QueueMaterialPath(Material material)
    {
        if (material == null || !EditorUtility.IsPersistent(material))
            return false;

        string assetPath = AssetDatabase.GetAssetPath(material);
        if (!ThumbnailAssetPathUtility.IsMaterialAssetPath(assetPath))
            return false;

        s_materialHashByPath[assetPath] = ComputeMaterialStateHash(material);
        return s_pendingMaterialPaths.Add(assetPath);
    }

    private static void ScheduleFlush()
    {
        s_nextFlushTime = EditorApplication.timeSinceStartup + FlushDelaySeconds;
    }

    private static void OnEditorUpdate()
    {
        if (!s_callbacksSubscribed)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now >= s_nextScanTime)
        {
            s_nextScanTime = now + ScanIntervalSeconds;
            ScanSelectedMaterialsForLiveChanges();
        }

        if (s_pendingMaterialPaths.Count > 0 && now >= s_nextFlushTime)
            FlushPendingPaths();

        UpdateSubscriptionState();
    }

    private static void FlushPendingPaths()
    {
        if (s_pendingMaterialPaths.Count == 0)
            return;

        string[] materialPaths = new string[s_pendingMaterialPaths.Count];
        s_pendingMaterialPaths.CopyTo(materialPaths);
        s_pendingMaterialPaths.Clear();

        for (int i = 0; i < materialPaths.Length; i++)
            PrefabThumbnailService.InvalidatePath(materialPaths[i]);
    }

    private static void RebuildSelectedMaterials()
    {
        s_selectedMaterials.Clear();
        s_seenPathsThisScan.Clear();

        UObject[] selected = Selection.objects;
        for (int i = 0; i < selected.Length; i++)
        {
            Material material = selected[i] as Material;
            if (material == null || !EditorUtility.IsPersistent(material))
                continue;

            string assetPath = AssetDatabase.GetAssetPath(material);
            if (!ThumbnailAssetPathUtility.IsMaterialAssetPath(assetPath))
                continue;

            if (!s_seenPathsThisScan.Add(assetPath))
                continue;

            s_selectedMaterials.Add(material);
            if (!s_materialHashByPath.ContainsKey(assetPath))
                s_materialHashByPath[assetPath] = ComputeMaterialStateHash(material);
        }
    }

    private static void ScanSelectedMaterialsForLiveChanges()
    {
        s_seenPathsThisScan.Clear();

        bool queuedAny = false;
        for (int i = s_selectedMaterials.Count - 1; i >= 0; i--)
        {
            Material selectedMaterial = s_selectedMaterials[i];
            if (selectedMaterial == null || !EditorUtility.IsPersistent(selectedMaterial))
            {
                s_selectedMaterials.RemoveAt(i);
                continue;
            }

            queuedAny |= TrackMaterialIfChanged(selectedMaterial);
        }

        s_pathsToRemove.Clear();
        foreach (KeyValuePair<string, int> entry in s_materialHashByPath)
        {
            if (!s_seenPathsThisScan.Contains(entry.Key))
                s_pathsToRemove.Add(entry.Key);
        }

        for (int i = 0; i < s_pathsToRemove.Count; i++)
            s_materialHashByPath.Remove(s_pathsToRemove[i]);

        if (queuedAny)
            ScheduleFlush();
    }

    private static void UpdateSubscriptionState()
    {
        bool shouldSubscribe = s_callbacksSubscribed &&
                               (s_selectedMaterials.Count > 0 || s_pendingMaterialPaths.Count > 0);
        if (shouldSubscribe == s_updateSubscribed)
            return;

        if (shouldSubscribe)
            EditorApplication.update += OnEditorUpdate;
        else
            EditorApplication.update -= OnEditorUpdate;

        s_updateSubscribed = shouldSubscribe;
    }

    private static bool TrackMaterialIfChanged(Material material)
    {
        if (material == null || !EditorUtility.IsPersistent(material))
            return false;

        string assetPath = AssetDatabase.GetAssetPath(material);
        if (!ThumbnailAssetPathUtility.IsMaterialAssetPath(assetPath))
            return false;

        s_seenPathsThisScan.Add(assetPath);

        int currentHash = ComputeMaterialStateHash(material);
        if (!s_materialHashByPath.TryGetValue(assetPath, out int previousHash))
        {
            // Baseline only: first observation should not force a redraw.
            s_materialHashByPath[assetPath] = currentHash;
            return false;
        }

        if (previousHash == currentHash)
            return false;

        s_materialHashByPath[assetPath] = currentHash;
        return s_pendingMaterialPaths.Add(assetPath);
    }

    private static int ComputeMaterialStateHash(Material material)
    {
        if (material == null)
            return 0;

        if (s_computeCrcMethod != null)
        {
            try
            {
                object result = s_computeCrcMethod.Invoke(material, null);
                if (result is int crc)
                    return crc;
            }
            catch
            {
                // Fallback below.
            }
        }

        // Fallback for editor/runtime versions where ComputeCRC is unavailable.
        return EditorJsonUtility.ToJson(material, false).GetHashCode();
    }
}

}
