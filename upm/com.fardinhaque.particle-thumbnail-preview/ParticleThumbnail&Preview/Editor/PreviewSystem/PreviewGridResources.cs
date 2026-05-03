using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
// Loads, caches, and serves grid/checker resources required for stable and performant preview background rendering.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewGridResources
    {
        internal static Mesh CreateGridMesh(float halfSize, float step, float alpha, bool is2D, PreviewGridStyle style)
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            RebuildGridMesh(mesh, halfSize, step, alpha, is2D, style);
            return mesh;
        }

        internal static void RebuildGridMesh(Mesh mesh, float halfSize, float step, float alpha, bool is2D, PreviewGridStyle style)
        {
            if (mesh == null)
                return;

            if (style == PreviewGridStyle.Classic)
            {
                BuildGridMesh(mesh, halfSize, step, alpha, is2D);
                return;
            }

            BuildStylizedGridMesh(mesh, halfSize, step, alpha, is2D);
        }

        internal static void EnsureGridMaterial(ref Material material)
        {
            if (material != null)
                return;

            material = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 2999;
        }

        private static void BuildGridMesh(Mesh mesh, float halfSize, float step, float alpha, bool is2D)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            Color lineColor = new Color(1f, 1f, 1f, alpha);
            int index = 0;

            if (is2D)
            {
                for (float x = -halfSize; x <= halfSize + 0.0001f; x += step)
                {
                    vertices.Add(new Vector3(x, -halfSize, 0f));
                    vertices.Add(new Vector3(x, halfSize, 0f));
                    colors.Add(lineColor);
                    colors.Add(lineColor);
                    indices.Add(index++);
                    indices.Add(index++);
                }

                for (float y = -halfSize; y <= halfSize + 0.0001f; y += step)
                {
                    vertices.Add(new Vector3(-halfSize, y, 0f));
                    vertices.Add(new Vector3(halfSize, y, 0f));
                    colors.Add(lineColor);
                    colors.Add(lineColor);
                    indices.Add(index++);
                    indices.Add(index++);
                }
            }
            else
            {
                for (float x = -halfSize; x <= halfSize + 0.0001f; x += step)
                {
                    vertices.Add(new Vector3(x, 0f, -halfSize));
                    vertices.Add(new Vector3(x, 0f, halfSize));
                    colors.Add(lineColor);
                    colors.Add(lineColor);
                    indices.Add(index++);
                    indices.Add(index++);
                }

                for (float z = -halfSize; z <= halfSize + 0.0001f; z += step)
                {
                    vertices.Add(new Vector3(-halfSize, 0f, z));
                    vertices.Add(new Vector3(halfSize, 0f, z));
                    colors.Add(lineColor);
                    colors.Add(lineColor);
                    indices.Add(index++);
                    indices.Add(index++);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        private static void BuildStylizedGridMesh(Mesh mesh, float halfSize, float step, float alpha, bool is2D)
        {
            float safeHalfSize = Mathf.Max(0.05f, halfSize);
            float safeStep = Mathf.Max(0.05f, step);
            int count = Mathf.Max(1, Mathf.RoundToInt(safeHalfSize / safeStep));

            var vertices = new List<Vector3>();
            var colors = new List<Color>();

            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, alpha)
                : new Color(0f, 0f, 0f, alpha);
            Color axisX = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.28f, 0.28f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.75f, 0.12f, 0.12f, Mathf.Clamp01(alpha * 1.85f));
            Color axisY = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 1f, 0.28f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.12f, 0.58f, 0.12f, Mathf.Clamp01(alpha * 1.85f));
            Color axisZ = EditorGUIUtility.isProSkin
                ? new Color(0.33f, 0.66f, 1f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.08f, 0.24f, 0.62f, Mathf.Clamp01(alpha * 1.85f));
            Color secondaryAxis = is2D ? axisY : axisZ;

            for (int i = -count; i <= count; i++)
            {
                float position = i * safeStep;
                float fade = Mathf.Pow(1f - Mathf.Abs(position) / safeHalfSize, 2f);
                bool isCenter = i == 0;

                Color xLine = isCenter ? axisX : baseColor;
                Color xPeak = new Color(xLine.r, xLine.g, xLine.b, xLine.a * fade);
                Color xEdge = new Color(xLine.r, xLine.g, xLine.b, 0f);

                Color secondaryLine = isCenter ? secondaryAxis : baseColor;
                Color secondaryPeak = new Color(secondaryLine.r, secondaryLine.g, secondaryLine.b, secondaryLine.a * fade);
                Color secondaryEdge = new Color(secondaryLine.r, secondaryLine.g, secondaryLine.b, 0f);

                if (is2D)
                {
                    vertices.Add(new Vector3(-safeHalfSize, position, 0f));
                    colors.Add(xEdge);
                    vertices.Add(new Vector3(0f, position, 0f));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(0f, position, 0f));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(safeHalfSize, position, 0f));
                    colors.Add(xEdge);

                    vertices.Add(new Vector3(position, -safeHalfSize, 0f));
                    colors.Add(secondaryEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(secondaryPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(secondaryPeak);
                    vertices.Add(new Vector3(position, safeHalfSize, 0f));
                    colors.Add(secondaryEdge);
                }
                else
                {
                    vertices.Add(new Vector3(-safeHalfSize, 0f, position));
                    colors.Add(xEdge);
                    vertices.Add(new Vector3(0f, 0f, position));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(0f, 0f, position));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(safeHalfSize, 0f, position));
                    colors.Add(xEdge);

                    vertices.Add(new Vector3(position, 0f, -safeHalfSize));
                    colors.Add(secondaryEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(secondaryPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(secondaryPeak);
                    vertices.Add(new Vector3(position, 0f, safeHalfSize));
                    colors.Add(secondaryEdge);
                }
            }

            int[] indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }
    }
}
