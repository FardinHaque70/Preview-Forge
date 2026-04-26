using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class ThumbnailRenderContext : IDisposable
{
    private PreviewRenderUtility _preview;
    private RenderTexture _captureRT;
    private Texture2D _readTexture;
    private GameObject _currentInstance;
    private int _previewSize;
    private Material _thumbnailSkyboxCubemapMaterial;
    private Material _thumbnailSkyboxPanoramicMaterial;
    private bool _renderEnvironmentPushed;
    private Material _previousSkyboxMaterial;
    private AmbientMode _previousAmbientMode;
    private Color _previousAmbientLight;
    private DefaultReflectionMode _previousDefaultReflectionMode;
    private Texture _previousCustomReflectionTexture;

    public PreviewRenderUtility Preview => _preview;
    public GameObject CurrentInstance => _currentInstance;

    public void EnsureInfrastructure(int thumbnailSize, Color backgroundColor, float fieldOfView, float primaryLightIntensity, float secondaryLightIntensity)
    {
        if (_preview != null && _previewSize == thumbnailSize)
        {
            ApplyCameraSettings(backgroundColor, fieldOfView, primaryLightIntensity, secondaryLightIntensity);
            return;
        }

        ReleaseRenderTargets();

        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }

        _preview = new PreviewRenderUtility(true);
        _preview.camera.clearFlags = CameraClearFlags.SolidColor;
        _preview.camera.nearClipPlane = 0.01f;
        _preview.camera.farClipPlane = 1000f;

        _previewSize = thumbnailSize;
        ApplyCameraSettings(backgroundColor, fieldOfView, primaryLightIntensity, secondaryLightIntensity);
    }

    public void SetInstance(GameObject instance)
    {
        _currentInstance = instance;
    }

    public Texture2D CapturePreview(int thumbnailSize)
    {
        if (_preview == null)
            return null;

        EnsureRenderTargets(thumbnailSize);

        bool previousSrgb = GL.sRGBWrite;

        // EndPreview must be called first — it internally restores RenderTexture.active to whatever
        // was active before BeginPreview. Capturing previousActive before EndPreview would snapshot
        // the preview's own RT and restore it in the finally, causing "Releasing render texture that
        // is set to be RenderTexture.active" warnings on the next Cleanup().
        Texture result = _preview.EndPreview();
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Graphics.Blit(result, _captureRT);

            RenderTexture.active = _captureRT;
            _readTexture.ReadPixels(new Rect(0f, 0f, thumbnailSize, thumbnailSize), 0, 0);
            _readTexture.Apply(false, false);

            Texture2D output = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            output.SetPixels32(_readTexture.GetPixels32());
            output.Apply(false, false);
            return output;
        }
        finally
        {
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previousActive;
        }
    }

    public void RenderPreviewCamera(bool applyThumbnailEnvironment = true)
    {
        if (_preview?.camera == null)
            return;

        if (!applyThumbnailEnvironment)
        {
            _preview.camera.Render();
            return;
        }

        PushRenderEnvironment();
        try
        {
            _preview.camera.Render();
        }
        finally
        {
            PopRenderEnvironment();
        }
    }

    public void Dispose()
    {
        PopRenderEnvironment();

        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }

        if (_thumbnailSkyboxCubemapMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(_thumbnailSkyboxCubemapMaterial);
            _thumbnailSkyboxCubemapMaterial = null;
        }

        if (_thumbnailSkyboxPanoramicMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(_thumbnailSkyboxPanoramicMaterial);
            _thumbnailSkyboxPanoramicMaterial = null;
        }

        ReleaseRenderTargets();
        _currentInstance = null;
        _previewSize = 0;
        _renderEnvironmentPushed = false;
    }

    private void ApplyCameraSettings(Color backgroundColor, float fieldOfView, float primaryLightIntensity, float secondaryLightIntensity)
    {
        _preview.camera.backgroundColor = backgroundColor;
        _preview.camera.clearFlags = CameraClearFlags.SolidColor;
        _preview.cameraFieldOfView = fieldOfView;
        _preview.lights[0].intensity = primaryLightIntensity;
        _preview.lights[1].intensity = secondaryLightIntensity;
    }

    private void PushRenderEnvironment()
    {
        if (_renderEnvironmentPushed || _preview?.camera == null)
            return;

        _renderEnvironmentPushed = true;

        _previousSkyboxMaterial = RenderSettings.skybox;
        _previousAmbientMode = RenderSettings.ambientMode;
        _previousAmbientLight = RenderSettings.ambientLight;
        _previousDefaultReflectionMode = RenderSettings.defaultReflectionMode;
        _previousCustomReflectionTexture = GetCustomReflectionTextureSafe();

        Texture thumbnailSkyboxTexture = ImprovedThumbnailSettings.ThumbnailSkyboxTexture;
        if (thumbnailSkyboxTexture == null)
            return;

        Material skyboxMaterial;
        if (thumbnailSkyboxTexture is Cubemap thumbnailSkyboxCubemap)
        {
            skyboxMaterial = GetOrCreateCubemapSkyboxMaterial();
            if (skyboxMaterial == null)
                return;

            skyboxMaterial.SetTexture("_Tex", thumbnailSkyboxCubemap);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = thumbnailSkyboxCubemap;
        }
        else
        {
            skyboxMaterial = GetOrCreatePanoramicSkyboxMaterial();
            if (skyboxMaterial == null)
                return;

            skyboxMaterial.SetTexture("_MainTex", thumbnailSkyboxTexture);
            if (skyboxMaterial.HasProperty("_Mapping"))
                skyboxMaterial.SetFloat("_Mapping", 1f); // Latitude-longitude
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.customReflectionTexture = null;
        }

        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Skybox;
    }

    private void PopRenderEnvironment()
    {
        if (!_renderEnvironmentPushed || _preview?.camera == null)
            return;

        _renderEnvironmentPushed = false;

        RenderSettings.skybox = _previousSkyboxMaterial;
        RenderSettings.ambientMode = _previousAmbientMode;
        RenderSettings.ambientLight = _previousAmbientLight;
        RenderSettings.defaultReflectionMode = _previousDefaultReflectionMode;
        RenderSettings.customReflectionTexture = _previousCustomReflectionTexture;
    }

    private static Texture GetCustomReflectionTextureSafe()
    {
        try
        {
            return RenderSettings.customReflectionTexture;
        }
        catch (System.ArgumentException)
        {
            return null;
        }
    }

    private Material GetOrCreateCubemapSkyboxMaterial()
    {
        if (_thumbnailSkyboxCubemapMaterial != null)
            return _thumbnailSkyboxCubemapMaterial;

        Shader shader = Shader.Find("Skybox/Cubemap");
        if (shader == null)
            return null;

        _thumbnailSkyboxCubemapMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return _thumbnailSkyboxCubemapMaterial;
    }

    private Material GetOrCreatePanoramicSkyboxMaterial()
    {
        if (_thumbnailSkyboxPanoramicMaterial != null)
            return _thumbnailSkyboxPanoramicMaterial;

        Shader shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
            return null;

        _thumbnailSkyboxPanoramicMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return _thumbnailSkyboxPanoramicMaterial;
    }

    private void EnsureRenderTargets(int thumbnailSize)
    {
        bool needsRT = _captureRT == null || _captureRT.width != thumbnailSize || _captureRT.height != thumbnailSize;
        bool needsReadTexture = _readTexture == null || _readTexture.width != thumbnailSize || _readTexture.height != thumbnailSize;

        if (needsRT)
        {
            if (_captureRT != null)
            {
                _captureRT.Release();
                UnityEngine.Object.DestroyImmediate(_captureRT);
            }

            _captureRT = new RenderTexture(thumbnailSize, thumbnailSize, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _captureRT.Create();
        }

        if (needsReadTexture)
        {
            if (_readTexture != null)
                UnityEngine.Object.DestroyImmediate(_readTexture);

            _readTexture = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private void ReleaseRenderTargets()
    {
        if (_captureRT != null)
        {
            _captureRT.Release();
            UnityEngine.Object.DestroyImmediate(_captureRT);
            _captureRT = null;
        }

        if (_readTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(_readTexture);
            _readTexture = null;
        }
    }
}

}
