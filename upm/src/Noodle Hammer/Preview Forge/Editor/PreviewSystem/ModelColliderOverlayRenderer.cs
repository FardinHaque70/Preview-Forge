using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class ModelColliderOverlayRenderer
    {
        private static readonly Color SolidColor = new Color(0.2f, 1f, 0.2f, 0.25f);
        private static readonly Color SolidTriggerColor = new Color(1f, 0.8f, 0.2f, 0.25f);
        private static readonly Color WireColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        private static readonly Color WireTriggerColor = new Color(1f, 0.8f, 0.2f, 0.8f);

        private static Mesh s_unitCubeSolidMesh;
        private static Mesh s_unitSphereSolidMesh;
        private static Mesh s_unitSphereWireMesh;
        private static Mesh s_boundsWireCubeMesh;
        private static Mesh s_unitQuadSolidMesh;
        private static Mesh s_unitSquareWireMesh;
        private static Mesh s_unitDiscSolidMesh;
        private static Mesh s_unitCircleWireMesh;

        private static Material s_solidColliderMaterial;
        private static Material s_solidTriggerMaterial;
        private static Material s_wireColliderMaterial;
        private static Material s_wireTriggerMaterial;

        private static bool s_cleanupRegistered;

        internal static void Draw(
            PreviewRenderUtility preview,
            IReadOnlyList<Collider> colliders3D,
            IReadOnlyList<Collider2D> colliders2D)
        {
            bool has3D = colliders3D != null && colliders3D.Count > 0;
            bool has2D = colliders2D != null && colliders2D.Count > 0;
            if (preview == null || (!has3D && !has2D))
                return;

            EnsureResources();
            if (s_solidColliderMaterial == null
                || s_solidTriggerMaterial == null
                || s_wireColliderMaterial == null
                || s_wireTriggerMaterial == null)
                return;

            if (has3D)
            {
                for (int i = 0; i < colliders3D.Count; i++)
                {
                    Collider collider = colliders3D[i];
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                        continue;

                    if (!TryBuildColliderSolidDraw(collider, out Mesh solidMesh, out Matrix4x4 solidMatrix))
                        continue;

                    if (!TryBuildColliderWireDraw(collider, out Mesh wireMesh, out Matrix4x4 wireMatrix))
                        continue;

                    Material solidMaterial = collider.isTrigger ? s_solidTriggerMaterial : s_solidColliderMaterial;
                    Material wireMaterial = collider.isTrigger ? s_wireTriggerMaterial : s_wireColliderMaterial;
                    preview.DrawMesh(solidMesh, solidMatrix, solidMaterial, 0);
                    preview.DrawMesh(wireMesh, wireMatrix, wireMaterial, 0);
                }
            }

            if (has2D)
            {
                for (int i = 0; i < colliders2D.Count; i++)
                {
                    Collider2D collider = colliders2D[i];
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                        continue;

                    if (!TryBuildCollider2DSolidDraw(collider, out Mesh solidMesh, out Matrix4x4 solidMatrix))
                        continue;

                    if (!TryBuildCollider2DWireDraw(collider, out Mesh wireMesh, out Matrix4x4 wireMatrix))
                        continue;

                    Material solidMaterial = collider.isTrigger ? s_solidTriggerMaterial : s_solidColliderMaterial;
                    Material wireMaterial = collider.isTrigger ? s_wireTriggerMaterial : s_wireColliderMaterial;
                    preview.DrawMesh(solidMesh, solidMatrix, solidMaterial, 0);
                    preview.DrawMesh(wireMesh, wireMatrix, wireMaterial, 0);
                }
            }
        }

        #region BuildData

        private static bool TryBuildColliderSolidDraw(Collider collider, out Mesh mesh, out Matrix4x4 matrix)
        {
            mesh = null;
            matrix = Matrix4x4.identity;

            if (collider is BoxCollider box)
            {
                mesh = s_unitCubeSolidMesh;
                if (mesh == null)
                    return false;

                Vector3 scale = Vector3.Scale(box.size, AbsScale(box.transform.lossyScale));
                matrix = Matrix4x4.TRS(box.transform.TransformPoint(box.center), box.transform.rotation, scale);
                return true;
            }

            if (collider is SphereCollider sphere)
            {
                mesh = s_unitSphereSolidMesh;
                if (mesh == null)
                    return false;

                float radius = ComputeSphereWorldRadius(sphere);
                matrix = Matrix4x4.TRS(
                    sphere.transform.TransformPoint(sphere.center),
                    sphere.transform.rotation,
                    Vector3.one * (radius * 2f));
                return true;
            }

            return false;
        }

        private static bool TryBuildColliderWireDraw(Collider collider, out Mesh mesh, out Matrix4x4 matrix)
        {
            mesh = null;
            matrix = Matrix4x4.identity;

            if (collider is BoxCollider box)
            {
                mesh = s_boundsWireCubeMesh;
                if (mesh == null)
                    return false;

                Vector3 scale = Vector3.Scale(box.size, AbsScale(box.transform.lossyScale));
                matrix = Matrix4x4.TRS(box.transform.TransformPoint(box.center), box.transform.rotation, scale);
                return true;
            }

            if (collider is SphereCollider sphere)
            {
                mesh = s_unitSphereWireMesh;
                if (mesh == null)
                    return false;

                float radius = ComputeSphereWorldRadius(sphere);
                matrix = Matrix4x4.TRS(
                    sphere.transform.TransformPoint(sphere.center),
                    sphere.transform.rotation,
                    Vector3.one * (radius * 2f));
                return true;
            }

            return false;
        }

        private static bool TryBuildCollider2DSolidDraw(Collider2D collider, out Mesh mesh, out Matrix4x4 matrix)
        {
            mesh = null;
            matrix = Matrix4x4.identity;

            if (collider is BoxCollider2D box)
            {
                mesh = s_unitQuadSolidMesh;
                if (mesh == null)
                    return false;

                Vector3 absScale = AbsScale(box.transform.lossyScale);
                float width = box.size.x * absScale.x;
                float height = box.size.y * absScale.y;
                float thickness = Compute2DThickness(width, height);
                Vector3 center = box.transform.TransformPoint(new Vector3(box.offset.x, box.offset.y, 0f));
                matrix = Matrix4x4.TRS(center, box.transform.rotation, new Vector3(width, height, thickness));
                return true;
            }

            if (collider is CircleCollider2D circle)
            {
                mesh = s_unitDiscSolidMesh;
                if (mesh == null)
                    return false;

                Vector3 absScale = AbsScale(circle.transform.lossyScale);
                float radius = circle.radius * Mathf.Max(absScale.x, absScale.y);
                float diameter = radius * 2f;
                float thickness = Compute2DThickness(diameter, diameter);
                Vector3 center = circle.transform.TransformPoint(new Vector3(circle.offset.x, circle.offset.y, 0f));
                matrix = Matrix4x4.TRS(center, circle.transform.rotation, new Vector3(diameter, diameter, thickness));
                return true;
            }

            return false;
        }

        private static bool TryBuildCollider2DWireDraw(Collider2D collider, out Mesh mesh, out Matrix4x4 matrix)
        {
            mesh = null;
            matrix = Matrix4x4.identity;

            if (collider is BoxCollider2D box)
            {
                mesh = s_unitSquareWireMesh;
                if (mesh == null)
                    return false;

                Vector3 absScale = AbsScale(box.transform.lossyScale);
                float width = box.size.x * absScale.x;
                float height = box.size.y * absScale.y;
                float thickness = Compute2DThickness(width, height);
                Vector3 center = box.transform.TransformPoint(new Vector3(box.offset.x, box.offset.y, 0f));
                matrix = Matrix4x4.TRS(center, box.transform.rotation, new Vector3(width, height, thickness));
                return true;
            }

            if (collider is CircleCollider2D circle)
            {
                mesh = s_unitCircleWireMesh;
                if (mesh == null)
                    return false;

                Vector3 absScale = AbsScale(circle.transform.lossyScale);
                float radius = circle.radius * Mathf.Max(absScale.x, absScale.y);
                float diameter = radius * 2f;
                float thickness = Compute2DThickness(diameter, diameter);
                Vector3 center = circle.transform.TransformPoint(new Vector3(circle.offset.x, circle.offset.y, 0f));
                matrix = Matrix4x4.TRS(center, circle.transform.rotation, new Vector3(diameter, diameter, thickness));
                return true;
            }

            return false;
        }

        private static float ComputeSphereWorldRadius(SphereCollider sphere)
        {
            Vector3 scale = AbsScale(sphere.transform.lossyScale);
            return sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        }

        private static float Compute2DThickness(float worldWidth, float worldHeight)
        {
            float reference = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(worldWidth), Mathf.Abs(worldHeight)));
            return Mathf.Max(0.005f, reference * 0.05f);
        }

        private static Vector3 AbsScale(Vector3 scale)
        {
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        #endregion

        #region MeshGeneration

        private static void BuildBoundsWireCubeMesh(Mesh mesh)
        {
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

            int[,] edgePairs =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };

            var vertices = new List<Vector3>();
            var colors = new List<Color>();

            for (int i = 0; i < edgePairs.GetLength(0); i++)
            {
                vertices.Add(corners[edgePairs[i, 0]]);
                colors.Add(Color.white);
                vertices.Add(corners[edgePairs[i, 1]]);
                colors.Add(Color.white);
            }

            SetLineMeshData(mesh, vertices, colors);
        }

        private static void BuildUnitSphereWireMesh(Mesh mesh)
        {
            const int segments = 32;
            const float radius = 0.5f;
            var vertices = new List<Vector3>(segments * 6);
            var colors = new List<Color>(segments * 6);
            Color c = Color.white;

            static void AddEdge(List<Vector3> verts, List<Color> cols, Vector3 a, Vector3 b, Color col)
            {
                verts.Add(a);
                cols.Add(col);
                verts.Add(b);
                cols.Add(col);
            }

            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments * Mathf.PI * 2f;
                float t1 = (i + 1f) / segments * Mathf.PI * 2f;

                AddEdge(vertices, colors,
                    new Vector3(Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius, 0f),
                    new Vector3(Mathf.Cos(t1) * radius, Mathf.Sin(t1) * radius, 0f), c);
                AddEdge(vertices, colors,
                    new Vector3(Mathf.Cos(t0) * radius, 0f, Mathf.Sin(t0) * radius),
                    new Vector3(Mathf.Cos(t1) * radius, 0f, Mathf.Sin(t1) * radius), c);
                AddEdge(vertices, colors,
                    new Vector3(0f, Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius),
                    new Vector3(0f, Mathf.Cos(t1) * radius, Mathf.Sin(t1) * radius), c);
            }

            SetLineMeshData(mesh, vertices, colors);
        }

        private static void BuildUnitSquareWireMesh(Mesh mesh)
        {
            var vertices = new List<Vector3>
            {
                new(-0.5f, -0.5f, 0f), new(0.5f, -0.5f, 0f),
                new(0.5f, -0.5f, 0f), new(0.5f, 0.5f, 0f),
                new(0.5f, 0.5f, 0f), new(-0.5f, 0.5f, 0f),
                new(-0.5f, 0.5f, 0f), new(-0.5f, -0.5f, 0f),
            };
            var colors = new List<Color>();
            for (int i = 0; i < vertices.Count; i++)
                colors.Add(Color.white);

            SetLineMeshData(mesh, vertices, colors);
        }

        private static void BuildUnitCircleWireMesh(Mesh mesh)
        {
            const int segments = 48;
            const float radius = 0.5f;
            var vertices = new List<Vector3>(segments * 2);
            var colors = new List<Color>(segments * 2);

            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments * Mathf.PI * 2f;
                float t1 = (i + 1f) / segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius, 0f));
                vertices.Add(new Vector3(Mathf.Cos(t1) * radius, Mathf.Sin(t1) * radius, 0f));
                colors.Add(Color.white);
                colors.Add(Color.white);
            }

            SetLineMeshData(mesh, vertices, colors);
        }

        private static Mesh CreateUnitQuadSolidMesh()
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "UnitQuadSolid" };
            mesh.SetVertices(new List<Vector3>
            {
                new(-0.5f, -0.5f, 0f),
                new(0.5f, -0.5f, 0f),
                new(0.5f, 0.5f, 0f),
                new(-0.5f, 0.5f, 0f),
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateUnitDiscSolidMesh()
        {
            const int segments = 48;
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var triangles = new List<int>(segments * 3);

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(t) * 0.5f, Mathf.Sin(t) * 0.5f, 0f));
            }

            for (int i = 0; i < segments; i++)
            {
                int current = i + 1;
                int next = i + 2;
                if (next > segments)
                    next = 1;

                triangles.Add(0);
                triangles.Add(current);
                triangles.Add(next);
            }

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "UnitDiscSolid" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreatePrimitiveMesh(PrimitiveType primitiveType)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            try
            {
                MeshFilter filter = primitive.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    return null;

                Mesh mesh = Object.Instantiate(filter.sharedMesh);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(primitive);
            }
        }

        private static void SetLineMeshData(Mesh mesh, List<Vector3> vertices, List<Color> colors)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);

            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;

            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        #endregion

        #region ResourceLifecycle

        private static void EnsureResources()
        {
            EnsureCleanupCallbacks();

            s_solidColliderMaterial ??= CreateColliderOverlayMaterial(2998, SolidColor);
            s_solidTriggerMaterial ??= CreateColliderOverlayMaterial(2998, SolidTriggerColor);
            s_wireColliderMaterial ??= CreateColliderOverlayMaterial(3000, WireColor);
            s_wireTriggerMaterial ??= CreateColliderOverlayMaterial(3000, WireTriggerColor);

            s_unitCubeSolidMesh ??= CreatePrimitiveMesh(PrimitiveType.Cube);
            s_unitSphereSolidMesh ??= CreatePrimitiveMesh(PrimitiveType.Sphere);
            s_unitQuadSolidMesh ??= CreateUnitQuadSolidMesh();
            s_unitDiscSolidMesh ??= CreateUnitDiscSolidMesh();

            if (s_unitSphereWireMesh == null)
            {
                s_unitSphereWireMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildUnitSphereWireMesh(s_unitSphereWireMesh);
            }

            if (s_boundsWireCubeMesh == null)
            {
                s_boundsWireCubeMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildBoundsWireCubeMesh(s_boundsWireCubeMesh);
            }

            if (s_unitSquareWireMesh == null)
            {
                s_unitSquareWireMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildUnitSquareWireMesh(s_unitSquareWireMesh);
            }

            if (s_unitCircleWireMesh == null)
            {
                s_unitCircleWireMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildUnitCircleWireMesh(s_unitCircleWireMesh);
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

            DestroyOwnedObject(ref s_solidColliderMaterial);
            DestroyOwnedObject(ref s_solidTriggerMaterial);
            DestroyOwnedObject(ref s_wireColliderMaterial);
            DestroyOwnedObject(ref s_wireTriggerMaterial);

            DestroyOwnedObject(ref s_unitCubeSolidMesh);
            DestroyOwnedObject(ref s_unitSphereSolidMesh);
            DestroyOwnedObject(ref s_unitSphereWireMesh);
            DestroyOwnedObject(ref s_boundsWireCubeMesh);
            DestroyOwnedObject(ref s_unitQuadSolidMesh);
            DestroyOwnedObject(ref s_unitSquareWireMesh);
            DestroyOwnedObject(ref s_unitDiscSolidMesh);
            DestroyOwnedObject(ref s_unitCircleWireMesh);
        }

        private static Material CreateColliderOverlayMaterial(int renderQueue, Color color)
        {
            var material = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = renderQueue,
            };
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetColor("_Color", color);
            return material;
        }

        private static void DestroyOwnedObject<T>(ref T value) where T : Object
        {
            if (value == null)
                return;

            Object.DestroyImmediate(value);
            value = null;
        }

        #endregion
    }
}
