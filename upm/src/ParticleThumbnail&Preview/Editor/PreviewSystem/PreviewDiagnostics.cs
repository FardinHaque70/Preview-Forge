using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
// Centralizes diagnostics capture and provides clipboard export helpers for easier bug reporting.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewDiagnostics
    {
        private const int MaxEntries = 400;
        private static readonly Queue<string> Entries = new();
        private static string _pendingLine;
        private static int _pendingRepeatCount;

        internal static void Log(string area, string message)
        {
            if (!PreviewSettings.EnableDiagnostics)
                return;

            Record("INFO", area, message, emitToConsole: false);
        }

        internal static void Warn(string area, string message)
        {
            if (!PreviewSettings.EnableDiagnostics)
                return;

            Record("WARN", area, message, emitToConsole: true);
        }

        internal static void Error(string area, string message)
        {
            if (!PreviewSettings.EnableDiagnostics)
                return;

            Record("ERROR", area, message, emitToConsole: true);
        }

        [MenuItem("Tools/Particle Thumbnail & Preview/Copy Diagnostics To Clipboard")]
        private static void CopyDiagnosticsToClipboard()
        {
            FlushPendingLine();

            var builder = new StringBuilder(16_384);
            builder.AppendLine("=== Particle Thumbnail & Preview Diagnostics ===");
            builder.AppendLine($"Unity: {Application.unityVersion}");
            builder.AppendLine($"Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Selection: {DescribeSelection()}");
            builder.AppendLine();
            builder.AppendLine("--- Recent Entries ---");

            foreach (string entry in Entries)
                builder.AppendLine(entry);

            builder.AppendLine();
            builder.AppendLine("--- Summary (Top Repeated) ---");
            AppendSummary(builder);

            string report = builder.ToString();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log($"[ParticleThumbnailPreview][Diagnostics] Copied {Entries.Count} entries to clipboard.");
        }

        [MenuItem("Tools/Particle Thumbnail & Preview/Clear Diagnostics Buffer")]
        private static void ClearDiagnosticsBuffer()
        {
            _pendingLine = null;
            _pendingRepeatCount = 0;
            Entries.Clear();
            Debug.Log("[ParticleThumbnailPreview][Diagnostics] Cleared diagnostics buffer.");
        }

        private static void Record(string level, string area, string message, bool emitToConsole)
        {
            if (string.IsNullOrEmpty(message))
                return;

            string line = $"[{level}] [ParticleThumbnailPreview][{area}] {message}";
            if (string.Equals(_pendingLine, line, System.StringComparison.Ordinal))
            {
                _pendingRepeatCount++;
            }
            else
            {
                FlushPendingLine();
                _pendingLine = line;
                _pendingRepeatCount = 1;
            }

            if (!emitToConsole)
                return;

            switch (level)
            {
                case "WARN":
                    Debug.LogWarning(line);
                    break;
                case "ERROR":
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }

        private static void FlushPendingLine()
        {
            if (string.IsNullOrEmpty(_pendingLine) || _pendingRepeatCount <= 0)
                return;

            string line = _pendingRepeatCount > 1
                ? $"{_pendingLine} (repeated x{_pendingRepeatCount})"
                : _pendingLine;
            Entries.Enqueue(line);
            while (Entries.Count > MaxEntries)
                Entries.Dequeue();

            _pendingLine = null;
            _pendingRepeatCount = 0;
        }

        private static string DescribeSelection()
        {
            Object[] selection = Selection.objects;
            if (selection == null || selection.Length == 0)
                return "<none>";

            int count = Mathf.Min(selection.Length, 6);
            var parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                Object item = selection[i];
                if (item == null)
                {
                    parts[i] = "<null>";
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(item);
                if (string.IsNullOrEmpty(path))
                    parts[i] = $"{item.GetType().Name}:{item.name}";
                else
                    parts[i] = $"{item.GetType().Name}:{item.name}@{path}";
            }

            string suffix = selection.Length > count ? ", ..." : string.Empty;
            return string.Join(", ", parts) + suffix;
        }

        private static void AppendSummary(StringBuilder builder)
        {
            var counts = new Dictionary<string, int>();
            foreach (string entry in Entries)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                if (!counts.TryAdd(entry, 1))
                    counts[entry]++;
            }

            if (counts.Count == 0)
            {
                builder.AppendLine("<none>");
                return;
            }

            var sorted = new List<KeyValuePair<string, int>>(counts);
            sorted.Sort(static (a, b) => b.Value.CompareTo(a.Value));

            int limit = Mathf.Min(20, sorted.Count);
            for (int i = 0; i < limit; i++)
            {
                KeyValuePair<string, int> kv = sorted[i];
                builder.AppendLine($"x{kv.Value} {kv.Key}");
            }
        }
    }
}
