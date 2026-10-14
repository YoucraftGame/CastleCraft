using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PeakCraft.World;

/// <summary>Draws native weapon sprites at exported Minecraft hand positions, without copying any native behaviour.</summary>
internal sealed unsafe class CastleEquipment : IDisposable {
 private sealed class Part {
  public SpriteRenderer Source=null!,Draw=null!;
  public Transform Mount=null!;
  public bool Right,WasEnabled;
 }
 private readonly List<Part> parts=new();
 private Component? hero;
 private object? weapon;
 private PropertyInfo? weaponProperty;
 private Vector3 rightAnchor,leftAnchor;
 private Quaternion rightRotation=Quaternion.identity,leftRotation=Quaternion.identity;
 private bool orientedAnchors;
 private bool hasAnchors;
 private float anchorsAt,nextScan;
 private SpriteRenderer? body;
 private Camera? handCamera;
 private RenderTexture? handTexture;
 private bool showHandOverlay;
 private const int HandLayer=27;
 public int VisibleParts{get;private set;}
 public void ReadAnchors(byte* data,uint bytes){
  if(bytes!=24&&bytes!=96)return;float* f=(float*)data;
  for(int i=0;i<bytes/4;i++)if(float.IsNaN(f[i])||float.IsInfinity(f[i])||Math.Abs(f[i])>8)return;
  rightAnchor=new Vector3(f[0],f[1],f[2]);leftAnchor=new Vector3(f[3],f[4],f[5]);hasAnchors=true;anchorsAt=Time.unscaledTime;
  orientedAnchors=bytes==96;
  if(orientedAnchors){rightRotation=Rotation(f+6);leftRotation=Rotation(f+15);}
 }
 private static Quaternion Rotation(float* f){var x=new Vector3(f[0],f[1],-f[2]);var up=new Vector3(f[3],f[4],-f[5]);var forward=Vector3.Cross(x,up);return forward.sqrMagnitude>.001f&&up.sqrMagnitude>.001f?Quaternion.LookRotation(forward,up):Quaternion.identity;}
 public void LateUpdate(Component? nativeHero,bool owned,Vector3 feet,bool showNativeWeapon,bool firstPerson=false){
  if(!owned||nativeHero==null){Release();return;}
  if(hero!=nativeHero){Release();hero=nativeHero;weaponProperty=AccessTools.Property(hero.GetType(),"Weapon");body=hero.transform.Find("noumenon/body")?.GetComponent<SpriteRenderer>();nextScan=0;}
  var equipped=weaponProperty?.GetValue(hero,null);
  if(!ReferenceEquals(weapon,equipped)||Time.unscaledTime>=nextScan){weapon=equipped;nextScan=Time.unscaledTime+.25f;Refresh();}
  bool anchorsReady=hasAnchors&&Time.unscaledTime-anchorsAt<.5f;
  showHandOverlay=firstPerson&&showNativeWeapon;
  if(showHandOverlay)EnsureHandCamera();
  VisibleParts=0;
  foreach(var part in parts){
   if(part.Source==null||part.Draw==null||part.Mount==null)continue;
   var source=part.Source;source.enabled=false;
   bool visible=showNativeWeapon&&(firstPerson||anchorsReady)&&source.gameObject.activeInHierarchy&&source.sprite!=null;
   part.Draw.enabled=visible;if(!visible)continue;
   var anchor=part.Right?rightAnchor:leftAnchor;
   // Avatar vertices are projected by its private camera at .8 castle units per MC unit.
   // Keep the weapon's native animated local offset/rotation, but move its grip to the posed MC hand.
   Vector3 position=firstPerson?new Vector3(part.Right?.1f:-.65f,-.25f,0):feet+new Vector3(anchor.x*Coords.Scale,anchor.y*Coords.Scale,0);
   Vector3 offset=source.transform.position-part.Mount.position;position+=new Vector3(offset.x,offset.y,0);
   position.z=firstPerson?(source.transform.position.z-part.Mount.position.z)*.01f:feet.z-.002f;
   var draw=part.Draw;
   if(!firstPerson&&orientedAnchors){
    // Draw in the same private 3D avatar camera as the Minecraft hand. This
    // retains depth/yaw/pitch instead of dropping the hand's Z component.
    var rotation=part.Right?rightRotation:leftRotation;
    var relative=part.Mount.worldToLocalMatrix*source.transform.localToWorldMatrix;
    var local=(Vector3)relative.GetColumn(3);
    position=new Vector3(anchor.x,anchor.y,-anchor.z)*Coords.Scale+rotation*local;
    draw.transform.SetPositionAndRotation(position,rotation*relative.rotation);draw.transform.localScale=relative.lossyScale;
   }else{draw.transform.SetPositionAndRotation(position,source.transform.rotation);draw.transform.localScale=source.transform.lossyScale;}
   draw.sprite=source.sprite;draw.color=source.color;draw.flipX=source.flipX;draw.flipY=source.flipY;draw.sharedMaterial=source.sharedMaterial;
   draw.sortingLayerID=Coords.SortingLayer;draw.sortingOrder=source.sortingOrder;draw.gameObject.layer=firstPerson?HandLayer:orientedAnchors?Coords.AvatarLayer:Coords.RenderLayer;
   VisibleParts++;
  }
  if(showHandOverlay&&handCamera!=null)handCamera.Render();
 }
 private void EnsureHandCamera(){
  if(handCamera!=null)return;
  handTexture=new RenderTexture(384,384,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};handTexture.Create();
  var go=new GameObject("CastleCraft native first-person weapon camera"){hideFlags=HideFlags.HideAndDontSave};UnityEngine.Object.DontDestroyOnLoad(go);
  handCamera=go.AddComponent<Camera>();handCamera.enabled=false;handCamera.orthographic=true;handCamera.orthographicSize=1.25f;handCamera.aspect=1;handCamera.nearClipPlane=.1f;handCamera.farClipPlane=10;handCamera.cullingMask=1<<HandLayer;handCamera.clearFlags=CameraClearFlags.SolidColor;handCamera.backgroundColor=Color.clear;handCamera.targetTexture=handTexture;handCamera.transform.position=new Vector3(0,.35f,-5);
 }
 public void DrawFirstPerson(){if(!showHandOverlay||handTexture==null||Event.current.type!=EventType.Repaint)return;float size=Screen.height*.62f;GUI.DrawTexture(new Rect(Screen.width-size,Screen.height-size,size,size),handTexture,ScaleMode.StretchToFill,true);}
 private void Refresh(){
  for(int i=parts.Count-1;i>=0;i--){var part=parts[i];if(part.Source==null||part.Mount==null){if(part.Draw!=null)UnityEngine.Object.Destroy(part.Draw.gameObject);parts.RemoveAt(i);}else if(part.Draw==null)part.Draw=CreateDrawing(part.Source);}
  if(hero==null)return;
  AddBranch(hero.transform.Find("noumenon/Rhand/Rweapon"),true);
  AddBranch(hero.transform.Find("noumenon/Lhand/Lweapon"),false);
 }
 private void AddBranch(Transform? mount,bool right){
  if(mount==null)return;
  foreach(var source in mount.GetComponentsInChildren<SpriteRenderer>(true)){
   if(parts.Exists(p=>p.Source==source))continue;
   // Particle systems, scripts, hitboxes and projectiles stay exclusively in the native game.
   parts.Add(new Part{Source=source,Draw=CreateDrawing(source),Mount=mount,Right=right,WasEnabled=source.enabled});
  }
 }
 private static SpriteRenderer CreateDrawing(SpriteRenderer source){var go=new GameObject("CastleCraft held "+source.name){hideFlags=HideFlags.HideAndDontSave};var draw=go.AddComponent<SpriteRenderer>();draw.enabled=false;return draw;}
 /// <summary>Call before the host restores its hidden renderer list.</summary>
 public void Release(){
  foreach(var part in parts){if(part.Source!=null)part.Source.enabled=part.WasEnabled;if(part.Draw!=null)UnityEngine.Object.Destroy(part.Draw.gameObject);}
  parts.Clear();hero=null;weapon=null;body=null;VisibleParts=0;hasAnchors=false;anchorsAt=0;showHandOverlay=false;
 }
 public void Dispose(){Release();if(handCamera!=null)UnityEngine.Object.Destroy(handCamera.gameObject);if(handTexture!=null){handTexture.Release();UnityEngine.Object.Destroy(handTexture);}handCamera=null;handTexture=null;}
}
