using UnityEditor;

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
