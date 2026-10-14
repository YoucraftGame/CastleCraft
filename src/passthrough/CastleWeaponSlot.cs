using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PeakCraft.World;

/// <summary>A view of the real equipped weapon, independent of Minecraft's nine slots.</summary>
internal sealed class CastleWeaponSlot {
 private Component? hero,weapon;
 private PropertyInfo? weaponProperty;
 private Sprite? icon;
 private Rect iconUv;
 private float nextIcon;
 public int Id=>weapon!=null?weapon.GetInstanceID():0;
 public bool Available=>weapon!=null;
 public void Update(Component? currentHero){
  if(hero!=currentHero){hero=currentHero;weapon=null;icon=null;weaponProperty=hero==null?null:AccessTools.Property(hero.GetType(),"Weapon");}
  var equipped=hero==null?null:weaponProperty?.GetValue(hero,null) as Component;
  if(weapon!=equipped){weapon=equipped;icon=null;nextIcon=0;}
  // Never retain/re-equip a dropped or destroyed weapon. The native Weapon owns
  // lifetime, pickup, durability, skill state and native replacement/drop rules.
  if(weapon==null){icon=null;return;}
  if(Time.unscaledTime<nextIcon)return;
  nextIcon=Time.unscaledTime+.25f;
  var mounts=new[]{hero!.transform.Find("noumenon/Rhand/Rweapon"),hero.transform.Find("noumenon/Lhand/Lweapon")};
  var sprite=mounts.Where(m=>m!=null).SelectMany(m=>m!.GetComponentsInChildren<SpriteRenderer>(true))
   .FirstOrDefault(r=>r.gameObject.activeInHierarchy&&r.sprite!=null)?.sprite;
  if(sprite==null)return;
  icon=sprite;var uv=sprite.uv;
  if(uv.Length>0)iconUv=Rect.MinMaxRect(uv.Min(v=>v.x),uv.Min(v=>v.y),uv.Max(v=>v.x),uv.Max(v=>v.y));
 }
 public void Draw(bool selected,string key){
  float size=Mathf.Clamp(Screen.height/16f,38,64);
  var box=new Rect(Screen.width*.5f-size*5.8f,Screen.height-size-17,size,size);
  var previous=GUI.color;
  GUI.color=selected?new Color(1f,.83f,.32f):new Color(.7f,.7f,.7f);
  GUI.Box(box,"");GUI.color=Color.white;
  if(icon!=null)GUI.DrawTextureWithTexCoords(new Rect(box.x+5,box.y+5,size-10,size-10),icon.texture,iconUv,true);
  GUI.Label(new Rect(box.x-12,box.y-23,size+60,23),key+" 城堡武器");
  GUI.Label(new Rect(box.x+size+5,box.y+4,125,45),Available?(selected?"使用中":"已收纳 · "+key+"取用"):"空槽");
  GUI.color=previous;
 }
}
