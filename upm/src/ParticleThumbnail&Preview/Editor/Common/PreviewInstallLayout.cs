using System.IO;
using UnityEditor;
using UnityObject = UnityEngine.Object;
// Resolves editor asset locations across Asset Store imports and Git UPM installs without requiring both roots in one project.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewInstallLayout
    {
        internal const string ToolFolderName = "ParticleThumbnail&Preview";
        internal const string PackageName = "com.fardinhaque.particle-thumbnail-preview";
        internal const string AssetsRoot = "Assets/" + ToolFolderName;
        internal const string PackageRoot = "Packages/" + PackageName + "/" + ToolFolderName;

        private static readonly string[] InstallRoots =
        {
            AssetsRoot,
            PackageRoot,
        };

        internal static string[] BuildAssetPaths(string relativePathFromToolRoot)
        {
            string normalizedRelativePath = NormalizeRelativePath(relativePathFromToolRoot);
            string[] paths = new string[InstallRoots.Length];
            for (int i = 0; i < InstallRoots.Length; i++)
                paths[i] = InstallRoots[i] + "/" + normalizedRelativePath;
            return paths;
        }

        internal static T LoadFirstAssetAtRelativePath<T>(string relativePathFromToolRoot) where T : UnityObject
        {
            string[] candidatePaths = BuildAssetPaths(relativePathFromToolRoot);
            for (int i = 0; i < candidatePaths.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(candidatePaths[i]);
                if (asset != null)
                    return asset;
            }

            return null;
        }

        internal static string TryResolveExistingAbsolutePath(string relativePathFromToolRoot)
        {
            string[] candidatePaths = BuildAssetPaths(relativePathFromToolRoot);
            for (int i = 0; i < candidatePaths.Length; i++)
            {
                string fullPath = Path.GetFullPath(candidatePaths[i]);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private static string NormalizeRelativePath(string relativePathFromToolRoot)
        {
            if (string.IsNullOrEmpty(relativePathFromToolRoot))
                return string.Empty;

            return relativePathFromToolRoot.TrimStart('/');
        }
    }
}
