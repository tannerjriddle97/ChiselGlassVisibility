using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ChiselGlassVisibility;

internal readonly record struct Cell(int X, int Y, int Z, int Dimension)
{
    public BlockPos Position => new(X, Y, Z, Dimension);
    public Cell Offset(int x, int y, int z) => new(X + x, Y + y, Z + z, Dimension);
    public Cell Region => new((int)Math.Floor(X / 4d) * 4, (int)Math.Floor(Y / 4d) * 4,
        (int)Math.Floor(Z / 4d) * 4, Dimension);
    public static Cell From(BlockPos p) => new(p.X, p.Y, p.Z, p.dimension);
}

internal sealed class GlassScene(ICoreClientAPI api)
{
    private sealed record Entry(ulong Signature, SurfaceGeometry Geometry);
    private readonly Dictionary<Cell, Entry> entries = [];
    private readonly Dictionary<int, SurfaceGeometry> regularTemplates = [];
    private readonly HashSet<Cell> dirtyRegions = [];
    private readonly HashSet<Cell> urgent = [];
    private readonly Dictionary<Cell, MeshData?> preparedUpdates = [];
    private List<(int X, int Y, int Z)> offsets = [];
    private Cell? center;
    private int radius;
    private int cursor;
    private bool wasActive;
    public double LastTickMilliseconds { get; private set; }
    public int BlockCount => entries.Count;

    public void Tick(bool active, int newRadius)
    {
        if (!active) { wasActive = false; return; }
        var timer = Stopwatch.StartNew();
        var player = api.World.Player.Entity;
        var current = Cell.From(player.Pos.AsBlockPos);
        if (center?.Dimension != current.Dimension || radius != newRadius)
        {
            Clear(); radius = newRadius;
            offsets = [];
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
                if (x * x + y * y + z * z <= radius * radius) offsets.Add((x, y, z));
            offsets.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
        }
        if (center != current || !wasActive)
        {
            center = current; cursor = 0;
            foreach (var key in entries.Keys.Where(k => !InRange(k, current)).ToArray()) Remove(key);
        }
        wasActive = true;
        // The crosshair block is checked every 100ms even during a long discovery sweep.
        if (api.World.Player.CurrentBlockSelection?.Position is { } selected)
        {
            var key = Cell.From(selected);
            if (InRange(key, current)) Refresh(key);
        }
        int processed = 0;
        foreach (var key in urgent.ToArray())
        {
            urgent.Remove(key);
            if (InRange(key, current)) Refresh(key, force: true);
            if (++processed >= 32 || timer.Elapsed.TotalMilliseconds > 4) break;
        }
        // Bounded discovery keeps large configured radii from stalling a frame.
        int limit = Math.Min(offsets.Count, 768);
        for (int i = 0; i < limit; i++)
        {
            var offset = offsets[cursor];
            Refresh(current.Offset(offset.X, offset.Y, offset.Z));
            cursor = (cursor + 1) % offsets.Count;
            if (timer.Elapsed.TotalMilliseconds > 5) break;
        }
        PrepareUpdates();
        LastTickMilliseconds = timer.Elapsed.TotalMilliseconds;
    }

    private static int Distance((int X, int Y, int Z) p) => p.X * p.X + p.Y * p.Y + p.Z * p.Z;
    private bool InRange(Cell p, Cell origin)
    {
        long x = (long)p.X - origin.X, y = (long)p.Y - origin.Y, z = (long)p.Z - origin.Z;
        return p.Dimension == origin.Dimension && x * x + y * y + z * z <= radius * radius;
    }

    public void Invalidate(BlockPos position)
    {
        var key = Cell.From(position);
        urgent.Add(key);
        foreach (var face in BlockFacing.ALLFACES) urgent.Add(key.Offset(face.Normali.X, face.Normali.Y, face.Normali.Z));
    }

    public void InvalidateChunk(Vec3i chunk)
    {
        // Chunk notifications can come from worker threads; callers enqueue this on the client thread.
        foreach (var key in entries.Keys)
        {
            if (Math.Abs(Math.Floor(key.X / 32d) - chunk.X) <= 1 &&
                Math.Abs(Math.Floor(key.Y / 32d) - chunk.Y) <= 1 &&
                Math.Abs(Math.Floor(key.Z / 32d) - chunk.Z) <= 1) urgent.Add(key);
        }
    }

    private void Refresh(Cell key, bool force = false)
    {
        var accessor = api.World.BlockAccessor;
        var pos = key.Position;
        if (!accessor.IsValidPos(pos)) { Remove(key); return; }
        var block = accessor.GetBlock(pos);
        if (block.Id == 0) { Remove(key); return; }
        var micro = accessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
        if (micro != null)
        {
            if (micro.BlockIds == null || micro.VoxelCuboids == null || !micro.BlockIds.Any(IsGlass))
            { Remove(key); return; }
            ulong signature = Signature(block.Id, micro);
            if (!force && entries.TryGetValue(key, out var previous) && previous.Signature == signature) return;
            var voxels = ReadVoxels(micro);
            // Neighbour grids are read once per rebuild, never per frame or per boundary face.
            var neighbourGrids = new Dictionary<Cell, byte[]>();
            byte Outside(int x, int y, int z)
            {
                int dx = x < 0 ? -1 : x >= 16 ? 1 : 0;
                int dy = y < 0 ? -1 : y >= 16 ? 1 : 0;
                int dz = z < 0 ? -1 : z >= 16 ? 1 : 0;
                var neighbour = key.Offset(dx, dy, dz);
                if (!neighbourGrids.TryGetValue(neighbour, out var grid))
                {
                    var npos = neighbour.Position;
                    var nblock = accessor.GetBlock(npos);
                    if (accessor.GetBlockEntity(npos) is BlockEntityMicroBlock nMicro) grid = ReadVoxels(nMicro);
                    else
                    {
                        grid = new byte[4096];
                        // Only full opaque neighbours can safely hide a microblock boundary.
                        if (nblock.AllSidesOpaque) Array.Fill(grid, (byte)1);
                        else if (IsGlass(nblock.Id) && IsFullCube(nblock)) Array.Fill(grid, (byte)2);
                    }
                    neighbourGrids.Add(neighbour, grid);
                }
                return grid[GlassSurface.Index((x + 16) % 16, (y + 16) % 16, (z + 16) % 16)];
            }
            Put(key, signature, GlassSurface.Build(voxels, Outside));
            if (!force) InvalidateExistingNeighbours(key);
        }
        else if (IsGlass(block.Id))
        {
            ulong signature = (ulong)block.Id;
            if (!force && entries.TryGetValue(key, out var previous) && previous.Signature == signature) return;
            if (!regularTemplates.TryGetValue(block.Id, out var geometry))
            {
                var source = api.TesselatorManager.GetDefaultBlockMesh(block);
                if (source == null) { Remove(key); return; }
                geometry = CopyGeometry(source);
                regularTemplates.Add(block.Id, geometry);
            }
            Put(key, signature, geometry);
        }
        else Remove(key);
    }

    private void InvalidateExistingNeighbours(Cell key)
    {
        foreach (var face in BlockFacing.ALLFACES)
        {
            var neighbour = key.Offset(face.Normali.X, face.Normali.Y, face.Normali.Z);
            if (entries.ContainsKey(neighbour)) urgent.Add(neighbour);
        }
    }

    private bool IsGlass(int id) => id > 0 && id < api.World.Blocks.Count &&
        api.World.Blocks[id].BlockMaterial == EnumBlockMaterial.Glass;

    private static bool IsFullCube(Block block) => block.Code?.Domain == "game" && block.Code.Path.StartsWith("glass-", StringComparison.Ordinal);

    private byte[] ReadVoxels(BlockEntityMicroBlock micro)
    {
        var voxels = new byte[4096];
        if (micro.BlockIds == null || micro.VoxelCuboids == null) return voxels;
        var cuboid = new CuboidWithMaterial();
        foreach (uint packed in micro.VoxelCuboids)
        {
            BlockEntityMicroBlock.FromUint(packed, cuboid);
            byte value = cuboid.Material < micro.BlockIds.Length && IsGlass(micro.BlockIds[cuboid.Material]) ? (byte)2 : (byte)1;
            for (int z = Math.Clamp(cuboid.Z1, 0, 16); z < Math.Clamp(cuboid.Z2, 0, 16); z++)
            for (int y = Math.Clamp(cuboid.Y1, 0, 16); y < Math.Clamp(cuboid.Y2, 0, 16); y++)
            for (int x = Math.Clamp(cuboid.X1, 0, 16); x < Math.Clamp(cuboid.X2, 0, 16); x++)
                voxels[GlassSurface.Index(x, y, z)] = value;
        }
        return voxels;
    }

    private static ulong Signature(int blockId, BlockEntityMicroBlock micro)
    {
        ulong hash = 14695981039346656037;
        hash = (hash ^ (uint)blockId) * 1099511628211;
        foreach (int id in micro.BlockIds) hash = (hash ^ (uint)id) * 1099511628211;
        foreach (uint value in micro.VoxelCuboids) hash = (hash ^ value) * 1099511628211;
        return hash;
    }

    private static SurfaceGeometry CopyGeometry(MeshData source)
    {
        var result = new SurfaceGeometry();
        for (int i = 0; i < source.VerticesCount; i++)
            result.Vertices.Add(new(source.xyz[3 * i], source.xyz[3 * i + 1], source.xyz[3 * i + 2], 235));
        for (int i = 0; i < source.IndicesCount; i++) result.Indices.Add(source.Indices[i]);
        return result;
    }

    private void Put(Cell key, ulong signature, SurfaceGeometry geometry)
    {
        entries[key] = new(signature, geometry);
        dirtyRegions.Add(key.Region);
    }

    private void Remove(Cell key)
    {
        if (!entries.Remove(key)) return;
        dirtyRegions.Add(key.Region);
        InvalidateExistingNeighbours(key);
    }

    private void PrepareUpdates()
    {
        var updates = new List<(Cell, MeshData?)>();
        // Batch spatially: one draw per 4x4x4 area, rather than one draw per glass block.
        foreach (var region in dirtyRegions)
        {
            var members = entries.Where(pair => pair.Key.Region == region).ToArray();
            int vertexCount = members.Sum(pair => pair.Value.Geometry.Vertices.Count);
            int indexCount = members.Sum(pair => pair.Value.Geometry.Indices.Count);
            if (indexCount == 0) { updates.Add((region, null)); continue; }
            var mesh = new MeshData(vertexCount, indexCount, false, false, true, false);
            foreach (var (key, entry) in members)
            {
                int start = mesh.VerticesCount;
                foreach (var v in entry.Geometry.Vertices)
                {
                    int rgba = unchecked((int)0xff000000) | (v.Shade << 16) | (v.Shade << 8) | v.Shade;
                    mesh.AddVertexSkipTex(v.X + key.X - region.X, v.Y + key.Y - region.Y, v.Z + key.Z - region.Z, rgba);
                }
                foreach (int index in entry.Geometry.Indices) mesh.AddIndex(start + index);
            }
            updates.Add((region, mesh));
        }
        dirtyRegions.Clear();
        foreach (var (origin, mesh) in updates)
        {
            if (preparedUpdates.Remove(origin, out var old)) old?.Dispose();
            preparedUpdates[origin] = mesh;
        }
    }

    public List<(Cell Origin, MeshData? Mesh)> TakeUpdates()
    {
        // Render callbacks only transfer prebuilt meshes to the GPU.
        var updates = preparedUpdates.Select(pair => (pair.Key, pair.Value)).ToList();
        preparedUpdates.Clear();
        return updates;
    }

    public void Clear()
    {
        foreach (var key in entries.Keys) dirtyRegions.Add(key.Region);
        foreach (var key in preparedUpdates.Keys) dirtyRegions.Add(key);
        foreach (var mesh in preparedUpdates.Values) mesh?.Dispose();
        preparedUpdates.Clear();
        foreach (var region in dirtyRegions) preparedUpdates[region] = null;
        dirtyRegions.Clear();
        entries.Clear(); regularTemplates.Clear(); urgent.Clear(); center = null; cursor = 0;
    }
}
