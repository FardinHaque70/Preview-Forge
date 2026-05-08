using System.Collections.Generic;
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
    }
}
