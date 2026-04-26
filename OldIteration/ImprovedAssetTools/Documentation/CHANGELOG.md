# Changelog

## 1.1.0 - 2026-04-18

- Added turntable auto-rotation button to 3D model and material previews (on by default, first button in each toolbar).
- Turntable disables automatically when the user manually orbits or pans the camera.
- Particle preview now plays continuously without interruption regardless of which Unity window has focus.
- Fixed preview repaint loop breaking mid-orbit-drag due to Unity's `mouseOverWindow` being transiently null during a drag gesture (500 ms recent-interaction grace period).
- Fixed input drops and slider-drag glitches in Project Settings: camera-lerp and turntable renders stop after 500 ms of no preview engagement, while particle playback remains continuous.
- Scoped all preview repaints to Inspector windows only — no other Unity windows are repainted by preview activity.

## 1.0.0 - 2026-03-29

- Added production packaging metadata with an editor assembly definition.
- Added in-project documentation for installation, usage, and package notes.
- Added Project Settings integration for both Improved Thumbnail and Improved Preview.
- Made the UI thumbnail provider optional so clean imports still compile when `com.unity.ugui` is not installed.
- Fixed thumbnail invalidation so imported model assets are refreshed alongside prefabs.
- Fixed provider priority changes so they take effect immediately instead of waiting for a domain reload.
- Added separate model-provider enablement and priority controls.
- Limited model-only framing options so they do not accidentally affect regular prefab thumbnails.
- Added orphaned persistent-cache cleanup for deleted source assets.
