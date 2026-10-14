using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
namespace PeakCraft.World;

// A first native-textured voxel reconstruction for inspected rigs. Unseen geometry/color
// is explicitly inferred/reused, not claimed to be the original missing 3D artwork.
#if CASTLECRAFT_UNREVIEWED_VOXEL_SKIN
internal sealed class CastleNativeVoxelModels : IDisposable {
 // The current inferred voxel surface splits continuous native art into depth tiles.
 // Keep the inspected prototype out of runtime until per-part geometry and face UVs
 // pass front, side and back review against the original sprite references.
 private static bool NativeSurfaceBindingsVerified => false;
 private sealed class Model {internal Mesh Mesh=null!;internal CastleVoxelSkin Skin=null!;}
 private readonly Dictionary<(Sprite,bool),Model> models=new();
 private readonly Dictionary<(Sprite,bool),Task<CastleVoxelSkin>> pending=new();
 private readonly Dictionary<(Sprite,bool),Material> materials=new();
 private readonly HashSet<(Sprite,bool)> failed=new();
 private CancellationTokenSource cancellation=new();
 private int starts,uploads;
 private readonly MaterialPropertyBlock properties=new();
 internal int Count=>models.Count;
 internal int Pending=>pending.Count;
 internal static bool Known(string name){int i=name.IndexOf("(Clone)",StringComparison.Ordinal);if(i>=0)name=name.Substring(0,i).TrimEnd();return name=="goldChest"||name=="goblin_drudgery"||name=="goblin_mage"||name=="goblin_berserker"||name=="npc_mysterious_merchant"||name=="npc_cattiness_merchant";}
 private static bool Overlay(string name){name=name.ToLowerInvariant();return name.Contains("eye")||name.Contains("cloth")||name.Contains("effect")||name.Contains("erosion");}
 private static bool Effect(string name){name=name.ToLowerInvariant();return name.Contains("effect")||name.Contains("erosion")||name.Contains("sweat");}
 internal void BeginFrame(){starts=uploads=0;var ready=new List<(Sprite,bool)>();foreach(var p in pending)if(p.Value.IsCompleted)ready.Add(p.Key);foreach(var key in ready){if(key.Item1==null){pending.Remove(key);continue;}if(uploads>=2)break;Publish(key);}}
 private Model? Get(Sprite sprite,CastleNativeSpriteTextures.Slice slice,bool overlay){
  var key=(sprite,overlay);if(models.TryGetValue(key,out var model))return model;if(failed.Contains(key))return null;
  if(pending.ContainsKey(key))return Publish(key);
  if(starts>=2||pending.Count>=2)return null;
  // Capture all Unity data before scheduling. The worker sees only immutable pixel copies.
  int w=slice.Texture.width,h=slice.Texture.height;double ppu=slice.PixelsPerUnit,px=slice.Pivot.x,py=slice.Pivot.y;
  double depth=w/ppu*(overlay ? .02 : .65);var pixels=slice.Pixels;var token=cancellation.Token;starts++;
  pending[key]=Task.Run(()=>CastleVoxelSkin.Build(pixels,w,h,ppu,px,py,depth,!overlay,token),token);return null;
 }
 private Model? Publish((Sprite,bool) key){
  if(!pending.TryGetValue(key,out var job)||!job.IsCompleted||uploads>=2)return null;
  pending.Remove(key);uploads++;if(job.IsCanceled)return null;
  if(job.IsFaulted){failed.Add(key);PeakCraft.Plugin.Log.LogWarning("Native voxel worker rejected "+key.Item1.name+": "+job.Exception!.GetBaseException().Message);return null;}
  var skin=job.Result;if(skin.Vertices.Length==0){failed.Add(key);return null;}
  var positions=new Vector3[skin.Vertices.Length];var normals=new Vector3[positions.Length];var uv=new Vector2[positions.Length];var colors=new Color32[positions.Length];
  for(int i=0;i<positions.Length;i++){var v=skin.Vertices[i];positions[i]=new Vector3(v.X,v.Y,v.Z);normals[i]=new Vector3(v.NX,v.NY,v.NZ);uv[i]=new Vector2(v.U,v.V);colors[i]=new Color32(255,255,255,255);}
  var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32,name="CastleCraft original-pixel voxel "+key.Item1.name,subMeshCount=2};mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.colors32=colors;mesh.SetTriangles(skin.Opaque,0);mesh.SetTriangles(skin.Blended,1);mesh.RecalculateBounds();
  var result=new Model{Mesh=mesh,Skin=skin};models[key]=result;return result;
 }
 internal bool TryDraw(SpriteRenderer source,Component? owner,Matrix4x4 projected,int layer,CastleNativeSpriteTextures textures,float floor,float lift){
  if(!NativeSurfaceBindingsVerified)return false;
  if(owner==null||source.sprite==null||!Known(owner.name)||Effect(source.name))return false;
  var slice=textures.Get(source.sprite);if(slice==null)return false;bool overlay=Overlay(source.name);var model=Get(source.sprite,slice,overlay);if(model==null)return false;
  var canonical=Coords.Unproject*projected;float scale=(canonical.GetColumn(0).magnitude+canonical.GetColumn(1).magnitude)*.5f;
  // Limit inherited volume to the real native shadow's floor-depth footprint when known.
  var shadow=CastleGround.Shadow(owner)??owner.transform.Find("shadow")?.GetComponent<Collider2D>();
  if(shadow!=null){float allowed=shadow.bounds.size.y*Coords.Scale/Coords.DepthScale;float nativeDepth=(float)(model.Skin.Width/model.Skin.Ppu*.65);if(allowed>.01f&&!overlay)scale=Math.Min(scale,allowed/nativeDepth);}
  if(overlay){
   SpriteRenderer? parent=null;for(var t=source.transform.parent;t!=null&&t!=owner.transform;t=t.parent){var r=t.GetComponent<SpriteRenderer>();if(r!=null&&r.sprite!=null){parent=r;break;}}
   if(parent!=null){
    var parentSlice=textures.Get(parent.sprite);if(parentSlice==null)return false;var parentModel=Get(parent.sprite,parentSlice,false);if(parentModel==null)return false;
    var localPoint=parent.transform.InverseTransformPoint(source.transform.position);
    var pm=parent.transform.localToWorldMatrix;pm.m13+=lift;pm.m02=0;pm.m12=.6f*.15f;pm.m20=pm.m21=0;pm.m22=.36f*.15f;pm.m23=Coords.ZBase+floor*.6f;
    var pc=Coords.Unproject*pm;float ps=(pc.GetColumn(0).magnitude+pc.GetColumn(1).magnitude)*.5f;
    if(shadow!=null){float allowed=shadow.bounds.size.y*Coords.Scale/Coords.DepthScale;float nativeDepth=(float)(parentModel.Skin.Width/parentModel.Skin.Ppu*.65);if(allowed>.01f)ps=Math.Min(ps,allowed/nativeDepth);}
    canonical.m23=pc.m23-parentModel.Skin.HalfDepthAt(localPoint.x,localPoint.y)*ps-.002f;
   }
  }
  canonical.SetColumn(2,new Vector4(0,0,scale,0));var matrix=Coords.Unproject.inverse*canonical;
  if(source.flipX)matrix=matrix*Matrix4x4.Scale(new Vector3(-1,1,1));if(source.flipY)matrix=matrix*Matrix4x4.Scale(new Vector3(1,-1,1));
  properties.Clear();properties.SetColor("_Color",source.color);
  var drawMaterials=new Material?[2];
  for(int sub=0;sub<2;sub++){
   if(sub==0&&model.Skin.Opaque.Length==0||sub==1&&model.Skin.Blended.Length==0)continue;
   bool blend=sub==1||source.color.a<.999f;var key=(source.sprite,blend);
   if(!materials.TryGetValue(key,out var material)){
    var shader=Shader.Find(blend?"Sprites/Default":"Legacy Shaders/VertexLit");if(shader==null||!shader.isSupported)return false;
    material=new Material(shader){mainTexture=slice.Texture,hideFlags=HideFlags.HideAndDontSave,renderQueue=(int)(blend?RenderQueue.Transparent:RenderQueue.Geometry)};materials[key]=material;
   }
   drawMaterials[sub]=material;
  }
  for(int sub=0;sub<2;sub++)if(drawMaterials[sub]!=null)Graphics.DrawMesh(model.Mesh,matrix,drawMaterials[sub]!,layer,null,sub,properties,ShadowCastingMode.Off,false);
  return true;
 }
 public void Dispose(){cancellation.Cancel();cancellation.Dispose();cancellation=new CancellationTokenSource();pending.Clear();foreach(var m in models.Values)UnityEngine.Object.Destroy(m.Mesh);foreach(var m in materials.Values)UnityEngine.Object.Destroy(m);models.Clear();materials.Clear();failed.Clear();}
}
#else
// Rejected voxel prototype remains available for offline review only. The normal
// Host build does not retain additional CPU sprite copies or schedule its workers.
internal sealed class CastleNativeVoxelModels : IDisposable {
 internal int Count=>0;
 internal int Pending=>0;
 internal void BeginFrame(){}
 internal bool TryDraw(SpriteRenderer source,Component? owner,Matrix4x4 projected,int layer,CastleNativeSpriteTextures textures,float floor,float lift)=>false;
 public void Dispose(){}
}
#endif
