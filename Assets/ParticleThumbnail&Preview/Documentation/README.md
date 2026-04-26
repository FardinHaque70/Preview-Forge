# Particle Thumbnail & Preview

Editor-only Unity tools for clearer particle prefab thumbnails and a richer particle-focused Inspector preview workflow.

## Package Includes

- `Particle Thumbnail`
  Custom Project window thumbnails for particle prefabs, including better framing for effects with motion.
- `Particle Preview`
  A dedicated particle prefab preview in the Inspector with playback controls, timeline scrubbing, and camera interaction.

## Why This Tool Exists

Unity's default prefab icon and preview flow usually does not communicate particle behavior clearly at a glance. That makes browsing VFX prefabs slower than it should be, especially in larger projects.

This toolset is built to improve that workflow by:

- rendering recognizable particle thumbnails directly in Project view
- providing a particle-first preview experience directly in the Inspector
- exposing project-scoped settings so teams can tune behavior without code changes
- keeping the package editor-only and focused on authoring-time productivity

## Features

- Static particle thumbnails in Project grid and list views with motion-aware framing for clearer asset recognition.
- Dedicated particle prefab Inspector preview with play/pause/scrub controls, orbit/pan/zoom interaction, and quick info overlays.
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
