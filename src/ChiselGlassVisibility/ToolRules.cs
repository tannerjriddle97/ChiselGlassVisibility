namespace ChiselGlassVisibility;

public static class ToolRules
{
    private static readonly HashSet<string> QpTools = new(StringComparer.Ordinal)
    {
        "truechisel", "pantograph", "simplepantograph", "megapantograph", "multigraph",
        "handplaner", "handwedge", "paintbrush", "jonaspaintbrush", "paintscraper",
        "palette", "blockswapper", "doortool", "laddermaker", "pathmaker", "locktool"
    };

    public static bool IsActive(string? domain, string? path, bool isVanillaChisel, bool offhandHammer)
    {
        if (domain == "chiseltools" && path != null)
        {
            int separator = path.IndexOf('-');
            string code = separator < 0 ? path : path[..separator];
            return QpTools.Contains(code);
        }
        return isVanillaChisel && offhandHammer;
    }
}
