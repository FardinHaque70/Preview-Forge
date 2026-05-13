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
        private static Mesh s_wireRectMesh;
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

        internal static void DrawLabels2D(Rect previewRect, Camera camera, Bounds bounds, float width, float height)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
                return;

            EnsureResources();
            if (!TryGetBoundsGuiRect(previewRect, camera, bounds, out Rect boundsRect))
                return;

            DrawUiBoundsRect(boundsRect, previewRect);
            DrawDimensionLabelAtGuiPoint(previewRect, new Vector2(boundsRect.center.x, boundsRect.yMax - 14f), 'W', width, WidthColor);
            DrawDimensionLabelAtGuiPoint(previewRect, new Vector2(boundsRect.xMin + 20f, boundsRect.center.y), 'H', height, HeightColor);
        }

        private static void DrawUiBoundsRect(Rect boundsRect, Rect previewRect)
        {
            const float minSize = 1f;
            if (boundsRect.width < minSize || boundsRect.height < minSize)
                return;

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(0.92f, 0.92f, 0.92f, 1f);

            float xMin = Mathf.Clamp(boundsRect.xMin, previewRect.xMin, previewRect.xMax);
            float xMax = Mathf.Clamp(boundsRect.xMax, previewRect.xMin, previewRect.xMax);
            float yMin = Mathf.Clamp(boundsRect.yMin, previewRect.yMin, previewRect.yMax);
            float yMax = Mathf.Clamp(boundsRect.yMax, previewRect.yMin, previewRect.yMax);
            if (xMax - xMin >= minSize && yMax - yMin >= minSize)
            {
                Handles.DrawAAPolyLine(
                    1f,
                    new Vector3(xMin, yMin),
                    new Vector3(xMax, yMin),
                    new Vector3(xMax, yMax),
                    new Vector3(xMin, yMax),
                    new Vector3(xMin, yMin));
            }

            Handles.color = previous;
            Handles.EndGUI();
        }

        internal static void DrawFlatWire(PreviewRenderUtility preview, Bounds bounds, Color color)
        {
            if (preview == null)
                return;

            EnsureResources();
            if (s_wireRectMesh == null || s_wireMaterial == null)
                return;

            Color previousColor = s_wireMaterial.GetColor("_Color");
            s_wireMaterial.SetColor("_Color", color);
            Vector3 scale = new Vector3(
                Mathf.Max(bounds.size.x, 0.0001f),
                Mathf.Max(bounds.size.y, 0.0001f),
                1f);
            Matrix4x4 matrix = Matrix4x4.TRS(bounds.center, Quaternion.identity, scale);
            preview.DrawMesh(s_wireRectMesh, matrix, s_wireMaterial, 0);
            s_wireMaterial.SetColor("_Color", previousColor);
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

        private static void DrawDimensionLabel(Rect previewRect, Camera camera, Bounds bounds, DimensionAxis axis, float? valueOverride = null)
        {
            Vector3 anchor = GetDimensionAnchor(bounds, axis);
            Vector3 offset = GetDimensionOffset(bounds, axis);
            if (!TryProjectToPreviewRect(camera, previewRect, anchor + offset, out Vector2 guiPoint))
                return;

            float value = valueOverride ?? GetDimensionValue(bounds, axis);
            char prefix = axis switch
            {
                DimensionAxis.Width => 'W',
                DimensionAxis.Height => 'H',
                _ => 'D',
            };
            DrawDimensionLabelAtGuiPoint(previewRect, guiPoint, prefix, value, GetDimensionColor(axis));
        }

        private static void DrawDimensionLabelAtGuiPoint(Rect previewRect, Vector2 guiPoint, char prefix, float value, Color textColor)
        {
            string text = FormatDimensionLabelForTests(prefix, value);
            GUIStyle style = s_labelStyle;
            Vector2 size = style.CalcSize(new GUIContent(text));
            const float horizontalPadding = 6f;
            const float verticalPadding = 2f;

            float labelWidth = size.x + horizontalPadding * 2f;
            float labelHeight = size.y + verticalPadding * 2f;
            const float edgePadding = 4f;
            float minX = previewRect.xMin + edgePadding;
            float maxX = previewRect.xMax - edgePadding - labelWidth;
            float minY = previewRect.yMin + edgePadding;
            float maxY = previewRect.yMax - edgePadding - labelHeight;
            float labelX = Mathf.Clamp(guiPoint.x - labelWidth * 0.5f, minX, Mathf.Max(minX, maxX));
            float labelY = Mathf.Clamp(guiPoint.y - labelHeight * 0.5f, minY, Mathf.Max(minY, maxY));

            Rect labelRect = new Rect(labelX, labelY, labelWidth, labelHeight);
            Rect textRect = new Rect(
                labelRect.x + horizontalPadding,
                labelRect.y + verticalPadding,
                size.x,
                size.y);

            Rect shadowTextRect = textRect;
            shadowTextRect.position += Vector2.one;

            Color previousColor = GUI.color;
            EditorGUI.DrawRect(labelRect, LabelBackgroundColor);
            GUI.color = LabelShadowColor;
            GUI.Label(shadowTextRect, text, style);
            GUI.color = textColor;
            GUI.Label(textRect, text, style);
            GUI.color = previousColor;
        }

        private static float GetDimensionValue(Bounds bounds, DimensionAxis axis)
        {
            return axis switch
            {
                DimensionAxis.Width => bounds.size.x,
                DimensionAxis.Height => bounds.size.y,
                _ => bounds.size.z,
            };
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

        private static bool TryGetBoundsGuiRect(Rect previewRect, Camera camera, Bounds bounds, out Rect guiRect)
        {
            guiRect = default;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float z = bounds.center.z;

            if (!TryProjectToPreviewRect(camera, previewRect, new Vector3(min.x, min.y, z), out Vector2 bottomLeft))
                return false;
            if (!TryProjectToPreviewRect(camera, previewRect, new Vector3(max.x, min.y, z), out Vector2 bottomRight))
                return false;
            if (!TryProjectToPreviewRect(camera, previewRect, new Vector3(max.x, max.y, z), out Vector2 topRight))
                return false;
            if (!TryProjectToPreviewRect(camera, previewRect, new Vector3(min.x, max.y, z), out Vector2 topLeft))
                return false;

            float xMin = Mathf.Min(bottomLeft.x, bottomRight.x, topRight.x, topLeft.x);
            float xMax = Mathf.Max(bottomLeft.x, bottomRight.x, topRight.x, topLeft.x);
            float yMin = Mathf.Min(bottomLeft.y, bottomRight.y, topRight.y, topLeft.y);
            float yMax = Mathf.Max(bottomLeft.y, bottomRight.y, topRight.y, topLeft.y);
            guiRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return guiRect.width > 0f && guiRect.height > 0f;
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

            if (s_wireRectMesh == null)
            {
                s_wireRectMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildWireRectMesh(s_wireRectMesh);
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
            DestroyOwnedObject(ref s_wireRectMesh);
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

        private static void BuildWireRectMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };

            Color[] colors = new Color[4];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            mesh.colors = colors;

            mesh.SetIndices(new[] { 0, 1, 1, 2, 2, 3, 3, 0 }, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }
    }
}
