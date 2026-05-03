using System;
using UnityEngine;
// Provides an isolated model-importer preview surface while reusing the existing model preview rendering/session stack.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ModelImporterPreviewImplementation
    {
        private readonly ModelPrefabPreviewImplementation _modelPreviewImplementation = new();
        private GameObject _activeModelRoot;
        private AnimationClip _activeAnimationClip;
        private bool _sessionActive;
        private int _lastLoggedRootInstanceId;
        private int _lastLoggedClipInstanceId = int.MinValue;
        private bool _lastReadyLogWasFailure;

        #region Lifecycle
        internal void SetRepaintCallback(Action repaintCallback)
        {
            _modelPreviewImplementation.SetRepaintCallback(repaintCallback);
        }

        internal bool EnsureReady(GameObject modelRoot, AnimationClip animationClip)
        {
            if (modelRoot == null)
                return false;

            _activeModelRoot = modelRoot;
            _activeAnimationClip = animationClip;
            bool ready = _modelPreviewImplementation.EnsureReady(modelRoot);
            if (!ready)
            {
                if (!_lastReadyLogWasFailure
                    || _lastLoggedRootInstanceId != modelRoot.GetInstanceID()
                    || _lastLoggedClipInstanceId != (animationClip != null ? animationClip.GetInstanceID() : 0))
                {
                    PreviewDiagnostics.Warn("ModelImporterImpl", $"EnsureReady failed root='{modelRoot.name}' clip='{(animationClip != null ? animationClip.name : "<none>")}'");
                    _lastReadyLogWasFailure = true;
                    _lastLoggedRootInstanceId = modelRoot.GetInstanceID();
                    _lastLoggedClipInstanceId = animationClip != null ? animationClip.GetInstanceID() : 0;
                }

                return false;
            }

            _sessionActive = true;
            _modelPreviewImplementation.SetPreviewAnimationClip(animationClip);
            int rootInstanceId = modelRoot.GetInstanceID();
            int clipInstanceId = animationClip != null ? animationClip.GetInstanceID() : 0;
            if (_lastReadyLogWasFailure
                || _lastLoggedRootInstanceId != rootInstanceId
                || _lastLoggedClipInstanceId != clipInstanceId)
            {
                PreviewDiagnostics.Log("ModelImporterImpl", $"EnsureReady root='{modelRoot.name}' clip='{(animationClip != null ? animationClip.name : "<none>")}'");
                _lastReadyLogWasFailure = false;
                _lastLoggedRootInstanceId = rootInstanceId;
                _lastLoggedClipInstanceId = clipInstanceId;
            }

            return true;
        }

        internal void Cleanup(bool clearSessionCache)
        {
            if (!_sessionActive)
                return;

            _activeModelRoot = null;
            _activeAnimationClip = null;
            _lastLoggedRootInstanceId = 0;
            _lastLoggedClipInstanceId = int.MinValue;
            _lastReadyLogWasFailure = false;
            _sessionActive = false;
            _modelPreviewImplementation.Cleanup(selectionIsEmpty: clearSessionCache);
        }
        #endregion

        #region Rendering
        internal GUIContent GetPreviewTitle()
        {
            string name = _activeAnimationClip != null ? _activeAnimationClip.name : (_activeModelRoot != null ? _activeModelRoot.name : null);
            return string.IsNullOrEmpty(name) ? new GUIContent("Model Importer Preview") : new GUIContent(name);
        }

        internal void Draw(Rect rect, GUIStyle background, bool isInteractive)
        {
            if (_activeModelRoot == null)
                return;

            _modelPreviewImplementation.Draw(rect, background, isInteractive);
        }
        #endregion
    }
}
