using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace PeakCraft.World;

// This renderer is invoked only by CastleStage3D.Draw in first-person mode.
// Palette geometry is an unapproved prototype. It must never replace original art before
// per-face native sprite/atlas bindings have been implemented and verified.
internal sealed class CastleFirstPersonModels : IDisposable {
 private static bool NativeTextureBindingsVerified=>false;
 private readonly Dictionary<string,List<(Mesh mesh,Material material)>> models=new();
 private readonly Dictionary<string,Material> materials=new();
 private readonly Dictionary<SpriteRenderer,(string model,Vector3 center,float scale)> anchors=new();
 private readonly MaterialPropertyBlock properties=new();
 internal bool TryDraw(SpriteRenderer source,Component? owner,Matrix4x4 projected,int layer){
  if(!NativeTextureBindingsVerified)return false;
  if(owner==null||source.sprite==null)return false;
  string? name=CastleCuboidLibrary.Binding(owner.name,source.name);if(name==null)return false;
  if(!models.TryGetValue(name,out var parts)){parts=Build(name);if(parts.Count==0)return false;models[name]=parts;}
  if(!anchors.TryGetValue(source,out var anchor)||anchor.model!=name){
   var vertices=source.sprite.vertices;var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
   foreach(var v in vertices){min=Vector2.Min(min,v);max=Vector2.Max(max,v);}
   Bounds bounds=parts[0].mesh.bounds;foreach(var p in parts)bounds.Encapsulate(p.mesh.bounds);
   float scale=Math.Min((max.x-min.x)/bounds.size.x,(max.y-min.y)/bounds.size.y);
   if(!(scale>0)||float.IsNaN(scale)||float.IsInfinity(scale))return false;
   // Stable bind-pose anchor: animation sprite swaps cannot make the model pulse in size.
   anchor=(name,new Vector3((min.x+max.x)*.5f,(min.y+max.y)*.5f,0)-bounds.center*scale,scale);anchors[source]=anchor;
  }
  var canonical=Coords.Unproject*projected;
  // Native bones retain their complete XY pose. Supply real depth on the room Z axis,
  // with inherited scale, rather than the old tiny camera-facing sprite thickness.
  float depthScale=(canonical.GetColumn(0).magnitude+canonical.GetColumn(1).magnitude)*.5f;
  canonical.SetColumn(2,new Vector4(0,0,depthScale,0));
  var local=Matrix4x4.TRS(anchor.center,Quaternion.identity,Vector3.one*anchor.scale);
  if(source.flipX)local=Matrix4x4.Scale(new Vector3(-1,1,1))*local;
  if(source.flipY)local=Matrix4x4.Scale(new Vector3(1,-1,1))*local;
  var matrix=Coords.Unproject.inverse*canonical*local;
  properties.Clear();properties.SetColor("_Color",source.color);
  foreach(var part in parts)Graphics.DrawMesh(part.mesh,matrix,part.material,layer,null,0,properties,ShadowCastingMode.Off,false);
  return true;
 }
 private List<(Mesh mesh,Material material)> Build(string name){
  var result=new List<(Mesh,Material)>();
  var shader=Shader.Find("Legacy Shaders/VertexLit")??Shader.Find("Unlit/Texture");
  if(shader==null||!shader.isSupported)return result;
  var groups=new Dictionary<string,List<CastleCuboid>>();
  foreach(var e in CastleCuboidLibrary.Models[name]){if(!e.Valid)throw new InvalidOperationException("Invalid cuboid "+name);if(!groups.TryGetValue(e.Palette,out var list))groups[e.Palette]=list=new();list.Add(e);}
  foreach(var group in groups){
   if(!materials.TryGetValue(group.Key,out var mat)){
    uint rgb=CastleCuboidLibrary.Color(group.Key);var texture=new Texture2D(16,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,hideFlags=HideFlags.HideAndDontSave};var pixels=new Color32[256];
    for(int y=0;y<16;y++)for(int x=0;x<16;x++){int s=(x*7+y*11)%17==0?-9:0;pixels[y*16+x]=new Color32((byte)Math.Max(0,(int)(rgb>>16)+s),(byte)Math.Max(0,(int)((rgb>>8)&255)+s),(byte)Math.Max(0,(int)(rgb&255)+s),255);}
    texture.SetPixels32(pixels);texture.Apply(false,true);mat=new Material(shader){mainTexture=texture,color=Color.white,hideFlags=HideFlags.HideAndDontSave,renderQueue=(int)RenderQueue.Geometry};materials[group.Key]=mat;
   }
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var indices=new List<int>();
   foreach(var e in group.Value){
    var a=new Vector3(e.X0,e.Y0,e.Z0);var b=new Vector3(e.X1,e.Y1,e.Z1);
    Face(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(a.x,b.y,a.z),b.x-a.x,b.y-a.y);
    Face(new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),b.x-a.x,b.y-a.y);
    Face(new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),b.z-a.z,b.y-a.y);
    Face(new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),b.z-a.z,b.y-a.y);
    Face(new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),b.x-a.x,b.z-a.z);
    Face(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z),b.x-a.x,b.z-a.z);
   }
   var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.SetNormals(normals);mesh.RecalculateBounds();result.Add((mesh,mat));
   void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float width,float height){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);var normal=Vector3.Cross(c-a,b-a).normalized;for(int k=0;k<4;k++)normals.Add(normal);uv.Add(Vector2.zero);uv.Add(new Vector2(width/16,0));uv.Add(new Vector2(width/16,height/16));uv.Add(new Vector2(0,height/16));indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});}
  }
  PeakCraft.Plugin.Log.LogInfo($"First-person cuboid model prepared: {name}, material groups={result.Count}; third-person native sprites retained");
  return result;
 }
 public void Dispose(){foreach(var group in models.Values)foreach(var p in group)UnityEngine.Object.Destroy(p.mesh);foreach(var mat in materials.Values){if(mat.mainTexture!=null)UnityEngine.Object.Destroy(mat.mainTexture);UnityEngine.Object.Destroy(mat);}models.Clear();materials.Clear();anchors.Clear();}
}
