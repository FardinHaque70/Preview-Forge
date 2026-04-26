using UnityEditor;

namespace FardinHaque.ImprovedAssetTools.Editor
{

[System.Serializable]
internal enum ThumbnailWelcomeDecision
{
    None = 0,
    Skipped = 1,
    Generated = 2,
}

[FilePath("ProjectSettings/ImprovedAssetTools/ThumbnailWelcomeState.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class ThumbnailWelcomeStateStorage : ScriptableSingleton<ThumbnailWelcomeStateStorage>
{
    [UnityEngine.SerializeField] private bool welcomeShown;
    private const string DecisionKeyPrefix = "FardinHaque.ImprovedThumbnail.WelcomeDecision.";

    public bool WelcomeShown => welcomeShown;

    public ThumbnailWelcomeDecision Decision
    {
        get
        {
            string key = GetDecisionEditorPrefKey();
            ThumbnailWelcomeDecision persisted = (ThumbnailWelcomeDecision)EditorPrefs.GetInt(key, (int)ThumbnailWelcomeDecision.None);

            // Migrate old ScriptableSingleton-only state to persistent skip decision.
            if (persisted == ThumbnailWelcomeDecision.None && welcomeShown)
            {
                persisted = ThumbnailWelcomeDecision.Skipped;
                EditorPrefs.SetInt(key, (int)persisted);
            }

            return persisted;
        }
    }

    public bool HasDecision => Decision != ThumbnailWelcomeDecision.None;

    public void MarkSkipped()
    {
        SetDecision(ThumbnailWelcomeDecision.Skipped);
    }

    public void MarkGenerated()
    {
        SetDecision(ThumbnailWelcomeDecision.Generated);
    }

    public void ResetDecision()
    {
        EditorPrefs.DeleteKey(GetDecisionEditorPrefKey());

        if (welcomeShown)
        {
            welcomeShown = false;
            ScriptRelativeAssetUtility.EnsureImprovedAssetToolsProjectSettingsFolder();
            Save(true);
        }
    }

    private void SetDecision(ThumbnailWelcomeDecision decision)
    {
        if (decision == ThumbnailWelcomeDecision.None)
            return;

        EditorPrefs.SetInt(GetDecisionEditorPrefKey(), (int)decision);

        if (!welcomeShown)
        {
            welcomeShown = true;
            ScriptRelativeAssetUtility.EnsureImprovedAssetToolsProjectSettingsFolder();
            Save(true);
        }
    }

    private static string GetDecisionEditorPrefKey()
    {
        // Scope to project path so decisions are per-project, not global.
        string projectToken = UnityEngine.Hash128.Compute(UnityEngine.Application.dataPath ?? string.Empty).ToString();
        return DecisionKeyPrefix + projectToken;
    }
}

public sealed class PrefabThumbnailPostprocessor : AssetPostprocessor
{
    private const string AssetsRoot = "Assets/ImprovedAssetTools/";
    private const string PackageRoot = "Packages/com.fardinhaque.improved-thumbnail-preview/";
    private const int ReimportAllHeuristicImportedAssetThreshold = 1000;
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        PrefabThumbnailService.ClearProjectWindowItemCache();
        Invalidate(importedAssets);
        Invalidate(movedAssets);
        Invalidate(movedFromAssetPaths);

        if (Invalidate(deletedAssets))
            ThumbnailPersistentCacheUtility.PruneMissingAssets();

        if (IsLikelyReimportAll(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
            ThumbnailWelcomeStateStorage.instance.ResetDecision();

        TryScheduleWelcomePopup(importedAssets);
    }

    private static double s_welcomeScheduledAt = -1.0;
    private static bool s_welcomePopupScheduled;

    private static void TryScheduleWelcomePopup(string[] importedAssets)
    {
        if (s_welcomePopupScheduled)
            return;

        if (ThumbnailWelcomeStateStorage.instance.HasDecision)
            return;

        if (!ContainsPluginImport(importedAssets))
            return;

        s_welcomeScheduledAt = EditorApplication.timeSinceStartup;
        s_welcomePopupScheduled = true;
        EditorApplication.update += ShowWelcomePopupDeferred;
    }

    private static void ShowWelcomePopupDeferred()
    {
        // Wait ~1 second after import — ensures we are well outside any native
        // InspectorWindow.RedrawFromNative iteration before calling GetWindow.
        // Both immediate update and delayCall can fire mid-repaint; time-gating avoids it.
        if (EditorApplication.timeSinceStartup - s_welcomeScheduledAt < 1.0)
            return;

        EditorApplication.update -= ShowWelcomePopupDeferred;
        s_welcomeScheduledAt = -1.0;
        s_welcomePopupScheduled = false;
        ThumbnailWelcomePopup.Open();
    }

    private static bool ContainsPluginImport(string[] importedAssets)
    {
        if (importedAssets == null)
            return false;

        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = NormalizePath(importedAssets[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            if (path.StartsWith(AssetsRoot, System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(PackageRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static bool IsLikelyReimportAll(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        int importedCount = importedAssets?.Length ?? 0;
        int deletedCount = deletedAssets?.Length ?? 0;
        int movedCount = movedAssets?.Length ?? 0;
        int movedFromCount = movedFromAssetPaths?.Length ?? 0;

        // Reimport All usually yields a large imported list with few/no move operations.
        return importedCount >= ReimportAllHeuristicImportedAssetThreshold
            && deletedCount == 0
            && movedCount == 0
            && movedFromCount == 0;
    }

    private static bool Invalidate(string[] paths)
    {
        bool invalidatedAny = false;
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrEmpty(path))
                continue;

            if (!ThumbnailAssetPathUtility.IsThumbnailSourceAssetPath(path))
                continue;

            PrefabThumbnailService.InvalidatePath(path);
            invalidatedAny = true;
        }

        return invalidatedAny;
    }
}

}
