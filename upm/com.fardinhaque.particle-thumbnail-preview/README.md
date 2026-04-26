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

### Particle Thumbnail

- Custom thumbnails for particle prefabs in both Project grid and list modes
- Motion-aware framing logic that better captures the visible area of dynamic effects
- Asynchronous queued rendering with configurable per-update limits to keep the editor responsive
- In-memory LRU cache plus persistent disk cache for faster repeat browsing
- Automatic invalidation when prefab assets are imported, moved, changed, or deleted
- One-click maintenance actions:
  `Tools/Particle Thumbnail/Clear Memory Cache`
  `Tools/Particle Thumbnail/Clear Persistent Cache`
  `Tools/Particle Thumbnail/Rebuild Visible Thumbnails`
  `Tools/Particle Thumbnail/Generate All Thumbnails`
  `Assets/Particle Thumbnail/Regenerate Thumbnail`

### Particle Preview

- Custom Inspector preview for supported particle prefabs
- Playback toolbar with play/pause, restart, and timeline scrubber
- Orbit, pan, and zoom controls with smoothing and motion assist settings
- Automatic camera framing designed for both static and highly dynamic particle systems
- Optional overlays for quick inspection:
  playback time and duration
  peak visible particle count
  sub-particle-system count
  grid toggle for spatial context
- Auto-selection of the custom preview for supported targets
- Foldout-collapsing helper to reduce Inspector UI conflicts while previewing

### Settings and Compatibility

- Project Settings integration:
  `Project/Particle Thumbnail & Preview/Particle Thumbnails`
  `Project/Particle Thumbnail & Preview/Particle Preview`
- Settings are stored in `ProjectSettings/ParticleThumbnailAndPreview`
- Render compatibility helpers include fallback paths for Built-in RP and SRP preview rendering

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
- The UPM package intentionally excludes internal test files.
