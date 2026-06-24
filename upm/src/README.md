# Preview Forge

<sub>Thumbnails and Custom Previews for Unity</sub>

Editor-only Unity tools for custom thumbnails and custom previews across a focused set of prefab and asset types.

## Support Matrix

| Content type | Custom thumbnail | Custom Inspector preview | Notes |
| --- | --- | --- | --- |
| Particle prefabs | Yes | Yes | Thumbnails support particle content in the prefab hierarchy. The particle custom preview path targets prefabs whose root object owns the primary `ParticleSystem`. |
| UI prefabs | Yes | No | Supports `Canvas`-based or loose `RectTransform` prefabs with drawable Unity UI `Graphic` content or `TextMeshProUGUI`. |
| Sprite prefabs | No | Yes | Uses the sprite prefab preview workflow with framing, bounds, collider, and grid tools. |
| Model prefabs | No | Yes | Supports prefabs with `MeshRenderer` or `SkinnedMeshRenderer` content. |
| Imported 3D assets | No | Yes | Uses the improved model importer preview workflow. |

## What This Package Provides

- `Custom Thumbnails`
  - Static Project-window thumbnails for supported particle and UI prefabs.
  - Motion-aware framing for effects that emit over traveled distance.
  - Thumbnail badges for supported prefab categories.
- `Custom Previews`
  - Particle prefab preview with playback controls and timeline scrubbing.
  - Sprite prefab preview with framing plus bounds, collider, and grid tools.
  - Model prefab and imported model preview with auto-framing, orbit, pan, zoom, and visual mode controls.
- `Editor Safety`
  - Editor-only assemblies with no runtime/player build dependency.
  - Scoped preview-hook patching with bounded retries and compatibility fallbacks.

## Compatibility

- Minimum Unity version: `2022.3` (UPM metadata baseline)
- Unity 6.5 (`6000.5+`) is supported through the package compatibility layer for Unity object identity APIs
- Verified in development environment: Unity `6000.3.10f1` on macOS; compatibility checked against installed Unity `6000.5.0f1` API metadata
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

1. Open `Project Settings > Preview Forge`
2. Temporarily disable Particle Preview
3. Adjust tool registration order / integration settings
4. Re-enable Particle Preview and recheck prefab inspector behavior

## Installation (Git UPM)

1. Open `Window > Package Manager`
2. Click `+`
3. Select `Add package from git URL...`
4. Paste:

```text
https://github.com/FardinHaque70/Preview-Forge.git?path=upm/src#main
```

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.noodlehammer.preview-forge": "https://github.com/FardinHaque70/Preview-Forge.git?path=upm/src#main"
  }
}
```

Unity Editor restart is recommended after first install so preview hook initialization is clean.

### Unity Asset Store Package

- Import the package into your project from the Unity Package Manager (`My Assets`) or from a provided `.unitypackage`.
- After import, open `Project Settings > Preview Forge` to confirm defaults for your project.
- Restart Unity once after first import so preview hook initialization is clean.

## Distribution Workflow

- Asset Store release payload: `Assets/Noodle Hammer/Preview Forge`
- Git UPM release payload: `upm/src`
- Each install is single-root:
  - Asset Store import runs entirely from `Assets/Noodle Hammer/Preview Forge`
  - Git UPM install runs entirely from `Packages/com.noodlehammer.preview-forge/Noodle Hammer/Preview Forge`
- Project configuration stays in `Assets/Noodle Hammer/Preview Forge/Settings`
- Git UPM installs should not require or auto-create a companion `Assets/Noodle Hammer/Preview Forge` folder

For this repository, authoring stays in `Assets/Noodle Hammer/Preview Forge`. Run `scripts/sync_upm_package.sh` before Git UPM release work to mirror the current source into `upm/src`.

## Usage

1. Import or select a particle prefab in the Project window.
2. Open the prefab Inspector preview to use playback and scrubbing controls.
3. Select supported UI prefabs in the Project window to let Preview Forge generate custom thumbnails.
4. For sprite or mesh/skinned prefabs, use preview controls for orbit/pan/zoom plus the available visualization tools.
5. Use package project settings for thumbnail cache maintenance, regeneration, and preview behavior defaults.

## Extending Safely

- Keep extension code in editor-only assemblies (`includePlatforms: Editor`).
- Reuse `PreviewRenderCompatibilityUtility` and `PreviewModeResolver` for SRP/version-safe decisions.
- Prefer lightweight, event-driven hooks over persistent editor update polling.
- When adding preview integrations, keep conflict logs actionable and one-time to avoid console spam.

## Limitations

- UI prefab support is currently thumbnail-only.
- Mixed UI prefabs that also contain `MeshRenderer`, `SkinnedMeshRenderer`, or `SpriteRenderer` content do not use the UI thumbnail renderer.
- VFX Graph thumbnails are not currently supported.
- In URP, particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews. This is not supported by the current preview rendering path.
