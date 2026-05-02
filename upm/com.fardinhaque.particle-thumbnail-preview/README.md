# Particle Thumbnail & Preview

Editor-only Unity tools for clearer particle prefab thumbnails and richer prefab-focused Inspector preview workflows.

## Package Includes

- `Particle Thumbnail`
  Custom Project window thumbnails for particle prefabs, including better framing for effects with motion.
- `Particle Preview`
  A dedicated custom prefab preview in the Inspector:
  - Particle-focused preview for root-particle prefabs (playback controls, timeline scrubbing, motion tools).
  - Lean model preview for mesh/skinned prefabs with robust auto-framing, orbit/pan/zoom, and Auto/2D/3D mode switching.
  Uses scoped Harmony patching for preview hooking so updates stay targeted without repainting the whole Inspector.

## Features

- Static particle thumbnails in Project grid and list views with motion-aware framing for clearer asset recognition.
- Dedicated custom prefab Inspector preview with particle playback controls and model preview workflows.
- Project-scoped settings and maintenance actions for cache control, thumbnail regeneration, and preview behavior tuning.
- Scoped Harmony patching is used only for preview-window hook behavior, minimizing impact so Odin Inspector and other editor extension tools should keep working smoothly.

## Tested Environment

- Unity `6000.3.10f1`
- Editor platform used during development: macOS

## Installation (Git UPM)

1. Open `Window > Package Manager`
2. Click the `+` button
3. Choose `Add package from git URL...`
4. Paste:

```text
https://github.com/FardinHaque70/ParticleThumbnail-Preview.git?path=/upm/com.fardinhaque.particle-thumbnail-preview#main
```

After installation, a Unity Editor restart is recommended so the preview window hook initializes properly.

Or add in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.fardinhaque.particle-thumbnail-preview": "https://github.com/FardinHaque70/ParticleThumbnail-Preview.git?path=/upm/com.fardinhaque.particle-thumbnail-preview#main"
  }
}
```

## Notes

- This is an editor-only toolset and does not affect player builds.
- VFX Graph thumbnails are not currently supported.
