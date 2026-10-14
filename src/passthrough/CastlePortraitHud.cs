using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
namespace PeakCraft.World;

/// <summary>Attach to the native owner's exact portrait node, preserving the frame and bars.</summary>
internal sealed class CastlePortraitHud {
 private Transform? node;
 private readonly Dictionary<Renderer,bool> hidden=new();
 private GameObject? replacement;
 private RawImage? picture;
 private Sprite? maskSprite;
 private Texture2D? maskTexture;
 private float nextScan;
 public void Update(bool owned,Texture? texture,Component? hero){
  if(!owned||hero==null){Release();return;}
  if(node!=null&&!node.gameObject.activeInHierarchy)Release();
  if(node!=null&&(replacement==null||picture==null))Create();
  if(Time.unscaledTime>=nextScan){
   nextScan=Time.unscaledTime+.5f;
   if(node==null){
    var type=AccessTools.TypeByName("Teran.UICharacterStatus");
    if(type!=null)foreach(Component status in Resources.FindObjectsOfTypeAll(type)){
     if(!status.gameObject.scene.IsValid()||!status.gameObject.activeInHierarchy||!Equals(AccessTools.Field(type,"owner")?.GetValue(status),hero))continue;
     node=status.transform.Find("status/head_portrait");if(node!=null){Create();Plugin.Log.LogInfo("Live Minecraft portrait attached to owner UICharacterStatus/status/head_portrait");break;}
    }
   }
   if(node!=null&&texture!=null)foreach(var renderer in node.GetComponentsInChildren<Renderer>(true)){
    if(!hidden.ContainsKey(renderer))hidden[renderer]=renderer.enabled;
    renderer.enabled=false;
   }
  }
  if(picture!=null&&texture!=null){picture.texture=texture;replacement!.SetActive(true);}
 }
 private void Create(){
  if(maskSprite==null){
   maskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};var pixels=new Color32[4096];
   for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=x-31.5f,dy=y-31.5f;pixels[y*64+x]=dx*dx+dy*dy<31*31?new Color32(255,255,255,255):new Color32(0,0,0,0);}
   maskTexture.SetPixels32(pixels);maskTexture.Apply();maskSprite=Sprite.Create(maskTexture,new Rect(0,0,64,64),new Vector2(.5f,.5f));
  }
  replacement=new GameObject("CastleCraft live Minecraft portrait",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Mask));
  var root=(RectTransform)replacement.transform;root.SetParent(node,false);root.localPosition=new Vector3(0,0,-1);root.sizeDelta=new Vector2(.98f,.98f);root.localScale=Vector3.one;
  replacement.GetComponent<Image>().sprite=maskSprite;replacement.GetComponent<Image>().raycastTarget=false;replacement.GetComponent<Mask>().showMaskGraphic=false;
  var child=new GameObject("Minecraft current model",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));var rect=(RectTransform)child.transform;rect.SetParent(root,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;picture=child.GetComponent<RawImage>();picture.raycastTarget=false;replacement.SetActive(false);
 }
 public void Release(){foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;hidden.Clear();node=null;picture=null;if(replacement!=null)UnityEngine.Object.Destroy(replacement);replacement=null;}
 public void Dispose(){Release();if(maskSprite!=null)UnityEngine.Object.Destroy(maskSprite);if(maskTexture!=null)UnityEngine.Object.Destroy(maskTexture);}
}
