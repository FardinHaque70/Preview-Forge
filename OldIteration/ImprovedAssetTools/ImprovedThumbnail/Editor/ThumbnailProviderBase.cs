using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public abstract class ThumbnailProviderBase
{
    public abstract string Id { get; }
    public abstract ThumbnailAssetKind AssetKind { get; }
    public abstract int Priority { get; }

    public ThumbnailSupportInfo GetSupportInfo(string guid, string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !SupportsAssetPath(assetPath))
            return ThumbnailSupportInfo.Unsupported;

        UnityEngine.Object asset = LoadAssetObject(assetPath);
        if (asset == null)
            return ThumbnailSupportInfo.Unsupported;

        return EvaluateSupport(asset, guid, assetPath);
    }

    public UnityEngine.Object LoadAssetObject(string assetPath)
    {
        return string.IsNullOrEmpty(assetPath) ? null : LoadAsset(assetPath);
    }

    public ThumbnailCacheRecord Render(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        ThumbnailFrameSet frames = RenderFrames(context, renderContext);
        if (frames == null)
            return null;

        return new ThumbnailCacheRecord
        {
            Guid = context.Guid,
            AssetPath = context.AssetPath,
            DependencyHash = context.GetDependencyHash(),
            AssetKind = context.AssetKind,
            Frames = frames,
        };
    }

    protected virtual bool SupportsAssetPath(string assetPath)
    {
        return ThumbnailAssetPathUtility.IsPrefabAssetPath(assetPath);
    }

    protected virtual UnityEngine.Object LoadAsset(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    protected abstract ThumbnailSupportInfo EvaluateSupport(UnityEngine.Object asset, string guid, string assetPath);
    protected abstract ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext);
}

}
