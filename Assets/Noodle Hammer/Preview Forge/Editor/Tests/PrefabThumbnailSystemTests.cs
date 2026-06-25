using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NoodleHammer.PreviewForge.Editor.Tests
{
    public sealed class PrefabThumbnailSystemTests
    {
        [Test]
        public void RendererRegistry_ParticlePrefab_ResolvesParticleRenderer()
        {
            GameObject root = new GameObject("ParticleRoot");
            try
            {
                root.AddComponent<ParticleSystem>();

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "particle-guid",
                    assetPath: "Assets/Particle.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.TypeOf<ParticlePrefabThumbnailRenderer>());
                Assert.That(supportInfo.Supported, Is.True);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.ParticlePrefab));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_ChildOnlyParticlePrefab_IsUnsupported()
        {
            GameObject root = new GameObject("ParticleRoot");
            try
            {
                GameObject child = new GameObject("ParticleChild");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<ParticleSystem>();

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "particle-guid",
                    assetPath: "Assets/Particle.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.Null);
                Assert.That(supportInfo.Supported, Is.False);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.Unsupported));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_CanvasUiPrefab_ResolvesUiRenderer()
        {
            GameObject root = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                CreateImageChild(root.transform, "Image", new Vector2(120f, 60f), new Vector2(10f, 0f));

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "ui-canvas-guid",
                    assetPath: "Assets/CanvasUi.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.TypeOf<UiPrefabThumbnailRenderer>());
                Assert.That(supportInfo.Supported, Is.True);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.UiPrefab));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_LooseRectTransformUiPrefab_ResolvesUiRenderer()
        {
            GameObject root = new GameObject("LooseUi", typeof(RectTransform));
            try
            {
                RectTransform rectTransform = root.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(220f, 80f);
                root.AddComponent<Image>();

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "ui-loose-guid",
                    assetPath: "Assets/LooseImage.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.TypeOf<UiPrefabThumbnailRenderer>());
                Assert.That(supportInfo.Supported, Is.True);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.UiPrefab));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_LooseTmpTextPrefab_ResolvesUiRenderer()
        {
            GameObject root = new GameObject("LooseTmp", typeof(RectTransform));
            try
            {
                RectTransform rectTransform = root.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(260f, 70f);
                TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
                text.text = "Preview Forge";

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "ui-tmp-guid",
                    assetPath: "Assets/LooseTmp.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.TypeOf<UiPrefabThumbnailRenderer>());
                Assert.That(supportInfo.Supported, Is.True);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.UiPrefab));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_MixedMeshAndUiPrefab_IsUnsupported()
        {
            GameObject root = new GameObject("MixedUiMesh", typeof(RectTransform));
            try
            {
                root.AddComponent<Image>();
                GameObject meshChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshChild.transform.SetParent(root.transform, false);

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "mixed-guid",
                    assetPath: "Assets/Mixed.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.Null);
                Assert.That(supportInfo.Supported, Is.False);
                Assert.That(supportInfo.AssetKind, Is.EqualTo(PrefabThumbnailAssetKind.Unsupported));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRegistry_SpritePrefab_IsUnsupported()
        {
            GameObject root = new GameObject("SpriteRoot");
            try
            {
                root.AddComponent<SpriteRenderer>();

                IPrefabThumbnailRenderer renderer = PrefabThumbnailRendererRegistry.FindBestRenderer(
                    root,
                    guid: "sprite-guid",
                    assetPath: "Assets/Sprite.prefab",
                    out PrefabThumbnailSupportInfo supportInfo);

                Assert.That(renderer, Is.Null);
                Assert.That(supportInfo.Supported, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrefabThumbnailRequest_AssetKindParticipatesInEqualityAndHash()
        {
            PrefabThumbnailRequest particleRequest = new PrefabThumbnailRequest(
                "guid",
                "Assets/Test.prefab",
                PrefabThumbnailAssetKind.ParticlePrefab,
                PrefabThumbnailSurface.ProjectWindowGrid);
            PrefabThumbnailRequest uiRequest = new PrefabThumbnailRequest(
                "guid",
                "Assets/Test.prefab",
                PrefabThumbnailAssetKind.UiPrefab,
                PrefabThumbnailSurface.ProjectWindowGrid);

            Assert.That(particleRequest.Equals(uiRequest), Is.False);
            Assert.That(particleRequest.GetHashCode(), Is.Not.EqualTo(uiRequest.GetHashCode()));
        }

        [Test]
        public void PersistentCacheKey_ChangesWhenAssetKindChanges()
        {
            string settingsToken = "settings";
            string dependencyToken = "dependency";
            PrefabThumbnailRequest particleRequest = new PrefabThumbnailRequest(
                "guid",
                "Assets/Test.prefab",
                PrefabThumbnailAssetKind.ParticlePrefab,
                PrefabThumbnailSurface.ProjectWindowGrid);
            PrefabThumbnailRequest uiRequest = new PrefabThumbnailRequest(
                "guid",
                "Assets/Test.prefab",
                PrefabThumbnailAssetKind.UiPrefab,
                PrefabThumbnailSurface.ProjectWindowGrid);

            string particleKey = PrefabThumbnailPersistentCache.BuildCacheKey(particleRequest, dependencyToken, settingsToken);
            string uiKey = PrefabThumbnailPersistentCache.BuildCacheKey(uiRequest, dependencyToken, settingsToken);

            Assert.That(particleKey, Is.Not.EqualTo(uiKey));
        }

        [Test]
        public void PersistentCacheDirectory_UsesGenericPrefabFolderName()
        {
            string libraryDirectory = Path.Combine("ProjectRoot", "Library");

            string directory = PrefabThumbnailPersistentCache.BuildCurrentCacheDirectoryPathForTests(libraryDirectory);

            Assert.That(
                directory,
                Is.EqualTo(Path.Combine(libraryDirectory, "Noodle Hammer", "Preview Forge", "PrefabThumbnailCache")));
        }

        [Test]
        public void PersistentCacheDirectory_LegacyParticlePathsRemainMigrationSources()
        {
            string libraryDirectory = Path.Combine("ProjectRoot", "Library");

            string nestedLegacyDirectory = PrefabThumbnailPersistentCache.BuildNestedLegacyCacheDirectoryPathForTests(libraryDirectory);
            string flatLegacyDirectory = PrefabThumbnailPersistentCache.BuildFlatLegacyCacheDirectoryPathForTests(libraryDirectory);
            string currentDirectory = PrefabThumbnailPersistentCache.BuildCurrentCacheDirectoryPathForTests(libraryDirectory);

            Assert.That(
                nestedLegacyDirectory,
                Is.EqualTo(Path.Combine(libraryDirectory, "Noodle Hammer", "Preview Forge", "ParticleThumbnailCache")));
            Assert.That(
                flatLegacyDirectory,
                Is.EqualTo(Path.Combine(libraryDirectory, "ParticleThumbnailCache")));
            Assert.That(currentDirectory, Is.Not.EqualTo(nestedLegacyDirectory));
            Assert.That(currentDirectory, Is.Not.EqualTo(flatLegacyDirectory));
        }

        [Test]
        public void SettingsStorage_UsesPrefabNamedSettingsAssetPath()
        {
            Assert.That(
                PrefabThumbnailSettingsStorage.GetSettingsPathForTests(),
                Is.EqualTo("Assets/Noodle Hammer/Preview Forge/Settings/PrefabThumbnailSettings.asset"));
        }

        [Test]
        public void SettingsStorage_LegacyParticleSettingsPath_RemainsMigrationSource()
        {
            Assert.That(
                PrefabThumbnailSettingsStorage.GetLegacySettingsPathForTests(),
                Is.EqualTo("Assets/Noodle Hammer/Preview Forge/Settings/ParticleThumbnailSettings.asset"));
        }

        [Test]
        public void BadgeResolver_ParticlePrefab_ResolvesParticleBadge()
        {
            Assert.That(
                PrefabThumbnailBadgeResolver.Resolve(PrefabThumbnailAssetKind.ParticlePrefab),
                Is.EqualTo(PrefabThumbnailBadgeType.Particle));
        }

        [Test]
        public void BadgeResolver_UiPrefab_ResolvesUiBadge()
        {
            Assert.That(
                PrefabThumbnailBadgeResolver.Resolve(PrefabThumbnailAssetKind.UiPrefab),
                Is.EqualTo(PrefabThumbnailBadgeType.Ui));
        }

        [Test]
        public void BadgeResolver_UnsupportedKind_ResolvesNoBadge()
        {
            Assert.That(
                PrefabThumbnailBadgeResolver.Resolve(PrefabThumbnailAssetKind.Unsupported),
                Is.EqualTo(PrefabThumbnailBadgeType.None));
        }

        [Test]
        public void BadgeDrawer_ListView_DoesNotDraw()
        {
            bool shouldDraw = PrefabThumbnailBadgeDrawer.ShouldDraw(
                PrefabThumbnailBadgeType.Ui,
                PrefabThumbnailSurface.ProjectWindowList,
                showGridViewBadges: true);

            Assert.That(shouldDraw, Is.False);
        }

        [Test]
        public void BadgeDrawer_DisabledToggle_DoesNotDraw()
        {
            bool shouldDraw = PrefabThumbnailBadgeDrawer.ShouldDraw(
                PrefabThumbnailBadgeType.Particle,
                PrefabThumbnailSurface.ProjectWindowGrid,
                showGridViewBadges: false);

            Assert.That(shouldDraw, Is.False);
        }

        [Test]
        public void SharedSettingsToken_DoesNotChangeWhenBadgeToggleChanges()
        {
            PrefabThumbnailSettingsStorage withBadges = ScriptableObject.CreateInstance<PrefabThumbnailSettingsStorage>();
            PrefabThumbnailSettingsStorage withoutBadges = ScriptableObject.CreateInstance<PrefabThumbnailSettingsStorage>();
            try
            {
                withBadges.ResetToDefaults();
                withoutBadges.ResetToDefaults();
                withBadges.showGridViewBadges = true;
                withoutBadges.showGridViewBadges = false;

                string withBadgesToken = PrefabThumbnailSettings.BuildPersistentSettingsToken(withBadges);
                string withoutBadgesToken = PrefabThumbnailSettings.BuildPersistentSettingsToken(withoutBadges);

                Assert.That(withBadgesToken, Is.EqualTo(withoutBadgesToken));
            }
            finally
            {
                Object.DestroyImmediate(withBadges);
                Object.DestroyImmediate(withoutBadges);
            }
        }

        [Test]
        public void UiFraming_MultiGraphicBoundsUnion_IsCorrect()
        {
            GameObject root = new GameObject("BoundsRoot", typeof(RectTransform));
            try
            {
                GameObject left = CreateImageChild(root.transform, "Left", new Vector2(100f, 50f), new Vector2(-120f, 0f));
                GameObject right = CreateImageChild(root.transform, "Right", new Vector2(80f, 120f), new Vector2(140f, 30f));
                Component[] graphics = root.GetComponentsInChildren<Graphic>(true);

                Bounds bounds = UiPrefabThumbnailRenderer.ComputeGraphicBoundsForTests(graphics, root.transform);

                Assert.That(bounds.min.x, Is.EqualTo(-170f).Within(0.01f));
                Assert.That(bounds.max.x, Is.EqualTo(180f).Within(0.01f));
                Assert.That(bounds.min.y, Is.EqualTo(-25f).Within(0.01f));
                Assert.That(bounds.max.y, Is.EqualTo(90f).Within(0.01f));

                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UiFraming_CenteredLooseElement_ComputesExpectedCenterAndSize()
        {
            GameObject root = new GameObject("CenteredRoot", typeof(RectTransform));
            try
            {
                RectTransform rectTransform = root.GetComponent<RectTransform>();
                rectTransform.anchoredPosition3D = new Vector3(30f, -20f, 0f);
                rectTransform.sizeDelta = new Vector2(160f, 40f);
                root.AddComponent<Image>();

                Component[] graphics = root.GetComponentsInChildren<Graphic>(true);
                Bounds bounds = UiPrefabThumbnailRenderer.ComputeGraphicBoundsForTests(graphics, root.transform);
                float orthographicSize = UiPrefabThumbnailRenderer.ComputeOrthographicSizeForBoundsForTests(bounds);

                Assert.That(bounds.center.x, Is.EqualTo(30f).Within(0.01f));
                Assert.That(bounds.center.y, Is.EqualTo(-20f).Within(0.01f));
                Assert.That(orthographicSize, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UiFraming_RotatedCanvas_UsesUiPlaneInsteadOfWorldAxes()
        {
            GameObject root = new GameObject("RotatedCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                RectTransform rootRectTransform = root.GetComponent<RectTransform>();
                rootRectTransform.rotation = Quaternion.Euler(90f, 0f, 0f);

                CreateImageChild(root.transform, "Panel", new Vector2(200f, 80f), Vector2.zero);
                Component[] graphics = root.GetComponentsInChildren<Graphic>(true);

                bool success = UiPrefabThumbnailRenderer.TryComputeFrameDataForTests(graphics, root.transform, out UiGraphicFrameData frameData);

                Assert.That(success, Is.True);
                Assert.That(Vector3.Dot(frameData.Forward, root.transform.forward), Is.GreaterThan(0.999f));
                Assert.That(frameData.PlaneExtents.x, Is.EqualTo(100f).Within(0.01f));
                Assert.That(frameData.PlaneExtents.y, Is.EqualTo(40f).Within(0.01f));
                Assert.That(frameData.DepthExtent, Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UiFraming_VisiblePixelScan_FindsTinyCenteredContent()
        {
            Color32[] pixels = new Color32[64 * 64];
            for (int y = 30; y < 34; y++)
            {
                for (int x = 29; x < 35; x++)
                    pixels[y * 64 + x] = new Color32(255, 255, 255, 255);
            }

            bool success = UiPrefabThumbnailRenderer.TryComputeVisiblePixelRectForTests(pixels, 64, 64, out RectInt pixelRect);

            Assert.That(success, Is.True);
            Assert.That(pixelRect.xMin, Is.EqualTo(29));
            Assert.That(pixelRect.yMin, Is.EqualTo(30));
            Assert.That(pixelRect.width, Is.EqualTo(6));
            Assert.That(pixelRect.height, Is.EqualTo(4));
        }

        [Test]
        public void UiFraming_VisiblePixelScan_FindsContentAgainstKeyedBackground()
        {
            Color32 clearPixel = new Color32(255, 0, 255, 255);
            Color32[] pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clearPixel;

            for (int y = 30; y < 34; y++)
            {
                for (int x = 29; x < 35; x++)
                    pixels[y * 64 + x] = new Color32(255, 255, 255, 255);
            }

            bool success = UiPrefabThumbnailRenderer.TryComputeVisiblePixelRectForTests(pixels, 64, 64, clearPixel, out RectInt pixelRect);

            Assert.That(success, Is.True);
            Assert.That(pixelRect.xMin, Is.EqualTo(29));
            Assert.That(pixelRect.yMin, Is.EqualTo(30));
            Assert.That(pixelRect.width, Is.EqualTo(6));
            Assert.That(pixelRect.height, Is.EqualTo(4));
        }

        [Test]
        public void UiFraming_AlphaTighten_ShrinksFrameForTinyCenteredContent()
        {
            UiPrefabThumbnailRenderer.ComputeRefinedFrameForTests(
                new Vector2(200f, 80f),
                currentOrthographicSize: 230f,
                visiblePixelRect: new RectInt(29, 30, 6, 4),
                width: 64,
                height: 64,
                out float refinedOrthographicSize,
                out Vector2 normalizedCenterOffset);

            Assert.That(refinedOrthographicSize, Is.LessThan(230f));
            Assert.That(Mathf.Abs(normalizedCenterOffset.x), Is.LessThan(0.02f));
            Assert.That(Mathf.Abs(normalizedCenterOffset.y), Is.LessThan(0.02f));
        }

        [Test]
        public void UiFraming_AlphaTighten_RecentersOffAxisContent()
        {
            UiPrefabThumbnailRenderer.ComputeRefinedFrameForTests(
                new Vector2(200f, 80f),
                currentOrthographicSize: 230f,
                visiblePixelRect: new RectInt(44, 10, 10, 8),
                width: 64,
                height: 64,
                out _,
                out Vector2 normalizedCenterOffset);

            Assert.That(normalizedCenterOffset.x, Is.GreaterThan(0.1f));
            Assert.That(normalizedCenterOffset.y, Is.LessThan(-0.1f));
        }

        [Test]
        public void UiFraming_ExtremeAspectRatio_RemainsFullyInView()
        {
            float orthographicSize = UiPrefabThumbnailRenderer.ComputeOrthographicSizeForPlaneExtentsForTests(new Vector2(200f, 20f), 1f);

            Assert.That(orthographicSize, Is.GreaterThanOrEqualTo(200f));
            Assert.That(orthographicSize, Is.GreaterThanOrEqualTo(20f));
        }

        [Test]
        public void SharedSettingsToken_ChangesWhenSharedDisplaySettingsChange()
        {
            string baseline = PrefabThumbnailSettings.BuildPersistentSettingsToken(
                enabled: true,
                drawInProjectGrid: true,
                drawInProjectList: true,
                gridRenderSize: 128,
                listRenderSize: 32,
                backgroundColor: new Color(0.15f, 0.15f, 0.15f, 1f),
                boundsPadding: 0.15f,
                cameraFov: 30f,
                cameraYaw: 35f,
                cameraPitch: 25f,
                scanMaxSeconds: 3f,
                motionPadding: 0.2f,
                motionRadius: 3f,
                motionSpeed: 60f,
                enableTightFraming: true,
                particleFramingPercentile: 0.92f,
                thumbnailFillTarget: 1f,
                maxRendersPerUpdate: 1,
                renderBudgetMs: 12f,
                memoryCacheMaxEntries: 200,
                enablePersistentCache: true);

            string changed = PrefabThumbnailSettings.BuildPersistentSettingsToken(
                enabled: true,
                drawInProjectGrid: true,
                drawInProjectList: true,
                gridRenderSize: 96,
                listRenderSize: 32,
                backgroundColor: new Color(0.15f, 0.15f, 0.15f, 1f),
                boundsPadding: 0.15f,
                cameraFov: 30f,
                cameraYaw: 35f,
                cameraPitch: 25f,
                scanMaxSeconds: 3f,
                motionPadding: 0.2f,
                motionRadius: 3f,
                motionSpeed: 60f,
                enableTightFraming: true,
                particleFramingPercentile: 0.92f,
                thumbnailFillTarget: 1f,
                maxRendersPerUpdate: 1,
                renderBudgetMs: 12f,
                memoryCacheMaxEntries: 200,
                enablePersistentCache: true);

            Assert.That(changed, Is.Not.EqualTo(baseline));
        }

        [Test]
        public void SharedSettingsToken_ChangesWhenParticleFramingSettingsChange()
        {
            string baseline = PrefabThumbnailSettings.BuildPersistentSettingsToken(
                enabled: true,
                drawInProjectGrid: true,
                drawInProjectList: true,
                gridRenderSize: 128,
                listRenderSize: 32,
                backgroundColor: new Color(0.15f, 0.15f, 0.15f, 1f),
                boundsPadding: 0.15f,
                cameraFov: 30f,
                cameraYaw: 35f,
                cameraPitch: 25f,
                scanMaxSeconds: 3f,
                motionPadding: 0.2f,
                motionRadius: 3f,
                motionSpeed: 60f,
                enableTightFraming: true,
                particleFramingPercentile: 0.92f,
                thumbnailFillTarget: 1f,
                maxRendersPerUpdate: 1,
                renderBudgetMs: 12f,
                memoryCacheMaxEntries: 200,
                enablePersistentCache: true);

            string changed = PrefabThumbnailSettings.BuildPersistentSettingsToken(
                enabled: true,
                drawInProjectGrid: true,
                drawInProjectList: true,
                gridRenderSize: 128,
                listRenderSize: 32,
                backgroundColor: new Color(0.15f, 0.15f, 0.15f, 1f),
                boundsPadding: 0.15f,
                cameraFov: 30f,
                cameraYaw: 35f,
                cameraPitch: 25f,
                scanMaxSeconds: 3f,
                motionPadding: 0.2f,
                motionRadius: 3f,
                motionSpeed: 60f,
                enableTightFraming: true,
                particleFramingPercentile: 0.97f,
                thumbnailFillTarget: 1f,
                maxRendersPerUpdate: 1,
                renderBudgetMs: 12f,
                memoryCacheMaxEntries: 200,
                enablePersistentCache: true);

            Assert.That(changed, Is.Not.EqualTo(baseline));
        }

        private static GameObject CreateImageChild(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            return gameObject;
        }
    }
}
