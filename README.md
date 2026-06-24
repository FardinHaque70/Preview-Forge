<div align="center">

<h1>Preview Forge</h1>

Thumbnails and Custom Previews for Unity.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-black?style=flat-square&logo=unity)](#compatibility)
[![Pipelines](https://img.shields.io/badge/Pipelines-Built--in%20%7C%20URP%20%7C%20HDRP-6f42c1?style=flat-square)](#compatibility)
[![Git UPM](https://img.shields.io/badge/Install-Git%20UPM-2ea44f?style=flat-square)](#installation)
[![Support](https://img.shields.io/badge/Support-Unity%20Asset%20Store-222c37?style=flat-square&logo=unity&logoColor=white)](https://assetstore.unity.com/packages/slug/370342)

</div>

> [!TIP]
> I want this tool to stay accessible to everyone, so the **free GitHub version** and the **paid Unity Asset Store version** include the exact same toolset.
> If you'd like to support my work and ongoing development, you can pick it up on the [Unity Asset Store](https://assetstore.unity.com/packages/slug/370342). Thanks!

Preview Forge adds custom Project window thumbnails and Inspector previews for a focused set of prefab and asset types, with editor safety and compatibility as first-class goals.

## What We Support

| Content type | Custom thumbnail | Custom Inspector preview | Notes |
| --- | --- | --- | --- |
| Particle prefabs | Yes | Yes | Particle thumbnails support particle content in the prefab hierarchy. The particle custom preview path targets prefabs whose root object owns the primary `ParticleSystem`. |
| UI prefabs | Yes | No | Supports `Canvas`-based or loose `RectTransform` prefabs with drawable Unity UI `Graphic` content or `TextMeshProUGUI`. |
| Sprite prefabs | No | Yes | Uses the sprite prefab preview workflow with framing, bounds, collider, and grid tools. |
| Model prefabs | No | Yes | Supports prefabs with `MeshRenderer` or `SkinnedMeshRenderer` content. |
| Imported 3D assets (`FBX`, `Blend`, similar model assets) | No | Yes | Uses the same improved model preview workflow in the importer preview. |

## What We Do Not Currently Support

- UI prefabs do not currently get a dedicated custom Inspector preview.
- Mixed UI + mesh or UI + sprite prefabs do not use the UI thumbnail renderer and fall back to the other applicable preview path or Unity defaults.
- VFX Graph thumbnails are not currently supported.

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

### Custom Thumbnails

- Particle prefab thumbnails with static rendering and motion-aware framing for effects that emit over traveled distance
- UI prefab thumbnails for supported `RectTransform`-based Unity UI and TMP UGUI prefabs
- Thumbnail badges that distinguish supported particle and UI prefab thumbnails
- Cached output stored in `Library/Noodle Hammer/Preview Forge/ParticleThumbnailCache`

### Custom Previews

- Particle prefab preview with playback controls, timeline scrubbing, and motion-aware inspection
- Sprite prefab preview with framing plus bounds, collider, and grid visualization tools
- Model and 3D asset preview with improved lighting, smoother orbit/pan/zoom, and auto-framing
- Visual modes including Normal, UV, Vertex Color, Matcap, and Overdraw


## Compatibility

- Minimum Unity version: `2022.3`
- Unity 6.5 (`6000.5+`) is supported through the package compatibility layer for Unity object identity APIs
- Developed and verified in Unity `6000.3.10f1` on macOS; compatibility checked against installed Unity `6000.5.0f1` API metadata
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

- The preview integration uses a scoped Harmony patcher designed to coexist with other editor tools like Odin Inspector but if you encounter any issue please report.

## Known Limitations

- UI prefab support is currently thumbnail-only.
- Particle custom previews are selected from prefabs whose root object contains the driving `ParticleSystem`.
- Mixed UI prefabs that also contain `MeshRenderer`, `SkinnedMeshRenderer`, or `SpriteRenderer` content do not use the UI thumbnail renderer.
- VFX Graph thumbnails are not currently supported.
- In URP, particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews. This is not supported by the current preview rendering path.
- A Unity Editor restart may be needed after first install or import for smoother preview window hook initialization.
