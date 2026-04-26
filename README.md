<div align="center">

# Particle Thumbnail & Preview

Editor-only Unity tools for clearer particle prefab thumbnails and a richer particle-focused Inspector preview workflow.

[![Unity 2021+](https://img.shields.io/badge/Unity-6000.3%2B-black?style=for-the-badge&logo=unity)](#tested-environment)
[![Git UPM](https://img.shields.io/badge/Install-Git%20UPM-2ea44f?style=for-the-badge)](#installation)

</div>

This package currently includes:

- `Particle Thumbnail`
  Custom Project window thumbnails for particle prefabs, including better framing for effects with motion.
- `Particle Preview`
  A dedicated particle prefab preview in the Inspector with playback controls, timeline scrubbing, and camera interaction. Uses scoped Harmony patching for preview hooking so updates stay targeted without repainting the whole Inspector.

This repository contains the Unity source project and a synced UPM package for Git-based installation.

## See It In Action

### 1. No More Blue Cubes

Particle prefabs are much easier to recognize when the Project window shows a real static thumbnail instead of Unity's default blue prefab cube.

<p align="center">
  <img src=".github/readme-media/no-more-blue-cubes.gif" alt="Static particle thumbnails in the Unity Project window" width="900" />
</p>

### 2. Better Particle Preview Controls

Particle prefabs can be inspected directly in the preview window, so you do not need to open the prefab or place it in the scene just to see how the effect looks.

<p align="center">
  <img src=".github/readme-media/better-particle-preview-controls.gif" alt="Particle prefab preview controls in the Unity Inspector" width="720" />
</p>

## Features

- Static particle thumbnails in Project grid and list views with motion-aware framing for clearer asset recognition.
- Dedicated particle prefab Inspector preview with play/pause/scrub controls, orbit/pan/zoom interaction, and quick info overlays.
- Project-scoped settings and maintenance actions for cache control, thumbnail regeneration, and preview behavior tuning.
- Scoped Harmony patching is used only for preview-window hook behavior, minimizing impact so Odin Inspector and other editor extension tools should keep working smoothly.

## Tested Environment

- Unity `6000.3.10f1`
- Editor platform used during development: macOS

## Installation

### From Git URL (UPM)

In Unity:

1. Open `Window > Package Manager`
2. Click the `+` button
3. Choose `Add package from git URL...`
4. Paste this URL:

```text
https://github.com/FardinHaque70/ParticleThumbnail-Preview.git?path=/upm/com.fardinhaque.particle-thumbnail-preview#main
```

After installation, a Unity Editor restart is recommended so the preview window hook initializes properly.

You can also add it directly in `Packages/manifest.json`:

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
