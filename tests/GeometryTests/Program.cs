using ChiselGlassVisibility;
using System.Numerics;
int count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message); count++;
}
double Area(SurfaceGeometry mesh)
{
    double area = 0;
    for (int i = 0; i < mesh.Indices.Count; i += 3)
    {
        Vector3 V(int j) { var v = mesh.Vertices[mesh.Indices[j]]; return new(v.X, v.Y, v.Z); }
        area += Vector3.Cross(V(i+1) - V(i), V(i+2) - V(i)).Length() / 2;
    }
    return area;
}
var full = Enumerable.Repeat((byte)2, 4096).ToArray();
var cube = GlassSurface.Build(full);
Check(cube.Vertices.Count == 24 && cube.Indices.Count == 36, "A full glass cube merges to six quads");
Check(Math.Abs(Area(cube) - 6) < 1e-6, "Merged cube surface area is exact");
bool winding = true;
for (int i = 0; i < cube.Indices.Count; i += 3)
{
    Vector3 V(int j) { var v = cube.Vertices[cube.Indices[j]]; return new(v.X, v.Y, v.Z); }
    var normal = Vector3.Cross(V(i+1) - V(i), V(i+2) - V(i));
    var center = (V(i)+V(i+1)+V(i+2))/3;
    winding &= Vector3.Dot(normal, center-new Vector3(0.5f)) > 0;
}
Check(winding, "All cube triangles have outward winding");
Check(GlassSurface.Build(new byte[4096]).Indices.Count == 0, "Empty block has no overlay");
Check(GlassSurface.Build(Enumerable.Repeat((byte)1,4096).ToArray()).Indices.Count == 0, "Solid non-glass has no overlay");
Check(GlassSurface.Build(full, (_,_,_) => 2).Indices.Count == 0, "Surrounding glass hides all shared internal faces");
Check(GlassSurface.Build(full, (_,_,_) => 1).Indices.Count == 0, "Surrounding opaque materials hide all faces");
var single = new byte[4096]; single[GlassSurface.Index(4,8,12)] = 2;
Check(Math.Abs(Area(GlassSurface.Build(single)) - 6d/256) < 1e-6, "One voxel retains its exact 1/16 scale");
single[GlassSurface.Index(5,8,12)] = 2;
Check(Math.Abs(Area(GlassSurface.Build(single)) - 10d/256) < 1e-6, "Adjacent glass voxels do not draw their common face");
single[GlassSurface.Index(5,8,12)] = 1;
Check(Math.Abs(Area(GlassSurface.Build(single)) - 5d/256) < 1e-6, "Mixed glass and stone renders only exposed glass");
var tunnel = (byte[])full.Clone();
for(int x=6;x<10;x++) for(int y=6;y<10;y++) for(int z=0;z<16;z++) tunnel[GlassSurface.Index(x,y,z)] = 0;
var hole = GlassSurface.Build(tunnel);
Check(hole.Vertices.Count > 24, "Tunnel cutout is preserved instead of drawing a solid bounding cube");
bool noCover = true;
for(int i=0;i<hole.Vertices.Count;i+=4)
{
    var vs=hole.Vertices.Skip(i).Take(4).ToArray();
    if(vs.All(v=>v.Z==0)||vs.All(v=>v.Z==1))
        noCover &= !(vs.Min(v=>v.X)<0.5f && vs.Max(v=>v.X)>0.5f && vs.Min(v=>v.Y)<0.5f && vs.Max(v=>v.Y)>0.5f);
}
Check(noCover, "No merged quad spans the central hole");
// Compare independent voxel face counting against the merged mesh on irregular mixed materials.
var random = new Random(20261026);
for(int sample=0;sample<12;sample++)
{
    var grid=Enumerable.Range(0,4096).Select(_=>(byte)random.Next(3)).ToArray();
    int faces=0;
    for(int x=0;x<16;x++) for(int y=0;y<16;y++) for(int z=0;z<16;z++)
    {
        if(grid[GlassSurface.Index(x,y,z)]!=2)continue;
        foreach(var (dx,dy,dz) in new[]{(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)})
        {
            int nx=x+dx,ny=y+dy,nz=z+dz;
            if(nx<0||nx>=16||ny<0||ny>=16||nz<0||nz>=16||grid[GlassSurface.Index(nx,ny,nz)]==0)faces++;
        }
    }
    Check(Math.Abs(Area(GlassSurface.Build(grid))-faces/256d)<1e-6,$"Irregular mixed-material surface {sample+1} matches independent face count");
}
Check(ToolRules.IsActive("game","chisel-copper",true,true), "Vanilla chisel and offhand hammer activate");
Check(!ToolRules.IsActive("game","chisel-copper",true,false), "Vanilla chisel without offhand hammer stays inactive");
Check(!ToolRules.IsActive(null,null,false,true), "Empty main hand stays inactive");
Check(!ToolRules.IsActive("game","pickaxe-iron",false,true), "Unrelated tool stays inactive");
foreach(var tool in new[]{"truechisel","pantograph","simplepantograph","megapantograph","handplaner","handwedge","paintbrush","jonaspaintbrush","paintscraper","palette","blockswapper","doortool","laddermaker","pathmaker","locktool"})
    Check(ToolRules.IsActive("chiseltools",tool+"-steel",false,false), "QP "+tool+" activates without hammer");
Check(!ToolRules.IsActive("othermod","pantograph-steel",false,false), "QP namespace prevents unrelated matches");
Check(!ToolRules.IsActive("chiseltools","deeps-ff0000",false,false), "QP palette material blocks are not editing tools");
var config=new VisibilityConfig();
Check(config.Radius==8&&config.Opacity==0.5f&&config.Enabled&&config.Colour=="#00D8FF", "Configuration defaults match the plan");
config.Radius=1000;config.Opacity=float.NaN;config.Colour="bad";config.Normalize();
Check(config.Radius==16&&config.Opacity==0.5f&&config.Colour=="#00D8FF", "Invalid configuration normalizes safely");
config.Colour="#ff0080";var rgb=config.Rgb;
Check(rgb.R==1&&rgb.G==0&&Math.Abs(rgb.B-128/255f)<1e-6, "Colour hex decodes correctly");
Console.WriteLine($"All {count} checks passed.");
