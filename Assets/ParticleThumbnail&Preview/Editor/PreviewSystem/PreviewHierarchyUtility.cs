using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewHierarchyUtility
    {
        internal static void ForceActivateHierarchy(GameObject root)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && !transform.gameObject.activeSelf)
                    transform.gameObject.SetActive(true);
            }
        }
    }
}
