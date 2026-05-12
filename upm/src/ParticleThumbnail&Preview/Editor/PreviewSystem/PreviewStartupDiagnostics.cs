using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Writes a focused first-selection startup trace so inspector rebuild races can be diagnosed from a single shared log file.

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class PreviewStartupDiagnostics
    {
        private const double CaptureWindowSeconds = 180.0d;
        private const int MaxLines = 6000;
        private const int MaxStackCharacters = 6000;
        private static readonly double DomainLoadTime;
        private static readonly System.Collections.Generic.HashSet<string> SeenMessages = new();
        private static int s_linesWritten;
        private static bool s_logRegistered;
        private static bool s_writeFailureReported;

        internal static string LogPath { get; } = BuildProjectLogPath();
        internal static string EditorLogPath { get; } = BuildProjectLogPath(batchMode: false);
        internal static string BatchLogPath { get; } = BuildProjectLogPath(batchMode: true);

        static PreviewStartupDiagnostics()
        {
            DomainLoadTime = EditorApplication.timeSinceStartup;
            StartNewLog();
            Register();
            Debug.Log(
                $"[ParticleThumbnailPreview] Startup diagnostics log path: {LogPath} " +
                $"(editor={EditorLogPath}, batch={BatchLogPath})");
            Log("DomainLoad", $"unity={Application.unityVersion} batch={Application.isBatchMode} project='{GetProjectRoot()}' cwd='{Path.GetFullPath(".")}'");
            LogSelection("DomainLoad.Selection");
        }

        internal static void Log(string area, string message, bool force = false)
        {
            if (!force && !IsCaptureWindowActive())
                return;

            if (s_linesWritten >= MaxLines)
                return;

            try
            {
                string safeArea = string.IsNullOrEmpty(area) ? "General" : area;
                string safeMessage = string.IsNullOrEmpty(message) ? "<empty>" : message.Replace('\n', ' ').Replace('\r', ' ');
                string messageKey = safeArea + "|" + safeMessage;
                if (!force && !SeenMessages.Add(messageKey))
                    return;

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} t={EditorApplication.timeSinceStartup:F3} [{safeArea}] {safeMessage}";
                AppendLine(line);
            }
            catch (Exception exception)
            {
                ReportWriteFailure(exception);
            }
        }

        internal static void LogSelection(string area)
        {
            Log(area, $"selection={DescribeTargets(Selection.objects)} active={DescribeObject(Selection.activeObject)} assetGuids={DescribeAssetGuids()}");
        }

        internal static string DescribeTargets(UnityObject[] targets)
        {
            if (targets == null)
                return "<null>";

            if (targets.Length == 0)
                return "<empty>";

            int count = Mathf.Min(targets.Length, 6);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = DescribeObject(targets[i]);

            string suffix = targets.Length > count ? ", ..." : string.Empty;
            return "[" + string.Join(", ", parts) + suffix + "]";
        }

        internal static string DescribeObject(UnityObject value)
        {
            if (value == null)
                return "<null>";

            try
            {
                string path = AssetDatabase.GetAssetPath(value);
                bool persistent = EditorUtility.IsPersistent(value);
                string prefab = value is GameObject gameObject
                    ? PrefabUtility.GetPrefabAssetType(gameObject).ToString()
                    : "-";
                return $"{value.GetType().Name}('{value.name}', id={value.GetInstanceID()}, persistent={persistent}, prefab={prefab}, path='{path}')";
            }
            catch (Exception exception)
            {
                return $"{value.GetType().Name}('{value.name}', describeError={exception.GetType().Name})";
            }
        }

        private static bool IsCaptureWindowActive()
        {
            return EditorApplication.timeSinceStartup - DomainLoadTime <= CaptureWindowSeconds;
        }

        private static void StartNewLog()
        {
            try
            {
                string directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(
                    LogPath,
                    "Particle Thumbnail & Preview Startup Diagnostics\n" +
                    $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                    $"Path: {LogPath}\n\n");
                s_linesWritten = 0;
            }
            catch (Exception exception)
            {
                ReportWriteFailure(exception);
            }
        }

        private static string BuildProjectLogPath()
        {
            return BuildProjectLogPath(Application.isBatchMode);
        }

        private static string BuildProjectLogPath(bool batchMode)
        {
            return Path.Combine(
                GetProjectRoot(),
                "Library",
                "ParticleThumbnailAndPreview",
                batchMode ? "StartupDiagnostics-Batch.log" : "StartupDiagnostics-Editor.log");
        }

        private static string GetProjectRoot()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                string projectRoot = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(projectRoot))
                    return Path.GetFullPath(projectRoot);
            }

            return Path.GetFullPath(".");
        }

        private static void Register()
        {
            if (s_logRegistered)
                return;

            s_logRegistered = true;
            Application.logMessageReceived += OnUnityLogMessage;
            Selection.selectionChanged += OnSelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnBeforeAssemblyReload;
        }

        private static void OnSelectionChanged()
        {
            LogSelection("SelectionChanged");
        }

        private static void OnUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
                return;

            bool relevant =
                Contains(condition, "InvalidOperationException")
                || Contains(condition, "Collection was modified")
                || Contains(stackTrace, "InspectorWindow.RedrawFromNative")
                || Contains(stackTrace, "ParticleThumbnailAndPreview");
            if (!relevant)
                return;

            string stack = string.IsNullOrEmpty(stackTrace)
                ? "<no stack>"
                : stackTrace;
            if (stack.Length > MaxStackCharacters)
                stack = stack.Substring(0, MaxStackCharacters) + "...<truncated>";

            Log("UnityLog", $"type={type} condition='{condition}' stack='{stack}'", force: true);
            LogSelection("UnityLog.Selection");
        }

        private static bool Contains(string value, string pattern)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeAssetGuids()
        {
            string[] guids = Selection.assetGUIDs;
            if (guids == null || guids.Length == 0)
                return "<empty>";

            int count = Mathf.Min(guids.Length, 6);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = guids[i] ?? "<null>";

            string suffix = guids.Length > count ? ", ..." : string.Empty;
            return "[" + string.Join(", ", parts) + suffix + "]";
        }

        private static void AppendLine(string line)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
            s_linesWritten++;
        }

        private static void ReportWriteFailure(Exception exception)
        {
            if (s_writeFailureReported)
                return;

            s_writeFailureReported = true;
            Debug.LogWarning(
                $"[ParticleThumbnailPreview] Failed to write startup diagnostics log at '{LogPath}': {exception.GetType().Name}: {exception.Message}");
        }

        private static void OnBeforeAssemblyReload()
        {
            Application.logMessageReceived -= OnUnityLogMessage;
            Selection.selectionChanged -= OnSelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnBeforeAssemblyReload;
            s_logRegistered = false;
            Log("Shutdown", "diagnostics unregistered", force: true);
        }
    }
}
