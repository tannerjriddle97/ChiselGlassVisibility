using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ChiselGlassVisibility;

internal sealed class GlassRenderer(ICoreClientAPI api, GlassScene scene, Func<bool> active,
    Func<VisibilityConfig> config) : IRenderer
{
    private readonly Dictionary<Cell, MeshRef> meshes = [];
    private readonly Matrixf model = new();
    private IShaderProgram? shader;
    private bool shaderCompiled;
    private bool disposed;
    public double RenderOrder => 0.65;
    public int RenderRange => 32;
    public int BatchCount => meshes.Count;
    public bool ShaderReady => shaderCompiled && shader is { LoadError: false, Disposed: false };
    public long FramesDrawn { get; private set; }

    public bool LoadShader()
    {
        shaderCompiled = false;
        try
        {
            shader?.Dispose();
            shader = api.Shader.NewShaderProgram();
            shader.AssetDomain = "chiselglassvisibility";
            shader.Oit = true;
            api.Shader.RegisterFileShaderProgram("chiselglass", shader);
            shaderCompiled = shader.Compile() && !shader.LoadError;
            if (!shaderCompiled) api.Logger.Error("[Chisel Glass Visibility] Could not compile overlay shader; check client-main.log.");
        }
        catch (Exception e)
        {
            api.Logger.Error("[Chisel Glass Visibility] Overlay shader unavailable: {0}", e);
        }
        return shaderCompiled;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (disposed) return;
        if (stage == EnumRenderStage.Before)
        {
            foreach (var (origin, mesh) in scene.TakeUpdates())
            {
                if (meshes.Remove(origin, out var old)) old.Dispose();
                if (mesh != null)
                {
                    try { meshes.Add(origin, api.Render.UploadMesh(mesh)); }
                    finally { mesh.Dispose(); }
                }
            }
            return;
        }
        if (stage != EnumRenderStage.OIT || !active() || !ShaderReady || meshes.Count == 0) return;
        var settings = config();
        if (settings.Opacity <= 0) return;
        var previousShader = api.Render.CurrentActiveShader;
        bool depthWrite = GL.GetBoolean(GetPName.DepthWritemask);
        bool depthTest = GL.IsEnabled(EnableCap.DepthTest);
        bool cull = GL.IsEnabled(EnableCap.CullFace);
        bool polygonOffset = GL.IsEnabled(EnableCap.PolygonOffsetFill);
        float offsetFactor = GL.GetFloat(GetPName.PolygonOffsetFactor);
        float offsetUnits = GL.GetFloat(GetPName.PolygonOffsetUnits);
        try
        {
            previousShader?.Stop();
            api.Render.GLEnableDepthTest();
            api.Render.GLDepthMask(false);
            api.Render.GlDisableCullFace();
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(-1f, -1f);
            shader!.Use();
            shader.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("viewMatrix", api.Render.CameraMatrixOriginf);
            var (r, g, b) = settings.Rgb;
            shader.Uniform("tint", r, g, b, settings.Opacity);
            var camera = api.World.Player.Entity.CameraPos;
            int dimension = api.World.Player.Entity.Pos.Dimension;
            foreach (var (origin, mesh) in meshes)
            {
                if (origin.Dimension != dimension) continue;
                model.Identity().Translate(origin.X - camera.X, origin.Y - camera.Y, origin.Z - camera.Z);
                shader.UniformMatrix("modelMatrix", model.Values);
                api.Render.RenderMesh(mesh);
            }
            FramesDrawn++;
        }
        finally
        {
            shader!.Stop();
            GL.PolygonOffset(offsetFactor, offsetUnits);
            if (!polygonOffset) GL.Disable(EnableCap.PolygonOffsetFill);
            api.Render.GLDepthMask(depthWrite);
            if (!depthTest) api.Render.GLDisableDepthTest();
            if (cull) api.Render.GlEnableCullFace();
            previousShader?.Use();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var mesh in meshes.Values) mesh.Dispose();
        meshes.Clear(); shader?.Dispose(); shader = null;
    }
}
