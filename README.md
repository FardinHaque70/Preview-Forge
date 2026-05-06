<div align="center">

# Particle Thumbnail & Preview

Editor-only Unity tools for clearer particle thumbnails and a more useful prefab preview workflow.

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black?style=for-the-badge&logo=unity)](#compatibility)
[![Git UPM](https://img.shields.io/badge/Install-Git%20UPM-2ea44f?style=for-the-badge)](#installation)

</div>

> [!TIP]
> I believe tools that make development easier should be available to **everyone**, so the **free GitHub version** and the **paid Asset Store version** include the same toolset.
> If you'd like to support **ongoing development**, you can also pick it up on the [Unity Asset Store](https://assetstore.unity.com/preview/370342/1307444). I genuinely appreciate the support.

This package focuses on two things:

- `Particle Thumbnail`  
  Static rendered Project window thumbnails for particle prefabs, so effects are easier to identify at a glance.
- `Improved Preview Window`  
  A richer Inspector preview with better lighting, environment options, floor and grid support, particle playback, and smoother camera control.


## See It In Action

### 1. No More Blue Cubes

Particle prefabs are easier to browse when the Project window shows a real rendered thumbnail instead of Unity's default prefab cube.

<p align="center">
  <img src=".github/readme-media/no-more-blue-cubes.gif" alt="Rendered particle thumbnails in the Unity Project window" width="720" />
</p>

### 2. Better Prefab Preview

Prefabs with 3D models, along with 3D asset files such as FBX and Blend, get a more practical preview workflow with improved lighting, helpful view modes like Normal and UV, and extra stats such as triangle and material counts.

<p align="center">
  <img src=".github/readme-media/general-prefab-preview.gif" alt="General prefab preview with orbit and framing controls in the Unity Inspector" width="720" />
</p>

### 3. Better Particle Preview Controls

Particle prefabs can be previewed directly in the preview window, so you do not need to open the prefab or drop it into the scene just to inspect the effect.

<table>
  <tr>
    <td align="center">
      <img src=".github/readme-media/better-particle-preview-controls.gif" alt="Particle preview controls in the Unity Inspector" width="100%" />
    </td>
    <td align="center">
      <img src=".github/readme-media/particle-thumbnail-preview-showcase.gif" alt="Particle thumbnail and preview showcase" width="100%" />
    </td>
  </tr>
</table>

## Feature Overview

### Project Window Thumbnails

- Static rendered thumbnails for particle prefabs
- Motion-aware framing for effects that emit over traveled distance
- Thumbnails are cached inside `Library/ParticleThumbnailCache`

### Custom Preview Window for Prefabs and 3D Assets

- Much improved lighting that includes shadow casting directional light and a 3-point lighting rig
- Better camera control with orbit, pan, zoom, and auto-framing controls
- View mode toggles like Normal, UV, Vertex Color, Matcap, and Overdraw
- Visualize box and sphere colliders

### Preview Window for Particle Prefabs

- Custom preview window automatically opens for the selected particle prefab
- Includes particle timeline scrubbing and motion path support for systems that need motion

### Notes

- Editor-only assemblies with no runtime build dependency
- The custom preview window is integrated through a scoped Harmony patcher
- Scoped preview integration designed to coexist with other editor tools like Odin Inspector
- Centralized settings under `Project Settings > Particle Thumbnail & Preview`

## Compatibility

- Minimum Unity version: `2021.3`
- Developed and verified in Unity `6000.3.10f1` on macOS
- Targeted render pipeline support: Built-in Render Pipeline, URP, HDRP

## Installation

### Git UPM

In Unity:

1. Open `Window > Package Manager`
2. Click `+`
3. Select `Add package from git URL...`
4. Paste:

```text
https://github.com/FardinHaque70/Particle-Thumbnail-And-Preview.git?path=upm/src#main
```

Or add it directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.fardinhaque.particle-thumbnail-preview": "https://github.com/FardinHaque70/Particle-Thumbnail-And-Preview.git?path=upm/src#main"
  }
}
```

Restarting Unity once after first install is recommended so preview initialization starts cleanly.

### Unity Asset Store Package

- Get it from the [Unity Asset Store](https://assetstore.unity.com/preview/370342/1307444)
- Import the package from `My Assets` or from a provided `.unitypackage`
- Open `Project Settings > Particle Thumbnail & Preview` to review defaults for your project
- Restart Unity once after first import

## Notes

- This is an editor-only package and does not target runtime or player features.
- Git UPM installs use `upm/src` as the package root.
- Asset Store imports use `Assets/ParticleThumbnail&Preview` as the package root.

## Limitations

- VFX Graph thumbnails are not currently supported.
