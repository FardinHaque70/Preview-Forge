using UnityEditor;
// Preserves a compatibility entry point for selection-collapse behavior while cursor ownership is handled by active preview rendering.

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class ParticlePreviewSelectionCollapser
    {
        static ParticlePreviewSelectionCollapser()
        {
            // Intentionally left as no-op.
            // Pointer/cursor ownership is handled directly in preview rendering.
        }
    }
}
