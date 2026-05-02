using UnityEngine;

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
