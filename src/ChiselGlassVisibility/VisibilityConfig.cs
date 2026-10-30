using System.Globalization;

namespace ChiselGlassVisibility;

public sealed class VisibilityConfig
{
    public bool Enabled { get; set; } = true;
    public int Radius { get; set; } = 8;
    public string Colour { get; set; } = "#00D8FF";
    public float Opacity { get; set; } = 0.5f;

    public void Normalize()
    {
        Radius = Math.Clamp(Radius, 1, 16);
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 0.5f;
        if (!TryColour(Colour, out _)) Colour = "#00D8FF";
    }

    public (float R, float G, float B) Rgb
    {
        get
        {
            TryColour(Colour, out int value);
            return (((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f);
        }
    }

    private static bool TryColour(string? value, out int rgb)
    {
        rgb = 0;
        return value is { Length: 7 } && value[0] == '#' &&
               int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
    }
}
