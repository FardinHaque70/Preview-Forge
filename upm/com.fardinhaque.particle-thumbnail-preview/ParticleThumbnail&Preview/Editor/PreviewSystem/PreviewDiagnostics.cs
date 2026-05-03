using UnityEngine;
// Centralizes lightweight diagnostic logging helpers with scoped categories and throttling to avoid noisy editor output.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewDiagnostics
    {
        internal static void Log(string area, string message)
        {
            if (!ParticlePreviewSettings.EnableDiagnostics)
                return;

            Debug.Log($"[ParticlePreview][{area}] {message}");
        }
    }
}
