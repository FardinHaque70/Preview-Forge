using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
// Resolves effective preview mode behavior from settings and runtime overrides, including 2D and 3D mode decisions.

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum PreviewModeOverride
    {
        Auto = 0,
        Force2D = 1,
        Force3D = 2,
    }

    internal readonly struct PreviewModeContext
    {
        public readonly PreviewModeOverride Override;
        public readonly PreviewRenderPipelineKind PipelineKind;
        public readonly bool IsEditorDefaultBehavior2D;
        public readonly bool IsUrp2DRenderer;
        public readonly bool Effective2D;

        public PreviewModeContext(
            PreviewModeOverride modeOverride,
            PreviewRenderPipelineKind pipelineKind,
            bool isEditorDefaultBehavior2D,
            bool isUrp2DRenderer,
            bool effective2D)
        {
            Override = modeOverride;
            PipelineKind = pipelineKind;
            IsEditorDefaultBehavior2D = isEditorDefaultBehavior2D;
            IsUrp2DRenderer = isUrp2DRenderer;
            Effective2D = effective2D;
        }
    }

    internal static class PreviewModeResolver
    {
        internal static bool ResolveEffective2DForTests(PreviewModeOverride modeOverride, bool isUrp2DRenderer, bool isEditorDefaultBehavior2D)
        {
            return modeOverride switch
            {
                PreviewModeOverride.Force2D => true,
                PreviewModeOverride.Force3D => false,
                _ => isUrp2DRenderer || isEditorDefaultBehavior2D,
            };
        }

        public static PreviewModeContext Resolve(PreviewModeOverride modeOverride)
        {
            bool editorMode2D = IsEditorDefaultBehavior2D();
            bool urp2D = IsUrp2DRendererActive();
            PreviewRenderPipelineKind kind = PreviewRenderCompatibilityUtility.DetectCurrentPipelineKind();

            bool effective2D = ResolveEffective2DForTests(modeOverride, urp2D, editorMode2D);

            return new PreviewModeContext(modeOverride, kind, editorMode2D, urp2D, effective2D);
        }

        private static bool IsEditorDefaultBehavior2D()
        {
#if UNITY_2018_1_OR_NEWER
            return EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D;
#else
            return false;
#endif
        }

        private static bool IsUrp2DRendererActive()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                return false;

            string pipelineTypeName = pipeline.GetType().FullName ?? pipeline.GetType().Name ?? string.Empty;
            if (pipelineTypeName.IndexOf("UniversalRenderPipeline", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            try
            {
                SerializedObject serialized = new SerializedObject(pipeline);
                SerializedProperty rendererList = serialized.FindProperty("m_RendererDataList");
                SerializedProperty defaultIndexProperty = serialized.FindProperty("m_DefaultRendererIndex");
                if (rendererList == null || !rendererList.isArray || rendererList.arraySize <= 0)
                    return false;

                int defaultIndex = defaultIndexProperty != null
                    ? Mathf.Clamp(defaultIndexProperty.intValue, 0, rendererList.arraySize - 1)
                    : 0;

                SerializedProperty rendererEntry = rendererList.GetArrayElementAtIndex(defaultIndex);
                Object rendererData = rendererEntry != null ? rendererEntry.objectReferenceValue : null;
                if (rendererData == null)
                    return false;

                string rendererTypeName = rendererData.GetType().FullName ?? rendererData.GetType().Name ?? string.Empty;
                return rendererTypeName.IndexOf("Renderer2DData", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
