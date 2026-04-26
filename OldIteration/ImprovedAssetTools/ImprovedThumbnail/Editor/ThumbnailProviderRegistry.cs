using System.Collections.Generic;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ThumbnailProviderRegistry
{
    private static readonly List<ThumbnailProviderBase> s_providers = new();
    private static bool s_providerOrderDirty;

    public static IReadOnlyList<ThumbnailProviderBase> Providers => s_providers;

    public static void Register(ThumbnailProviderBase provider)
    {
        if (provider == null || s_providers.Contains(provider))
            return;

        s_providers.Add(provider);
        s_providerOrderDirty = true;
        EnsureSorted();
    }

    public static void Unregister(ThumbnailProviderBase provider)
    {
        if (provider == null)
            return;

        if (s_providers.Remove(provider))
            s_providerOrderDirty = true;
    }

    public static ThumbnailProviderBase FindBestProvider(string guid, string assetPath, out ThumbnailSupportInfo supportInfo)
    {
        EnsureSorted();
        ThumbnailProviderBase bestProvider = null;
        ThumbnailSupportInfo bestSupport = ThumbnailSupportInfo.Unsupported;

        for (int i = 0; i < s_providers.Count; i++)
        {
            ThumbnailProviderBase provider = s_providers[i];
            ThumbnailSupportInfo support = provider.GetSupportInfo(guid, assetPath);
            if (!support.Supported)
                continue;

            if (bestProvider == null || support.Priority < bestSupport.Priority)
            {
                bestProvider = provider;
                bestSupport = support;
            }
        }

        supportInfo = bestSupport;
        return bestProvider;
    }

    public static void SortProviders()
    {
        s_providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        s_providerOrderDirty = false;
    }

    private static void EnsureSorted()
    {
        if (!s_providerOrderDirty)
            return;

        SortProviders();
    }
}

}
