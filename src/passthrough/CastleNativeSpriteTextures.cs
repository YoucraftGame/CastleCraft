using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace PeakCraft.World;

// First-person-owned copies. Atlas readback/upload remain on Unity's thread;
// owned-polygon sampling runs in at most two cancellable CPU jobs, never a frame-loop wait.
internal sealed class CastleNativeSpriteTextures : IDisposable {
 internal sealed class Slice {
  internal readonly Texture2D Texture;
  internal readonly (int width,int height,byte[] alpha) Mask;
  internal readonly bool Translucent;
  private readonly float ppu,width,height;
  private readonly Vector2 pivot;
  internal Slice(Texture2D texture,byte[] alpha,bool translucent,Sprite sprite){Texture=texture;Mask=(texture.width,texture.height,alpha);Translucent=translucent;ppu=sprite.pixelsPerUnit;width=texture.width;height=texture.height;pivot=sprite.pivot;}
  internal Vector2 Uv(Vector2 local)=>new((local.x*ppu+pivot.x)/width,(local.y*ppu+pivot.y)/height);
 }
 private readonly Dictionary<Sprite,Slice> slices=new();
 private readonly Dictionary<Sprite,Task<CastleSpritePixelCopy>> pending=new();
 private readonly Dictionary<Texture2D,uint[]> atlases=new();
 private readonly Dictionary<Texture2D,(int width,int height,byte[] alpha)> atlasMasks=new();
 private readonly HashSet<Sprite> failed=new();
 private CancellationTokenSource cancellation=new();
 private int starts,uploads;
 internal int Count=>slices.Count;
 internal int Pending=>pending.Count;
 internal void BeginFrame(){
  starts=uploads=0;
  var ready=new List<Sprite>();foreach(var p in pending)if(p.Value.IsCompleted)ready.Add(p.Key);
  foreach(var sprite in ready){if(uploads>=2)break;Get(sprite);}
 }
 internal Slice? Get(Sprite sprite){
  if(sprite==null)return null;
  if(slices.TryGetValue(sprite,out var existing))return existing;
  if(failed.Contains(sprite))return null;
  Texture2D? texture=null;
  try{
   if(pending.TryGetValue(sprite,out var job)){
    if(!job.IsCompleted||uploads>=2)return null;
    pending.Remove(sprite);uploads++;
    if(job.IsCanceled)return null;
    if(job.IsFaulted)throw job.Exception!.GetBaseException();
    var copy=job.Result; // Proven complete; no render-thread wait.
    texture=new Texture2D(copy.Width,copy.Height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave,name="CastleCraft first-person native "+sprite.name};
    var colors=new Color32[copy.Pixels.Length];for(int i=0;i<colors.Length;i++){uint rgba=copy.Pixels[i];colors[i]=new Color32((byte)rgba,(byte)(rgba>>8),(byte)(rgba>>16),(byte)(rgba>>24));}
    texture.SetPixels32(colors);texture.Apply(false,true);
    var result=new Slice(texture,copy.Alpha,copy.Translucent,sprite);slices[sprite]=result;return result;
   }
   if(pending.Count>=2||starts>=2)return null;
   var positions=sprite.vertices;var uv=sprite.uv;var indices=sprite.triangles;
   if(positions.Length!=uv.Length)throw new InvalidOperationException("Native vertex/UV lengths differ");
   var points=new CastleSpriteVertex[positions.Length];for(int i=0;i<points.Length;i++)points[i]=new CastleSpriteVertex(positions[i].x,positions[i].y,uv[i].x,uv[i].y);
   var triangles=new int[indices.Length];for(int i=0;i<indices.Length;i++)triangles[i]=indices[i];
   if(!CastleSpriteUv.TryCreate(points,triangles,out var map))throw new InvalidOperationException("Native sprite UV is not a verified affine mapping");
   int width=Mathf.CeilToInt(sprite.rect.width),height=Mathf.CeilToInt(sprite.rect.height);
   if(width<=0||height<=0||width>4096||height>4096||!(sprite.pixelsPerUnit>0))throw new InvalidOperationException("Native sprite size/PPU outside bounds");
   var atlas=sprite.texture;var source=Pixels(atlas);int aw=atlas.width,ah=atlas.height;
   var split=sprite.associatedAlphaSplitTexture;var alphaPixels=split==null?null:Pixels(split);int sw=split==null?0:split.width,sh=split==null?0:split.height;
   var pivot=sprite.pivot;double px=pivot.x,py=pivot.y,ppu=sprite.pixelsPerUnit;
   var token=cancellation.Token;starts++;
   pending[sprite]=Task.Run(()=>CastleSpritePixels.Unpack(map!,width,height,px,py,ppu,source,aw,ah,alphaPixels,sw,sh,token),token);
   return null;
  }catch(Exception e){if(texture!=null)UnityEngine.Object.Destroy(texture);failed.Add(sprite);PeakCraft.Plugin.Log.LogWarning($"First-person native sprite isolation rejected {sprite.name}: {e.Message}; retain existing native fallback");return null;}
 }
 internal (int width,int height,byte[] alpha) AtlasMask(Texture2D texture){
  if(atlasMasks.TryGetValue(texture,out var mask))return mask;
  var pixels=Pixels(texture);var alpha=new byte[pixels.Length];for(int i=0;i<pixels.Length;i++)alpha[i]=(byte)(pixels[i]>>24);
  mask=(texture.width,texture.height,alpha);atlasMasks[texture]=mask;return mask;
 }
 private uint[] Pixels(Texture2D texture){
  if(atlases.TryGetValue(texture,out var packed))return packed;
  Color32[] pixels;
  if(texture.isReadable)pixels=texture.GetPixels32();
  else{
   var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);Texture2D? readable=null;
   try{readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);Graphics.Blit(texture,target);RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply(false);pixels=readable.GetPixels32();}
   finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(readable!=null)UnityEngine.Object.Destroy(readable);}
  }
  packed=new uint[pixels.Length];for(int i=0;i<pixels.Length;i++){var p=pixels[i];packed[i]=(uint)p.r|((uint)p.g<<8)|((uint)p.b<<16)|((uint)p.a<<24);}
  atlases[texture]=packed;return packed;
 }
 public void Dispose(){
  cancellation.Cancel();cancellation.Dispose();cancellation=new CancellationTokenSource();pending.Clear();
  foreach(var s in slices.Values)UnityEngine.Object.Destroy(s.Texture);slices.Clear();atlases.Clear();atlasMasks.Clear();failed.Clear();
 }
}
