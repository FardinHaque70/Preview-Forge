# Preview Forge

Thumbnails and Custom Previews for Unity

Editor-only Unity tools for custom thumbnails and custom previews that improve Unity's default prefab and asset browsing workflow.

## What This Package Provides

- `Custom Thumbnails`
  - Static Project-window thumbnails for particle prefabs and supported UI prefabs that Unity does not thumbnail well by default.
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
- Render pipeline support:
  - Built-in Render Pipeline
  - URP (`3D` and `2D Renderer` detection for preview compatibility safeguards)
  - HDRP

## Performance and Safety Notes

- Preview Forge is designed to stay lightweight and editor-only, but it is not a zero-cost or zero-risk integration.
- Unity does not provide a clean public API for fully replacing Project window asset thumbnails, so Unity's default thumbnail still draws underneath before Preview Forge overlays its custom thumbnail result.
- Custom prefab preview ownership is patched in with a scoped Harmony integration because Unity does not expose a stable public replacement path for this workflow.
- No runtime update loops or player-side hooks are added using Harmony Patcher.
- Preview update hooks are scoped and unsubscribed when not needed.
- Because these features depend on Unity editor internals, future untested Unity versions may require compatibility updates if Unity changes thumbnail or preview behavior.

## Third-Party Inspector / Preview Coexistence

This package is designed to coexist with other editor tools, including Odin Inspector and custom `CustomPreview` workflows.

If you see preview ownership conflicts:

1. Check the Console for the Preview Forge conflict log to confirm whether Unity's built-in preview or another custom preview provider took ownership.
2. Open `Project Settings > Preview Forge` and temporarily disable custom prefab previews to confirm whether the conflict is on Preview Forge's preview path.
3. If another tool is trying to own the same prefab preview surface, keep one preview system enabled for that workflow instead of running both against the same target.
4. Re-enable Preview Forge, reselect the prefab, and restart Unity if needed so the inspector host rebuilds cleanly.
5. If the conflict still happens on a supported Unity version without another competing preview tool, report it as a compatibility issue.

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

## Install Layout

- Asset Store payload root: `Assets/Noodle Hammer/Preview Forge`
- Git UPM install root: `Packages/com.noodlehammer.preview-forge/Noodle Hammer/Preview Forge`
- Writable settings assets: `Assets/Noodle Hammer/Preview Forge/Settings`

## Usage

1. Import or select a particle prefab in the Project window.
2. Open the prefab Inspector preview to use playback and scrubbing controls.
3. Select supported UI prefabs in the Project window to let Preview Forge generate custom thumbnails.
4. For sprite or mesh/skinned prefabs, use preview controls for orbit/pan/zoom plus the available visualization tools.
5. Use package project settings for thumbnail cache maintenance, regeneration, and preview behavior defaults.

## Limitations

- UI prefab support is currently thumbnail-only.
- Mixed UI prefabs that also contain `MeshRenderer`, `SkinnedMeshRenderer`, or `SpriteRenderer` content do not use the UI thumbnail renderer.
- VFX Graph thumbnails are not currently supported.
- In URP, particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews. This is not supported by the current preview rendering path.
