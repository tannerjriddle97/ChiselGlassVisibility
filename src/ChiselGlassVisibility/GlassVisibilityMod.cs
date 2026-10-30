using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ChiselGlassVisibility;

public sealed class GlassVisibilityMod : ModSystem
{
    private const string ConfigFile = "chiselglassvisibility.json";
    private ICoreClientAPI? api;
    private VisibilityConfig config = new();
    private GlassScene? scene;
    private GlassRenderer? renderer;
    private long tickId;
    private bool tickErrorLogged;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.api = api;
        ReadConfig();
        api.Event.LevelFinalize += EnterWorld;
        api.Event.LeaveWorld += LeaveWorld;
        api.Event.ReloadShader += ReloadShader;
        api.Event.BlockChanged += BlockChanged;
        api.Event.ChunkDirty += ChunkDirty;
        tickId = api.Event.RegisterGameTickListener(Tick, 100);
        api.ChatCommands.Create("glassvisibility")
            .WithDescription("Glass visibility: status, on, off, or reload configuration.")
            .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"))
            .HandleWith(args => Command(args[0] as string ?? "status"));
        api.Logger.Notification("[Chisel Glass Visibility] Loaded client mod 1.0.0; QP support is optional.");
    }

    private void ReadConfig()
    {
        try { config = api!.LoadModConfig<VisibilityConfig>(ConfigFile) ?? new(); }
        catch (Exception e)
        {
            config = new();
            api!.Logger.Warning("[Chisel Glass Visibility] Invalid configuration; using defaults: {0}", e.Message);
            return; // Preserve a malformed file so it can be corrected by its owner.
        }
        config.Normalize();
        api!.StoreModConfig(config, ConfigFile);
    }

    private void EnterWorld()
    {
        LeaveWorld(); tickErrorLogged = false;
        scene = new GlassScene(api!);
        renderer = new GlassRenderer(api!, scene, Active, () => config);
        renderer.LoadShader();
        api!.Event.RegisterRenderer(renderer, EnumRenderStage.Before, "chiselglass-upload");
        api.Event.RegisterRenderer(renderer, EnumRenderStage.OIT, "chiselglass-overlay");
    }

    private bool Active()
    {
        if (!config.Enabled || config.Opacity <= 0 || api?.World?.Player?.Entity == null) return false;
        var entity = api.World.Player.Entity;
        var main = entity.RightHandItemSlot?.Itemstack?.Collectible;
        var off = entity.LeftHandItemSlot?.Itemstack?.Collectible;
        return main != null && ToolRules.IsActive(main.Code?.Domain, main.Code?.Path,
            main.Tool == EnumTool.Chisel, off?.Tool == EnumTool.Hammer);
    }

    private void Tick(float dt)
    {
        try { scene?.Tick(Active(), config.Radius); }
        catch (Exception e)
        {
            if (tickErrorLogged) return;
            tickErrorLogged = true;
            api!.Logger.Error("[Chisel Glass Visibility] Overlay update failed: {0}", e);
        }
    }

    private void BlockChanged(BlockPos position, Block oldBlock) => scene?.Invalidate(position);
    private void ChunkDirty(Vec3i coord, IWorldChunk chunk, EnumChunkDirtyReason reason)
    {
        if (scene == null) return;
        var currentScene = scene;
        var copy = coord.Clone();
        api!.Event.EnqueueMainThreadTask(() => { if (scene == currentScene) scene.InvalidateChunk(copy); }, "chiselglass-chunkdirty");
    }

    private bool ReloadShader() => renderer?.LoadShader() ?? true;

    private TextCommandResult Command(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "on": config.Enabled = true; api!.StoreModConfig(config, ConfigFile); break;
            case "off": config.Enabled = false; api!.StoreModConfig(config, ConfigFile); break;
            case "reload": ReadConfig(); scene?.Clear(); break;
            case "status": break;
            default: return TextCommandResult.Error("Use .glassvisibility status|on|off|reload");
        }
        return TextCommandResult.Success($"Glass visibility: {(config.Enabled ? "enabled" : "disabled")}, " +
            $"{config.Radius} blocks, {config.Colour}, opacity {config.Opacity:0.##}; " +
            $"tools {(Active() ? "active" : "inactive")}, {scene?.BlockCount ?? 0} glass blocks, " +
            $"{renderer?.BatchCount ?? 0} batches, shader {(renderer?.ShaderReady == true ? "ready" : "unavailable")}, " +
            $"{renderer?.FramesDrawn ?? 0} rendered frames, scan {scene?.LastTickMilliseconds ?? 0:0.0}ms.");
    }

    private void LeaveWorld()
    {
        if (renderer != null)
        {
            api!.Event.UnregisterRenderer(renderer, EnumRenderStage.Before);
            api.Event.UnregisterRenderer(renderer, EnumRenderStage.OIT);
            renderer.Dispose(); renderer = null;
        }
        scene?.Clear(); scene = null;
    }

    public override void Dispose()
    {
        if (api == null) return;
        LeaveWorld();
        api.Event.UnregisterGameTickListener(tickId);
        api.Event.LevelFinalize -= EnterWorld;
        api.Event.LeaveWorld -= LeaveWorld;
        api.Event.ReloadShader -= ReloadShader;
        api.Event.BlockChanged -= BlockChanged;
        api.Event.ChunkDirty -= ChunkDirty;
        api = null;
    }
}
