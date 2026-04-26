using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class ModelThumbnailProvider : ThumbnailProviderBase
{
    public override string Id => "model-asset";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.ModelAsset;
    public override int Priority => ImprovedThumbnailSettings.ModelThumbnailProviderPriority;

    protected override bool SupportsAssetPath(string assetPath)
    {
        return ThumbnailAssetPathUtility.IsModelAssetPath(assetPath);
    }

    protected override ThumbnailSupportInfo EvaluateSupport(Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableModelThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        GameObject prefab = asset as GameObject;
        if (prefab == null)
            return ThumbnailSupportInfo.Unsupported;

        bool supported = prefab.GetComponentInChildren<MeshRenderer>(true) != null
            || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

        return supported
            ? new ThumbnailSupportInfo(true, ThumbnailAssetKind.ModelAsset, Priority)
            : ThumbnailSupportInfo.Unsupported;
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        return GeneralPrefabThumbnailProvider.RenderMeshThumbnail(
            context,
            renderContext,
            ImprovedThumbnailSettings.ModelThumbnailYaw,
            ImprovedThumbnailSettings.ModelThumbnailPitch,
            ImprovedThumbnailSettings.ModelThumbnailLightIntensity,
            ImprovedThumbnailSettings.ModelThumbnailBoundsPadding,
            ImprovedThumbnailSettings.ModelThumbnailVerticalBias);
    }
}

}
