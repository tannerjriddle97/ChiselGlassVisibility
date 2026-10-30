# Chisel Glass Visibility 1.0.0

A client-side glass visibility aid for Vintage Story **1.22.7**. Built against that installed release; QP's Chisel Tools **1.17.8** item codes are supported without requiring QP's DLL.

## Install

1. Close Vintage Story.
2. Put **chiselglassvisibility-1.0.0.zip** in `%APPDATA%\VintagestoryData\Mods`. Leave the ZIP intact. If you use a custom data directory, use its `Mods` folder instead.
3. Start the game and check that **Chisel Glass Visibility** is enabled in the Mod Manager.
4. Enter a world. Hold a vanilla chisel with a hammer in your offhand, or hold a QP editing tool in your main hand. Glass within eight blocks should gain a cyan tint. Nearby discovery can take a moment; switching away hides the tint immediately.

Install on your client only, including when playing multiplayer. QP's Chisel Tools is optional. This mod changes rendering only; it does not modify glass, saves, lighting, collisions, or tool actions.

## Configuration

The first launch creates `%APPDATA%\VintagestoryData\ModConfig\chiselglassvisibility.json`:

```json
{
  "Enabled": true,
  "Radius": 8,
  "Colour": "#00D8FF",
  "Opacity": 0.5
}
```

`Radius` is clamped to 1–16 blocks. `Colour` accepts `#RRGGBB`. `Opacity` accepts 0–1 and applies to each rendered surface; looking through multiple surfaces gives a stronger tint. Non-glass materials within chiselled blocks retain their normal appearance. Ordinary glass blocks, panes, and slabs use their actual block meshes, so their frames can also pick up the tint. Glass supplied by other mods is recognised when its block material is `Glass`; custom animated/rendered furniture is not explicitly integrated.

After editing the file, use **`.glassvisibility reload`** in game. A malformed JSON file is preserved and defaults are used until you fix it.

Commands:

- `.glassvisibility status` — enabled state, tool detection, discovered blocks, mesh batches, shader readiness, rendered frame count, and last scan time.
- `.glassvisibility on` / `.glassvisibility off` — save the enabled setting.
- `.glassvisibility reload` — reload configuration and rebuild the overlay.

QP support includes all material variants of True/QP Chisel, pantographs, multigraph (`megapantograph`), planer, wedge, brushes, scraper, palette, block swapper, door tool, ladder maker, path maker, and lock tool. These activate the visual aid without a hammer, even if the tool's editing operation itself needs one.

## First in-game test

Use a disposable creative world first. Compare regular glass and a chiselled glass shape with a hole; try a mixed glass/stone shape. Switch tools, remove the offhand hammer for a vanilla chisel, and test a QP tool with an empty offhand. Check editing, painting, pasting and rotation, then look from both sides and behind an opaque wall. Compare daylight and night and leave/rejoin the world.

If nothing appears, run `.glassvisibility status`. `tools active` confirms activation. `shader ready` confirms shader compilation. A rendered frame count that increases confirms the renderer is running. If the shader is unavailable or an error occurs, share the status output and relevant `[Chisel Glass Visibility]` / `chiselglass` lines from `Logs\client-main.log`.

## Validation status

Release build: **0 warnings, 0 errors**. **48 automated geometry, activation and configuration checks pass**. Live shader compilation, game rendering, multiplayer behaviour and performance have **not** been verified in game; the user is performing those tests. See `TESTING.md` for the exact coverage.

## Build from source

Requires the **.NET 10 SDK** and an installed copy of Vintage Story 1.22.7. No game binaries, QP binaries, account settings or SDK are distributed in the source archive.

```powershell
.\build.ps1 -GamePath 'E:\Games\Vintagestory'
# Optional: point at a portable SDK:
.\build.ps1 -GamePath 'E:\Games\Vintagestory' -DotnetPath 'C:\path\to\dotnet.exe'
```

The script builds the mod, runs all automated checks and creates `dist\chiselglassvisibility-1.0.0.zip`. No external NuGet packages are required. It never installs the mod or changes your game folders.

To uninstall, close the game and remove its ZIP. The configuration file may be kept for future use.

## Source repository

[ChiselGlassVisibility](https://github.com/tannerjriddle97/ChiselGlassVisibility) contains the addon source, authoring files and available test sources. See [LICENSE](LICENSE) for the existing source license. Game/mod dependencies, local profiles, saves, recovery provenance and generated packages are excluded. Historical audit/probe scripts require a separate deliberate runtime-testing decision; publication does not run them. Interactive acceptance limits remain as documented.
