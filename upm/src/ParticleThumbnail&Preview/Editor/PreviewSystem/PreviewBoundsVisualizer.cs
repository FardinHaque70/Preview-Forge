using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewBoundsVisualizer
    {
        private const float WireAlpha = 0.82f;
        private static readonly Color LabelBackgroundColor = new(0.08f, 0.08f, 0.08f, 0.88f);
        private static readonly Color LabelShadowColor = new(0f, 0f, 0f, 0.7f);
        private static readonly Color WidthColor = new(0.93f, 0.37f, 0.37f, 1f);
        private static readonly Color HeightColor = new(0.44f, 0.86f, 0.44f, 1f);
        private static readonly Color DepthColor = new(0.4f, 0.7f, 0.97f, 1f);
        private static Mesh s_wireCubeMesh;
        private static Material s_wireMaterial;
        private static GUIStyle s_labelStyle;
        private static bool s_cleanupRegistered;

        private enum DimensionAxis
        {
            Width,
            Height,
            Depth,
        }

        internal static void DrawWire(PreviewRenderUtility preview, Bounds bounds)
        {
            if (preview == null)
                return;

            EnsureResources();
            if (s_wireCubeMesh == null || s_wireMaterial == null)
                return;

            Matrix4x4 matrix = Matrix4x4.TRS(bounds.center, Quaternion.identity, bounds.size);
            preview.DrawMesh(s_wireCubeMesh, matrix, s_wireMaterial, 0);
        }

        internal static void DrawLabels(Rect previewRect, Camera camera, Bounds bounds)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
                return;

            EnsureResources();
            DrawDimensionLabel(previewRect, camera, bounds, DimensionAxis.Width);
            DrawDimensionLabel(previewRect, camera, bounds, DimensionAxis.Height);
            DrawDimensionLabel(previewRect, camera, bounds, DimensionAxis.Depth);
        }

        internal static string FormatDimensionLabelForTests(char prefix, float value)
        {
            return $"{prefix} {value:F2}";
        }

        internal static Vector3 GetDimensionAnchorForTests(Bounds bounds, int axisIndex)
        {
            return GetDimensionAnchor(bounds, (DimensionAxis)Mathf.Clamp(axisIndex, 0, 2));
        }

        internal static Color GetDimensionColorForTests(int axisIndex)
        {
            return GetDimensionColor((DimensionAxis)Mathf.Clamp(axisIndex, 0, 2));
        }

        internal static Color GetWireColorForTests(int axisIndex)
        {
            Color color = GetDimensionColorForTests(axisIndex);
            color.a = WireAlpha;
            return color;
        }

        private static void DrawDimensionLabel(Rect previewRect, Camera camera, Bounds bounds, DimensionAxis axis)
        {
            Vector3 anchor = GetDimensionAnchor(bounds, axis);
            Vector3 offset = GetDimensionOffset(bounds, axis);
            if (!TryProjectToPreviewRect(camera, previewRect, anchor + offset, out Vector2 guiPoint))
                return;

            string text = axis switch
            {
                DimensionAxis.Width => FormatDimensionLabelForTests('W', bounds.size.x),
                DimensionAxis.Height => FormatDimensionLabelForTests('H', bounds.size.y),
                _ => FormatDimensionLabelForTests('D', bounds.size.z),
            };

            GUIStyle style = s_labelStyle;
            Vector2 size = style.CalcSize(new GUIContent(text));
            const float horizontalPadding = 6f;
            const float verticalPadding = 2f;
            Rect labelRect = new Rect(
                guiPoint.x - (size.x + horizontalPadding * 2f) * 0.5f,
                guiPoint.y - (size.y + verticalPadding * 2f) * 0.5f,
                size.x + horizontalPadding * 2f,
                size.y + verticalPadding * 2f);

            Rect textRect = new Rect(
                labelRect.x + horizontalPadding,
                labelRect.y + verticalPadding,
                size.x,
                size.y);

            Rect shadowRect = labelRect;
            shadowRect.position += Vector2.one;
            Rect shadowTextRect = textRect;
            shadowTextRect.position += Vector2.one;

            Color previousColor = GUI.color;
            EditorGUI.DrawRect(labelRect, LabelBackgroundColor);
            GUI.color = LabelShadowColor;
            GUI.Label(shadowTextRect, text, style);
            GUI.color = GetDimensionColor(axis);
            GUI.Label(textRect, text, style);
            GUI.color = previousColor;
        }

        private static Vector3 GetDimensionAnchor(Bounds bounds, DimensionAxis axis)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return axis switch
            {
                DimensionAxis.Width => new Vector3((min.x + max.x) * 0.5f, min.y, min.z),
                DimensionAxis.Height => new Vector3(min.x, (min.y + max.y) * 0.5f, min.z),
                _ => new Vector3(min.x, min.y, (min.z + max.z) * 0.5f),
            };
        }

        private static Vector3 GetDimensionOffset(Bounds bounds, DimensionAxis axis)
        {
            float scale = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1f) * 0.045f;
            return axis switch
            {
                DimensionAxis.Width => new Vector3(0f, -scale, -scale),
                DimensionAxis.Height => new Vector3(-scale, 0f, -scale),
                _ => new Vector3(-scale, -scale, 0f),
            };
        }

        private static Color GetDimensionColor(DimensionAxis axis)
        {
            return axis switch
            {
                DimensionAxis.Width => WidthColor,
                DimensionAxis.Height => HeightColor,
                _ => DepthColor,
            };
        }

        private static bool TryProjectToPreviewRect(Camera camera, Rect previewRect, Vector3 worldPoint, out Vector2 guiPoint)
        {
            guiPoint = default;
            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPoint);
            if (viewportPoint.z <= 0f)
                return false;

            guiPoint = new Vector2(
                previewRect.x + viewportPoint.x * previewRect.width,
                previewRect.y + (1f - viewportPoint.y) * previewRect.height);
            return true;
        }

        private static void EnsureResources()
        {
            EnsureCleanupCallbacks();

            if (s_wireMaterial == null)
            {
                s_wireMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = 2999,
                };
                s_wireMaterial.SetInt("_ZWrite", 0);
                s_wireMaterial.SetInt("_Cull", 0);
                s_wireMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                s_wireMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                s_wireMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                s_wireMaterial.SetColor("_Color", Color.white);
            }

            if (s_wireCubeMesh == null)
            {
                s_wireCubeMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildWireCubeMesh(s_wireCubeMesh);
            }

            if (s_labelStyle == null)
            {
                s_labelStyle = new GUIStyle(PreviewToolbarTheme.InfoValueStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow,
                };
            }
        }

        private static void EnsureCleanupCallbacks()
        {
            if (s_cleanupRegistered)
                return;

            s_cleanupRegistered = true;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeResources;
            EditorApplication.quitting += DisposeResources;
        }

        private static void DisposeResources()
        {
            if (s_cleanupRegistered)
            {
                AssemblyReloadEvents.beforeAssemblyReload -= DisposeResources;
                EditorApplication.quitting -= DisposeResources;
                s_cleanupRegistered = false;
            }

            DestroyOwnedObject(ref s_wireCubeMesh);
            DestroyOwnedObject(ref s_wireMaterial);
            s_labelStyle = null;
        }

        private static void DestroyOwnedObject<T>(ref T value) where T : Object
        {
            if (value == null)
                return;

            Object.DestroyImmediate(value);
            value = null;
        }

        private static void BuildWireCubeMesh(Mesh mesh)
        {
            mesh.Clear();
            Vector3[] corners =
            {
                new(-0.5f, -0.5f, -0.5f),
                new(0.5f, -0.5f, -0.5f),
                new(0.5f, 0.5f, -0.5f),
                new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f),
                new(0.5f, -0.5f, 0.5f),
                new(0.5f, 0.5f, 0.5f),
                new(-0.5f, 0.5f, 0.5f),
            };

            int[] edgeCornerIndices =
            {
                0, 1, 3, 2, 4, 5, 7, 6,
                3, 0, 2, 1, 7, 4, 6, 5,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            DimensionAxis[] edgeAxes =
            {
                DimensionAxis.Width, DimensionAxis.Width, DimensionAxis.Width, DimensionAxis.Width,
                DimensionAxis.Height, DimensionAxis.Height, DimensionAxis.Height, DimensionAxis.Height,
                DimensionAxis.Depth, DimensionAxis.Depth, DimensionAxis.Depth, DimensionAxis.Depth,
            };

            Vector3[] vertices = new Vector3[edgeCornerIndices.Length];
            Color[] colors = new Color[edgeCornerIndices.Length];
            int[] indices = new int[edgeCornerIndices.Length];
            for (int i = 0; i < edgeCornerIndices.Length; i += 2)
            {
                DimensionAxis axis = edgeAxes[i / 2];
                Color wireColor = GetDimensionColor(axis);
                wireColor.a = WireAlpha;

                vertices[i] = corners[edgeCornerIndices[i]];
                vertices[i + 1] = corners[edgeCornerIndices[i + 1]];
                colors[i] = wireColor;
                colors[i + 1] = wireColor;
                indices[i] = i;
                indices[i + 1] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }
    }
}
