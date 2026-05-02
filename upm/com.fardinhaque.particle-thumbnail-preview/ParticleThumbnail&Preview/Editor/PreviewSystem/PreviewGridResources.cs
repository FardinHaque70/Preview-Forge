using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewGridResources
    {
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

        internal static void EnsureGridMesh(ref Mesh mesh, float halfSize, float step, float alpha, bool is2D)
        {
            if (mesh != null)
                return;

            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            BuildGridMesh(mesh, halfSize, step, alpha, is2D);
        }

        internal static void EnsureStylizedGridMesh(ref Mesh mesh, float halfSize, float step, float alpha, bool is2D)
        {
            if (mesh != null)
                return;

            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            BuildStylizedGridMesh(mesh, halfSize, step, alpha, is2D);
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
            Color axisB = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 1f, 0.28f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.12f, 0.58f, 0.12f, Mathf.Clamp01(alpha * 1.85f));

            for (int i = -count; i <= count; i++)
            {
                float position = i * safeStep;
                float fade = Mathf.Pow(1f - Mathf.Abs(position) / safeHalfSize, 2f);
                bool isCenter = i == 0;

                Color xLine = isCenter ? axisX : baseColor;
                Color xPeak = new Color(xLine.r, xLine.g, xLine.b, xLine.a * fade);
                Color xEdge = new Color(xLine.r, xLine.g, xLine.b, isCenter ? xLine.a * 0.32f : 0f);

                Color bLine = isCenter ? axisB : baseColor;
                Color bPeak = new Color(bLine.r, bLine.g, bLine.b, bLine.a * fade);
                Color bEdge = new Color(bLine.r, bLine.g, bLine.b, isCenter ? bLine.a * 0.32f : 0f);

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
                    colors.Add(bEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(bPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(bPeak);
                    vertices.Add(new Vector3(position, safeHalfSize, 0f));
                    colors.Add(bEdge);
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
                    colors.Add(bEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(bPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(bPeak);
                    vertices.Add(new Vector3(position, 0f, safeHalfSize));
                    colors.Add(bEdge);
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
