# Prefab Scatter

A Unity editor tool for scattering prefabs (plants, rocks, grass, debris...) inside a hand-drawn, organic area. Everything is placed neatly on the ground. Inspired by [Greebler](https://github.com/alexismorin/Greebler).

- Unity **2019.4** or newer
- Works with any render pipeline (the tool only spawns prefabs)
- Editor-time only: nothing extra ends up in a build except the (data-only) component

## Features

- **Prefab list** with per prefab: weight (chance), scale min/max, align to normal and Y offset
- **Drag & drop**: drop a whole selection of prefabs into the inspector at once
- **Organic area**: control points with a smooth Catmull-Rom outline; concave shapes work too
- **Ground placement** with downward raycasts, only on the selected **Ground Layers** (multiple layers supported)
- **Density** per m², **Min Distance** between objects, **Edge Falloff** for a soft border and optional **Perlin noise** for clumps and clearings
- **Max slope**: steep spots are skipped
- **Random Y rotation** (0-360°)
- **Clean hierarchy**: everything is parented under a single child object (`Scattered`)
- **Deterministic**: same seed + same settings = same result. **New seed** for a new variation
- **Full Undo support**
- **Visual feedback** in the Scene view:
  - filled area with an outline that follows the terrain
  - live preview of the placements (one colour per prefab), before you press Scatter
  - panel in the top-left with Edit / Scatter / Clear / New seed and the counts

## Installation

### Via git URL (recommended)

*Window > Package Manager* → **+** → **Add package from git URL...**

```
https://github.com/PauseFish/prefab-scatterer.git
```

Want a fixed version? Add the tag:

```
https://github.com/PauseFish/prefab-scatterer.git#v1.0.0
```

Or add it directly to `Packages/manifest.json`:

```json
"dependencies": {
  "com.thesourcegame.prefabscatter": "https://github.com/PauseFish/prefab-scatterer.git#v1.0.0"
}
```

> Is the repository private? Then git on your machine needs to be able to authenticate with GitHub (e.g. via GitHub Desktop or the Git Credential Manager).

### Updating

Unity stores the resolved commit in `Packages/packages-lock.json`. To get a new version, change the tag in the URL/manifest (e.g. `#v1.1.0`). Without a tag, remove this package's entry from `packages-lock.json` to fetch the latest commit.

### Editing locally

To modify the tool inside a project, clone the repository into `<Project>/Packages/`. Unity then treats it as an embedded package that you can edit directly.

## Usage

1. **GameObject > The Source Game > Prefab Scatter Area**. An irregular circle appears on the ground at the centre of your Scene view.
2. Add prefabs via the list, or drag them into the drop box below it.
3. Choose the **Ground Layers**. The ground needs a **collider** (MeshCollider, TerrainCollider...).
4. Click the area edit button and shape the area:

   | Action | Input |
   |---|---|
   | Move point (across the ground) | Drag |
   | Add point | Shift + click |
   | Remove point | Ctrl/Cmd + click on a point |
   | Stop editing | Esc |

5. Set **Density**, **Min Distance**, **Edge Falloff** and optionally **Noise**. The preview shows the result immediately.
6. Click **Scatter**. Clicking again regenerates everything. **Clear** removes it all.

### Settings

| Setting | Description |
|---|---|
| Weight | Relative chance that a prefab is picked |
| Scale (min / max) | Random uniform scale, multiplied by the prefab's own scale |
| Align To Normal | 0 = straight up, 1 = fully follows the slope |
| Y Offset | Offset along the up axis. Negative = sink slightly into the ground |
| Ground Layers | Only colliders on these layers count as ground |
| Max Slope | Steeper spots (in degrees) are skipped |
| Raycast Height | How far above the area the raycast starts |
| Smooth Outline / Subdivisions | Smooth outline through the control points |
| Density | Maximum number of objects per m² |
| Min Distance | Minimum distance between two objects (0 = off) |
| Edge Falloff | Distance from the border over which density fades to 0 |
| Use Noise / Scale / Threshold | Perlin noise for clumps. Higher threshold = more clearings |
| Random Y Rotation | Random rotation around Y |
| Seed | Controls the randomness |
| Max Instances | Safety limit per area |
| Container Name | Name of the child object that holds all spawned objects |

### Tips

- The **pivot** of your prefabs should be at the bottom; it is placed on the ground hit point.
- Spawned objects are ignored by the raycasts, even if they are on a ground layer.
- Use multiple areas for layering (e.g. one for grass, one for bushes with a larger Min Distance).
- The snap-to-ground button puts all control points on the terrain, handy after moving the object.

## Structure

```
package.json
Runtime/
  PrefabScatterArea.cs        Component holding all settings (data + gizmo only)
  ScatterMath.cs              Polygon maths on the XZ plane (smoothing, inside test, triangulation)
Editor/
  PrefabScatterGenerator.cs   Calculates placements (raycasts) and spawns/clears with Undo
  PrefabScatterAreaEditor.cs  Inspector, scene handles, preview and scene overlay
```

Namespace: `TheSourceGame.PrefabScatter`.
