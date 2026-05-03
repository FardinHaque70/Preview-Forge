using System;
using UnityEngine;
// Defines the shared preview implementation contract used by prefab preview hosts to initialize, draw, and clean up sessions predictably.

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
