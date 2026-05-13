# Particle Thumbnail & Preview

Editor-only Unity tools for rendered particle thumbnails and a workflow-focused prefab preview experience.

## What This Package Provides

- `Particle Thumbnail`
  - Static Project-window thumbnails for particle prefabs.
  - Motion-aware framing for effects that emit over traveled distance.
- `Particle Preview`
  - Custom prefab preview for particle prefabs with playback controls and timeline scrubbing.
  - Model prefab preview (mesh and skinned mesh) with auto-framing, orbit, pan, zoom, and default `3D` view mode.
  - UI prefab preview for RectTransform-based prefabs (Canvas, Image, Text, Button, and TMP UGUI) with automatic Canvas wrapping when needed.
- `Editor Safety`
  - Editor-only assemblies with no runtime/player build dependency.
  - Scoped preview-hook patching with bounded retries and compatibility fallbacks.

## Compatibility

- Minimum Unity version: `2021.3` (UPM metadata baseline)
- Verified in development environment: Unity `6000.3.10f1` on macOS
- Render pipeline support target:
  - Built-in Render Pipeline
  - URP (`3D` and `2D Renderer` detection for preview compatibility safeguards)
  - HDRP

## Performance and Safety Notes

- No runtime update loops or player-side hooks are added using Harmony Patcher.
- Preview update hooks are scoped and unsubscribed when not needed.

## Third-Party Inspector / Preview Coexistence

This package is designed to coexist with other editor tools, including Odin Inspector and custom `CustomPreview` workflows.

If you see preview ownership conflicts:

1. Open `Project Settings > Particle Thumbnail & Preview`
2. Temporarily disable Particle Preview
3. Adjust tool registration order / integration settings
4. Re-enable Particle Preview and recheck prefab inspector behavior

## Installation (Git UPM)

1. Open `Window > Package Manager`
2. Click `+`
3. Select `Add package from git URL...`
4. Paste:

```text
https://github.com/FardinHaque70/Particle-Thumbnail-And-Preview.git?path=upm/src#main
```

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.fardinhaque.particle-thumbnail-preview": "https://github.com/FardinHaque70/Particle-Thumbnail-And-Preview.git?path=upm/src#main"
  }
}
```

Unity Editor restart is recommended after first install so preview hook initialization is clean.

### Unity Asset Store Package

- Import the package into your project from the Unity Package Manager (`My Assets`) or from a provided `.unitypackage`.
- After import, open `Project Settings > Particle Thumbnail & Preview` to confirm defaults for your project.
- Restart Unity once after first import so preview hook initialization is clean.

## Usage

1. Import or select a particle prefab in the Project window.
2. Open the prefab Inspector preview to use playback and scrubbing controls.
3. For mesh/skinned prefabs, use model preview controls for orbit/pan/zoom and visual mode toggles.
4. Use package project settings for thumbnail cache maintenance, regeneration, and preview behavior defaults.

## Limitations

- VFX Graph thumbnails are not currently supported.
- In URP, particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews. This is not supported by the current preview rendering path.
