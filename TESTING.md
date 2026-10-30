# Validation record — Chisel Glass Visibility 1.0.0

Built on 2026-10-26 against Vintage Story 1.22.7 (`VintagestoryAPI.dll`, `VSSurvivalMod.dll`, and the game's `OpenTK.Graphics.dll`), using an official portable .NET 10.0.403 SDK. Microsoft SHA-512 metadata matched the downloaded SDK archive. The game installation was read only.

## Completed

- Release compilation succeeds with zero warnings and zero errors.
- 48 deterministic checks pass: full cube merging and area, outward triangle winding, empty and non-glass blocks, hidden boundaries against glass and opaque neighbours, individual voxel scale, adjacent voxel face removal, mixed-material surfaces, a through-hole without spanning faces, twelve seeded irregular voxel grids checked against an independent exposed-face count, vanilla tool/hammer combinations, every installed QP editing-tool code, namespace exclusions, defaults, configuration bounds and colour parsing.
- QP 1.17.8 packaged item definitions were inspected to establish the supported `chiseltools` codes. QP is not referenced by the assembly or required by mod metadata.
- The installed microblock `FromUint` method and renderer/shader signatures were checked. Cuboids are decoded with the game's own method; no assumed packed format is used.
- Shader code uses the game's `oit.fsh` include and the `IShaderProgram.Oit` flag. That matches the installed shader loader's `USEOIT` prefix mechanism. This is a static compatibility check, not a successful GPU compilation claim.
- Release ZIP and source archive are checked for their expected contents. The release contains only the mod DLL, metadata and two original shaders; no game libraries are redistributed.

## Pending user tests

- Mod loading, shader compilation and actual visual appearance on the game client.
- Plain and coloured glass, regular panes/slabs, thin chiselled designs and mixed-material blocks.
- QP editing operations: paint, paste, rotate, replace and undo; vanilla chiselling.
- Immediate deactivation, activation after movement, chunk unloads and world reloads.
- Day/night, correct opaque-wall occlusion, layered transparency and graphics-setting/shader reloads.
- Frame rate and scanner cost around a large glass build, plus multiplayer and QP-absent runs.

No existing world or active game window was used for automated tests. An isolated test profile was prepared, but live UI testing was handed to the user. No mod was installed in the user's normal data folder.

## Architecture notes

All runtime changes are client-local. A 100ms tick performs bounded discovery and mesh construction; the crosshair target is prioritised. The renderer checks held tools every frame to hide the effect immediately. Glass microblock surfaces are greedily merged, shared occupied voxel faces are removed, and geometry is batched by 4x4x4 regions. GPU upload occurs in the `Before` render stage, drawing in `OIT`; depth writes are disabled and depth testing retained. Modified GL depth/culling/offset state and the previous shader are restored. World exit unregisters the renderers and releases GPU meshes.

The REA artifact inspection confirmed the installed survival assembly identity and .NET 10 target (artifact evidence `ev_1485eb0d177dc8909f8a0dfd1678356920ec52eba74673c2fd375c121ea6e7a4`). REA's complete member inventory exceeded its metadata representation budget; focused local reflection/CIL inspection supplied the required interface checks. Static inspection does not establish live rendering behaviour.
