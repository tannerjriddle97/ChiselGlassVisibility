namespace ChiselGlassVisibility;

public readonly record struct SurfaceVertex(float X, float Y, float Z, byte Shade);

public sealed class SurfaceGeometry
{
    public List<SurfaceVertex> Vertices { get; } = [];
    public List<int> Indices { get; } = [];

    public void Quad(SurfaceVertex a, SurfaceVertex b, SurfaceVertex c, SurfaceVertex d)
    {
        int start = Vertices.Count;
        Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
        Indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
    }
}

/// <summary>Pure geometry: 0=empty, 1=other occupied material, 2=glass.</summary>
public static class GlassSurface
{
    public const int Size = 16;
    public static int Index(int x, int y, int z) => x + Size * (y + Size * z);

    public static SurfaceGeometry Build(byte[] voxels, Func<int, int, int, byte>? outside = null)
    {
        if (voxels.Length != 4096) throw new ArgumentException("Expected 16 cubed voxels.", nameof(voxels));
        var result = new SurfaceGeometry();
        var mask = new bool[256];
        for (int axis = 0; axis < 3; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        for (int layer = 0; layer < Size; layer++)
        {
            for (int v = 0; v < Size; v++)
            for (int u = 0; u < Size; u++)
            {
                var (x, y, z) = Point(axis, layer, u, v);
                byte current = voxels[Index(x, y, z)];
                int nx = x + (axis == 0 ? sign : 0), ny = y + (axis == 1 ? sign : 0), nz = z + (axis == 2 ? sign : 0);
                byte neighbour = nx is >= 0 and < Size && ny is >= 0 and < Size && nz is >= 0 and < Size
                    ? voxels[Index(nx, ny, nz)] : outside?.Invoke(nx, ny, nz) ?? 0;
                mask[u + Size * v] = current == 2 && neighbour == 0;
            }
            // Merge coplanar faces. A full glass cube needs six quads, rather than 1536.
            for (int v = 0; v < Size; v++)
            for (int u = 0; u < Size; u++)
            {
                if (!mask[u + Size * v]) continue;
                int width = 1;
                while (u + width < Size && mask[u + width + Size * v]) width++;
                int height = 1;
                while (v + height < Size && RowFilled(mask, u, v + height, width)) height++;
                for (int dv = 0; dv < height; dv++)
                for (int du = 0; du < width; du++) mask[u + du + Size * (v + dv)] = false;
                int plane = layer + (sign > 0 ? 1 : 0);
                byte shade = (byte)(axis == 1 ? (sign > 0 ? 255 : 180) : axis == 0 ? 215 : 235);
                SurfaceVertex Vertex(int pu, int pv)
                {
                    var (px, py, pz) = Point(axis, plane, pu, pv);
                    return new(px / 16f, py / 16f, pz / 16f, shade);
                }
                var a = Vertex(u, v); var b = Vertex(u + width, v);
                var c = Vertex(u + width, v + height); var d = Vertex(u, v + height);
                if (sign > 0) result.Quad(a, b, c, d); else result.Quad(a, d, c, b);
            }
        }
        return result;
    }

    private static bool RowFilled(bool[] mask, int u, int v, int width)
    {
        for (int i = 0; i < width; i++) if (!mask[u + i + Size * v]) return false;
        return true;
    }

    private static (int X, int Y, int Z) Point(int axis, int plane, int u, int v) => axis switch
    {
        0 => (plane, u, v), 1 => (v, plane, u), _ => (u, v, plane)
    };
}
