<div align="center">

<h1>Preview Forge</h1>

Thumbnails and Custom Previews for Unity.

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](#compatibility)
[![Pipelines](https://img.shields.io/badge/Pipelines-Built--in%20%7C%20URP%20%7C%20HDRP-6f42c1?style=flat-square)](#compatibility)
[![Git UPM](https://img.shields.io/badge/Install-Git%20UPM-2ea44f?style=flat-square)](#installation)
[![Support](https://img.shields.io/badge/Support-Unity%20Asset%20Store-222c37?style=flat-square&logo=unity&logoColor=white)]([https://assetstore.unity.com/preview/370342/1347096](https://assetstore.unity.com/packages/slug/370342))

</div>

> [!TIP]
> I want this tool to stay accessible to everyone, so the **free GitHub version** and the **paid Unity Asset Store version** include the exact same toolset.
> If you'd like to support my work and ongoing development, you can pick it up on the [Unity Asset Store](https://assetstore.unity.com/preview/370342/1347096). Thanks!

This package focuses on three core workflows:

- `Particle Thumbnail`  
  Static rendered Project window thumbnails for particle prefabs, so effects are easier to identify at a glance.
- `Prefab and 3D Asset Preview`  
  A richer Inspector preview for prefabs and 3D assets with better lighting, view modes, and smoother camera control.
- `Particle Preview`  
  A playback-focused preview workflow for particle prefabs with scrubbing and motion-aware inspection.

## See It In Action

### 1. Particle Thumbnails and Custom Preview

Particle prefabs are easier to browse when the Project window shows real thumbnails and a built-in custom preview workflow, so you can inspect effects directly from the Project window and Inspector.

<table width="100%">
  <tr>
    <td align="center" width="60%">
      <img src=".github/readme-media/particle-thumbnail-preview-showcase.gif" alt="Particle thumbnails and custom preview showcase" width="100%" />
    </td>
    <td align="center" width="40%">
      <img src=".github/readme-media/better-particle-preview-controls.gif" alt="Particle preview controls in the Unity Inspector" width="100%" />
    </td>
  </tr>
</table>

### 2. Better Prefab Preview

Prefabs with 3D models, along with 3D asset files such as FBX and Blend, get a more practical preview workflow with improved lighting, helpful view modes like Normal and UV, and extra stats such as triangle and material counts.

<p align="center">
  <img src=".github/readme-media/general-prefab-preview.gif" alt="General prefab preview with orbit and framing controls in the Unity Inspector" width="720" />
</p>

## Feature Overview

### Particle Thumbnail

- Static rendered thumbnails for particle prefabs
- Motion-aware framing for effects that emit over traveled distance
- Thumbnails are cached inside `Library/Noodle Hammer/Preview Forge/ParticleThumbnailCache`

### Prefab Preview

- Much improved lighting that includes shadow casting directional light and a 3-point lighting rig
- Better camera control with orbit, pan, zoom, and auto-framing controls
- View mode toggles like Normal, UV, Vertex Color, Matcap, and Overdraw
- Visualize box and sphere colliders

### Particle Preview

- Custom preview window automatically opens for the selected particle prefab
- Includes particle timeline scrubbing and motion path support for systems that need motion


## Compatibility

- Minimum Unity version: `2021.3`
- Developed and verified in Unity `6000.3.10f1` on macOS
- Targeted render pipeline support: Built-in Render Pipeline, URP, HDRP

## Installation

> [!NOTE]
> Choose the install path that matches how you want to use the tool: `Git UPM` for package-based installs, or `Unity Asset Store` for project import workflows.

### Git UPM

Best for package-based installs and versioned repository usage.

1. Open `Window > Package Manager`
2. Click `+`
3. Select `Add package from git URL...`
4. Paste:

```text
https://github.com/FardinHaque70/Preview-Forge.git?path=upm/src#main
```

Or add it directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.noodlehammer.preview-forge": "https://github.com/FardinHaque70/Preview-Forge.git?path=upm/src#main"
  }
}
```

Restarting the Unity Editor once after first install is recommended so the preview window hooks initialize cleanly.

## Notes

- This is an editor-only package.
- The preview integration uses a scoped Harmony patcher designed to coexist with other editor tools like Odin Inspector.

## Known Limitations

- VFX Graph thumbnails are not currently supported.
- In URP, particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews. This is not supported by the current preview rendering path.
- A Unity Editor restart may be needed after first install or import for smoother preview window hook initialization.
