using System;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal interface IPrefabPreviewImplementation
    {
        PrefabPreviewTargetKind Kind { get; }
        void SetRepaintCallback(Action repaintCallback);
        bool EnsureReady(GameObject prefab);
        void Cleanup(bool selectionIsEmpty);
        void Draw(Rect rect, GUIStyle background, bool isInteractive);
        GUIContent GetPreviewTitle(GameObject prefab);
    }
}
