using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using PeakCraft.Render;

namespace PeakCraft.World;

/// <summary>Runtime reconstruction from the owner's native colliders and animated sprites.
/// Extruded sprites preserve native animation; they are not reconstructed anatomical models.</summary>
internal sealed class CastleStage3D {
 internal const int Layer=29;
 private readonly Dictionary<Sprite,Mesh> meshes=new();
 private readonly CastleFirstPersonModels cuboidModels=new();
 private readonly CastleNativeVoxelModels voxelModels=new();
 internal int NativeVoxelModels=>voxelModels.Count;
 internal int NativeVoxelPending=>voxelModels.Pending;
 private readonly CastleNativeSpriteTextures nativeTextures=new();
 internal int NativeTextureCopies=>nativeTextures.Count;
 internal int NativeTexturePending=>nativeTextures.Pending;
 private readonly HashSet<Sprite> isolatedMeshes=new();
 private readonly Dictionary<SpriteRenderer,Sprite> lastSprites=new();
 private int spriteCursor;
 private readonly Dictionary<Sprite,Vector2[]> spritePoints=new();
 private readonly Dictionary<(Sprite sprite,bool blended),Material> materials=new();
 private readonly List<Entry> entries=new();
 private readonly List<Bounds> walls=new();
 private readonly Dictionary<Animator,AnimatorCullingMode> animationModes=new();
 private readonly Dictionary<ParticleSystemRenderer,Mesh> particles=new();
 private readonly Type? creatureType=AccessTools.TypeByName("Teran.Creature");
 private readonly Type? npcType=AccessTools.TypeByName("Teran.Npc");
 private readonly Dictionary<Component,float> groundOffsets=new();
 private readonly Dictionary<Texture,(int width,int height,byte[] alpha)> masks=new();
 internal void RestoreAnimations(){foreach(var pair in animationModes)if(pair.Key!=null)pair.Key.cullingMode=pair.Value;animationModes.Clear();if(stageLight!=null)stageLight.enabled=false;}
 private readonly MaterialPropertyBlock properties=new();
 private float nextScan,nextParticleScan;
 private string room="";
 private Mesh? cube;
 private Material? stone;
 private Light? stageLight;
 private float roofHeight=3f;
 private bool indoor;
 private struct Entry {internal SpriteRenderer Source;internal Component? Creature;internal Collider2D? Foot;internal Transform? Ground;internal bool Floor,Actor;internal float GroundLift;}
 public void Reset(){voxelModels.Dispose();cuboidModels.Dispose();RestoreAnimations();groundOffsets.Clear();masks.Clear();indoor=false;roofHeight=3f;if(stone!=null)stone.mainTexture=null;foreach(var mesh in particles.Values)UnityEngine.Object.Destroy(mesh);particles.Clear();foreach(var mesh in meshes.Values)UnityEngine.Object.Destroy(mesh);foreach(var mat in materials.Values)UnityEngine.Object.Destroy(mat);nativeTextures.Dispose();isolatedMeshes.Clear();meshes.Clear();lastSprites.Clear();spritePoints.Clear();materials.Clear();entries.Clear();nextScan=nextParticleScan=0;}
 public void Dispose(){Reset();if(cube!=null)UnityEngine.Object.Destroy(cube);if(stone!=null)UnityEngine.Object.Destroy(stone);if(stageLight!=null)UnityEngine.Object.Destroy(stageLight.gameObject);cube=null;stone=null;stageLight=null;}
 public void Draw(string key,CastleCollision collision,Component? hero,Vector3 eye,float yaw){
  if(room!=key){Reset();room=key;}
  nativeTextures.BeginFrame();voxelModels.BeginFrame();
  // Short-lived native spell emitters may finish between the one-second static scene scans.
  if(Time.unscaledTime>=nextParticleScan){
   nextParticleScan=Time.unscaledTime+.05f;
   foreach(var renderer in UnityEngine.Object.FindObjectsOfType<ParticleSystemRenderer>()){
    if(renderer.gameObject.layer==Coords.GeometryLayer||renderer.gameObject.layer==Coords.AvatarLayer||renderer.gameObject.layer==Layer)continue;
    if(!particles.ContainsKey(renderer)){var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();particles[renderer]=mesh;}
   }
  }
  if(Time.unscaledTime>=nextScan){
   nextScan=Time.unscaledTime+1;
   entries.Clear();walls.Clear();walls.AddRange(collision.WallBounds);
   foreach(var animator in UnityEngine.Object.FindObjectsOfType<Animator>()){
    if(!animationModes.ContainsKey(animator))animationModes[animator]=animator.cullingMode;
    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   }
   var creature=creatureType;
   foreach(var source in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()){
    if(entries.Count>=1024)break;
    if(source.sprite==null||source.gameObject.layer==5||source.gameObject.layer==Coords.AvatarLayer||source.gameObject.layer==Coords.GeometryLayer||source.gameObject.layer==Layer)continue;
    var roomBounds=collision.RoomBounds;
    if(roomBounds.width>0&&!Rect.MinMaxRect(roomBounds.xMin-3,roomBounds.yMin-3,roomBounds.xMax+3,roomBounds.yMax+8).Overlaps(Rect.MinMaxRect(source.bounds.min.x,source.bounds.min.y,source.bounds.max.x,source.bounds.max.y)))continue;
    if(hero!=null&&source.transform.IsChildOf(hero.transform))continue;
    if(source.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0||source.name.IndexOf("blackEdge",StringComparison.OrdinalIgnoreCase)>=0||source.sprite.name=="castle_dark")continue;
    var owner=creature==null?null:source.GetComponentInParent(creature);
    if(owner==null&&npcType!=null)owner=source.GetComponentInParent(npcType);
    bool actor=owner!=null;
    // Static props also have multi-part silhouettes and a native ground shadow.
    if(owner==null)for(var parent=source.transform.parent;parent!=null;parent=parent.parent)if(parent.Find("shadow")!=null){owner=parent;break;}
    var ground=owner?.transform.Find("shadow");
    var foot=(owner==null?null:CastleGround.Shadow(owner))??ground?.GetComponent<Collider2D>()??(owner==null?source.GetComponentInParent<Collider2D>():owner.GetComponent<Collider2D>());
    float lift=0;
    if(actor&&owner!=null){
     if(!groundOffsets.TryGetValue(owner,out lift)){
      var property=owner.GetType().GetProperty("BodyCollider",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
      var body=property?.GetValue(owner,null) as Collider2D;
      float floor=CastleGround.Position(owner).y;
      lift=body!=null?Math.Max(0,floor-body.bounds.min.y):0;groundOffsets[owner]=lift;
     }
    }else if(foot!=null)lift=Math.Max(0,foot.bounds.center.y-source.bounds.min.y);
    var partNames=new List<string>{source.sprite.name};
    // Stop before the actor root: a boss called Wall must not make its entire body static.
    for(var parent=source.transform;parent!=null;parent=parent.parent){
     if(actor&&owner!=null&&parent==owner.transform)break;
     partNames.Add(parent.name);if(owner!=null&&parent==owner.transform)break;
    }
    string name=string.Join(" ",partNames).ToLowerInvariant();
    bool vertical=CastleStageClassification.Vertical(partNames);
    if(source.sprite.name=="castle_trap_sting"){actor=false;owner=source.transform;ground=source.transform;foot=null;lift=0;}
    entries.Add(new Entry{Source=source,Creature=owner,Foot=foot,Ground=ground,GroundLift=lift,Actor=source.sprite.name!="castle_trap_sting"&&CastleStageClassification.Billboard(actor,partNames),Floor=!actor&&!vertical&&(name.Contains("ground")||name.Contains("floor")||name.Contains("tile")||name.Contains("hay")||name.Contains("straw")||name.Contains("carpet")||name.Contains("rug")||name.Contains("castle_trap_base")||name.Contains("castle_trap_sting"))});
   }
  }
  EnsureCube();
  if(stageLight!=null)stageLight.enabled=true;
  if(stone!=null&&stone.mainTexture==null)foreach(var entry in entries){
   var sprite=entry.Source.sprite;if(sprite==null||!sprite.name.StartsWith("castle_wall",StringComparison.OrdinalIgnoreCase))continue;
   var uv=sprite.uv;var min=new Vector2(1,1);var max=Vector2.zero;foreach(var point in uv){min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
   stone.mainTexture=sprite.texture;stone.mainTextureScale=max-min;stone.mainTextureOffset=min;stone.color=Color.white;indoor=true;roofHeight=Math.Max(CastleCollision.WallHeightMc*Coords.Scale,entry.Source.bounds.size.y+.15f);break;
  }
  var bounds=collision.RoomBounds;
  if(bounds.width>0&&bounds.height>0)Box(new Bounds(new Vector3(bounds.center.x,-.12f,bounds.center.y*Coords.Scale/Coords.DepthScale),new Vector3(bounds.width,.24f,bounds.height*Coords.Scale/Coords.DepthScale)));
  // Retail Castle stages have no upward-facing surface. Reconstruct their indoor
  // roof from the same local wall atlas, keeping it above the native wall art.
  if(indoor&&bounds.width>0&&bounds.height>0)Box(new Bounds(new Vector3(bounds.center.x,roofHeight+.12f,bounds.center.y*Coords.Scale/Coords.DepthScale),new Vector3(bounds.width,.24f,bounds.height*Coords.Scale/Coords.DepthScale)));
  foreach(var wall in walls)Box(new Bounds(new Vector3(wall.center.x,CastleCollision.WallHeightMc*Coords.Scale*.5f,wall.center.y*Coords.Scale/Coords.DepthScale),new Vector3(Math.Max(.1f,wall.size.x),CastleCollision.WallHeightMc*Coords.Scale,Math.Max(.1f,wall.size.y*Coords.Scale/Coords.DepthScale))));
  int newMeshes=0;
  float angle=yaw*Mathf.Deg2Rad;
  var toward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
  // Rotate the work order so animated crowds cannot starve later renderers.
  for(int step=0;step<entries.Count;step++){
   var entry=entries[(spriteCursor+step)%entries.Count];
   var source=entry.Source;if(source==null||!source.enabled||!source.gameObject.activeInHierarchy||source.sprite==null||source.color.a<=.001f)continue;
   var sprite=source.sprite;
   float floor=Ground(entry);
   var matrix=source.transform.localToWorldMatrix;
   matrix.m13+=entry.GroundLift;
   // Preserve the native animated XY pose; remove sprite-sorting Z and give it actual depth.
   matrix.m02=0;matrix.m12=.6f*.15f;matrix.m20=matrix.m21=0;matrix.m22=.36f*.15f;matrix.m23=Coords.ZBase+floor*.6f;
   if(entry.Floor){matrix.m20=matrix.m10*.6f;matrix.m21=matrix.m11*.6f;matrix.m23=Coords.ZBase+matrix.m13*.6f;matrix.m13+=.008f;}
   if(!entry.Floor&&voxelModels.TryDraw(source,entry.Creature,matrix,Layer,nativeTextures,floor,entry.GroundLift))continue;
   if(!entry.Floor&&cuboidModels.TryDraw(source,entry.Creature,matrix,Layer))continue;
   var isolated=nativeTextures.Get(sprite);
   if(isolated!=null&&!isolatedMeshes.Contains(sprite)&&meshes.TryGetValue(sprite,out var legacy)&&newMeshes<8){
    UnityEngine.Object.Destroy(legacy);meshes.Remove(sprite);
    foreach(bool blended in new[]{false,true})if(materials.TryGetValue((sprite,blended),out var legacyMaterial)){UnityEngine.Object.Destroy(legacyMaterial);materials.Remove((sprite,blended));}
   }
   if(!meshes.TryGetValue(sprite,out var mesh)){
    if(newMeshes>=8){
     if(!lastSprites.TryGetValue(source,out sprite)||!meshes.TryGetValue(sprite,out mesh))continue;
    }else{newMeshes++;meshes[sprite]=mesh=Extrude(sprite);}
   }
   lastSprites[source]=sprite;
   var nativeSurface=isolatedMeshes.Contains(sprite)?nativeTextures.Get(sprite):null;
   bool blendSurface=source.color.a<.999f||nativeSurface!=null&&nativeSurface.Translucent;
   if(!materials.TryGetValue((sprite,blendSurface),out var mat)){
    var native=nativeSurface;
    var shader=blendSurface?Shader.Find("Sprites/Default"):Shader.Find("Legacy Shaders/Transparent/Cutout/VertexLit");
    if(shader==null||!shader.isSupported)shader=Shader.Find("Legacy Shaders/VertexLit")??Shader.Find("Unlit/Texture");
    if(shader==null||!shader.isSupported)continue;
    mat=new Material(shader){mainTexture=native?.Texture??sprite.texture,hideFlags=HideFlags.HideAndDontSave};
    mat.SetFloat("_Cutoff",.1f);mat.renderQueue=(int)(blendSurface?RenderQueue.Transparent:RenderQueue.AlphaTest);materials[(sprite,blendSurface)]=mat;
   }
   if(entry.Actor&&entry.Creature!=null&&!entry.Floor){
    // Native characters are animated sprites. Keep their silhouette facing the viewer
    // in canonical space, then apply the same projection used by guest geometry.
    {
     var canonical=Coords.Unproject*matrix;
     // Every part uses the same creature ground pivot. Rotating each head, leg or
     // weapon around itself scatters a skeletal sprite into disconnected pieces.
     var pivot=new Vector3(entry.Creature.transform.position.x,0,floor*Coords.Scale/Coords.DepthScale);
     var anchor=(Vector3)canonical.GetColumn(3);
     // A camera-facing billboard uses view orientation. Eye-to-pivot direction
     // reverses abruptly when the camera crosses a nearby creature's origin.
     if(toward.sqrMagnitude>.0001f){
      var right=Vector3.Cross(Vector3.up,toward.normalized);
      var facing=Matrix4x4.identity;
      var x=right*canonical.m00+Vector3.up*canonical.m10;
      var y=right*canonical.m01+Vector3.up*canonical.m11;
      // Preserve the original part pose and layer order in a small depth band.
      var depth=toward.normalized;
      var position=pivot+right*(anchor.x-pivot.x)+Vector3.up*anchor.y;
      position+=depth*(source.transform.position.z-entry.Creature.transform.position.z)*.004f;
      facing.SetColumn(0,new Vector4(x.x,x.y,x.z,0));
      facing.SetColumn(1,new Vector4(y.x,y.y,y.z,0));
      facing.SetColumn(2,new Vector4(depth.x*.002f,0,depth.z*.002f,0));
      facing.SetColumn(3,new Vector4(position.x,position.y,position.z,1));
      matrix=Coords.Unproject.inverse*facing;
     }
    }
   }
   if(source.flipX){matrix.m00=-matrix.m00;matrix.m10=-matrix.m10;matrix.m20=-matrix.m20;}
   if(source.flipY){matrix.m01=-matrix.m01;matrix.m11=-matrix.m11;matrix.m21=-matrix.m21;}
   properties.Clear();properties.SetColor("_Color",source.color);
   Graphics.DrawMesh(mesh,matrix,mat,Layer,null,0,properties,ShadowCastingMode.Off,false);
  }
  if(entries.Count>0)spriteCursor=(spriteCursor+8)%entries.Count;
  // Native particles continue simulating in Castle coordinates. Bake the live renderer
  // rather than taking a still image of an attack, hit or status effect.
  var main=Camera.main;
  if(main!=null)foreach(var pair in particles){
   var renderer=pair.Key;if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
   var system=renderer.GetComponent<ParticleSystem>();if(system==null||system.particleCount==0)continue;
   renderer.BakeMesh(pair.Value,main,false);
   var owner=creatureType==null?null:renderer.GetComponentInParent(creatureType);
   float floor=owner!=null?owner.transform.position.y:renderer.transform.position.y;
   var projection=Matrix4x4.identity;projection.m02=projection.m12=projection.m20=projection.m21=projection.m22=0;projection.m23=Coords.ZBase+floor*.6f;
   var native=system.main.simulationSpace==ParticleSystemSimulationSpace.World?Matrix4x4.identity:renderer.transform.localToWorldMatrix;
   var mats=renderer.sharedMaterials;
   for(int sub=0;sub<pair.Value.subMeshCount&&sub<mats.Length;sub++)if(mats[sub]!=null)Graphics.DrawMesh(pair.Value,projection*native,mats[sub],Layer,null,sub,null,ShadowCastingMode.Off,false);
  }
 }
 private static bool Vertical(string name)=>name.Contains("wall")||name.Contains("gate")||name.Contains("door")||name.Contains("column")||name.Contains("roof");
 private static float Ground(Entry entry)=>entry.Foot!=null?entry.Foot.bounds.center.y:entry.Ground!=null?entry.Ground.position.y:entry.Creature!=null?entry.Creature.transform.position.y:entry.Source.bounds.min.y;
 private void EnsureCube(){
  if(cube!=null)return;
  var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=UnityEngine.Object.Instantiate(temporary.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.Destroy(temporary);
  var shader=Shader.Find("Legacy Shaders/VertexLit")??Shader.Find("Unlit/Texture");
  if(shader!=null)stone=new Material(shader){color=new Color(.32f,.29f,.26f),hideFlags=HideFlags.HideAndDontSave};
  var lightObject=new GameObject("CastleCraft native stage illumination");lightObject.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(lightObject);stageLight=lightObject.AddComponent<Light>();stageLight.type=LightType.Directional;stageLight.cullingMask=1<<Layer;stageLight.intensity=.8f;stageLight.color=new Color(1,.95f,.86f);lightObject.transform.rotation=Quaternion.Euler(50,-30,0);
 }
 private void Box(Bounds b){if(cube==null||stone==null)return;Graphics.DrawMesh(cube,Coords.Unproject.inverse*Matrix4x4.TRS(b.center,Quaternion.identity,b.size),stone,Layer,null,0,null,ShadowCastingMode.Off,false);}
 private (int width,int height,byte[] alpha) Mask(Texture2D texture)=>nativeTextures.AtlasMask(texture);
 private Mesh Extrude(Sprite sprite){
  var vertices=sprite.vertices;var uv=sprite.uv;var triangles=sprite.triangles;var native=nativeTextures.Get(sprite);
  var mask=native?.Mask??Mask(sprite.texture);
  if(native!=null){isolatedMeshes.Add(sprite);for(int i=0;i<uv.Length;i++)uv[i]=native.Uv(vertices[i]);}
  var points=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();var edges=new CastleBoundaryEdges();
  for(int i=0;i<triangles.Length;i+=3){
   int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
   var weights=OpaqueClip.Triangle(mask,uv[a],uv[b],uv[c]);
   for(int j=0;j<weights.Length;j+=3){
    int first=points.Count;
    for(int side=0;side<2;side++)for(int k=0;k<3;k++){
     var w=weights[j+k];var v=vertices[a]*w.x+vertices[b]*w.y+vertices[c]*w.z;
     points.Add(new Vector3(v.x,v.y,side==0?-.5f:.5f));uvs.Add(uv[a]*w.x+uv[b]*w.y+uv[c]*w.z);
    }
    indices.AddRange(new[]{first,first+1,first+2,first+5,first+4,first+3});
    for(int k=0;k<3;k++){int n=(k+1)%3;var aPoint=points[first+k];var bPoint=points[first+n];edges.Add(aPoint.x,aPoint.y,bPoint.x,bPoint.y,first+k,first+n,first+n+3,first+k+3);}
   }
  }
  foreach(var edge in edges.Triangles())indices.AddRange(edge);
  var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetUVs(0,uvs);var colors=new Color32[points.Count];for(int i=0;i<colors.Length;i++)colors[i]=new Color32(255,255,255,255);mesh.colors32=colors;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
}
