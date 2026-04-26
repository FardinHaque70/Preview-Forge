using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ThumbnailCacheUtility
{
    public static void DestroyRecordTextures(ThumbnailCacheRecord record)
    {
        if (record == null || record.Frames == null)
            return;

        if (record.Frames.StaticFrame != null)
            Object.DestroyImmediate(record.Frames.StaticFrame);
    }
}

}
