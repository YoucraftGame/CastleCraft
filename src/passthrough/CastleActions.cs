using System;
using System.Reflection;
using HarmonyLib;
using PeakCraft.Link;
using UnityEngine;

namespace PeakCraft.World;

/// <summary>Reports the real castle animation, after native input has been accepted.</summary>
internal sealed class CastleActions {
 private readonly PeakLink link;
 private Component? observedHero;
 private object? nativeAnim;
 private Type? categoryType,facingType;
 private PropertyInfo? animProperty,weaponProperty,normalizedTime,fullPathHash;
 private FieldInfo? stateField,categoryField;
 private MethodInfo? isSkill,isAttack,isJumpAttack,isDashAttack,isSpecial,isHurt,isName,changeDirection;
 private float nextSend,lastProgress;
 private int lastState,lastCode=-1,lastFlags=-1;
 public bool NativeWeaponVisible{get;private set;}
 public int Action{get;private set;}
 public CastleActions(PeakLink link){this.link=link;}
 public void Tick(Component? hero,bool owned,bool castleMouse,bool scripted=false,bool paused=false){
  if(!owned||hero==null){if(lastFlags!=0)Send(0,0,0,0);NativeWeaponVisible=false;Action=0;observedHero=null;nativeAnim=null;return;}
  if(observedHero!=hero){
   observedHero=hero;var type=hero.GetType();animProperty=AccessTools.Property(type,"anim");weaponProperty=AccessTools.Property(type,"Weapon");
   BindAnimation(animProperty?.GetValue(hero,null));lastCode=-1;lastState=0;lastProgress=0;nextSend=0;
  }else if(nativeAnim==null){BindAnimation(animProperty?.GetValue(hero,null));}
  object? weapon=weaponProperty?.GetValue(hero,null);
  int category=0;
  if(weapon!=null){
   if(categoryType!=weapon.GetType()){categoryType=weapon.GetType();categoryField=AccessTools.Field(categoryType,"category");}
   category=Category(categoryField?.GetValue(weapon)?.ToString()??"");
  }
  var animator=hero.GetComponent<Animator>();
  object? state=animator!=null&&animator.isInitialized?(object)animator.GetCurrentAnimatorStateInfo(0):nativeAnim==null?null:stateField?.GetValue(nativeAnim);
  int stateId=state==null||fullPathHash==null?0:Convert.ToInt32(fullPathHash.GetValue(state,null));
  float progress=state==null||normalizedTime==null?0:Convert.ToSingle(normalizedTime.GetValue(state,null));
  if(float.IsNaN(progress)||float.IsInfinity(progress))progress=0;
  int code=0;
  if(True(isHurt))code=5;
  else if(True(isSkill))code=3;
  else if(True(isSpecial))code=2;
  else if(True(isAttack)||True(isJumpAttack)||True(isDashAttack))code=1;
  else if(state!=null&&(Named(state,"activeItem")||Named(state,"drinkPotion")||Named(state,"useItem")))code=4;
  if(scripted&&state!=null){
   if(Named(state,"move"))code=6;
   else if(Named(state,"run"))code=7;
   else if(Named(state,"jump")||Named(state,"jump_attack_end"))code=8;
   else if(Named(state,"fellDown"))code=9;
   else if(Named(state,"revive"))code=10;
  }
  if(code==6||code==7)progress=Mathf.Repeat(progress,1);
  bool explicitNativeAction=!scripted&&Input.GetKey(KeyCode.LeftAlt)&&(code==1||code==2||code==3);
  Action=code;NativeWeaponVisible=!scripted&&weapon!=null&&(castleMouse||explicitNativeAction);
  int flags=1|(!scripted&&castleMouse?2:0)|(weapon!=null?4:0)|(explicitNativeAction?16:0)|(scripted?32:0)|(paused?64:0);
  bool changed=code!=lastCode||stateId!=lastState||(code!=0&&progress+.2f<lastProgress);
  if(Time.unscaledTime>=nextSend||changed||flags!=lastFlags){
   nextSend=Time.unscaledTime+.05f;Send(code,(int)Math.Round(Mathf.Clamp01(progress)*10000),category,flags|(changed?8:0));lastState=stateId;lastFlags=flags;
  }
  lastProgress=progress;
 }
 /// <summary>Set the real two-direction native weapon heading from the Minecraft aim before HandleInput.</summary>
 public void Face(Component? hero,float guestYaw){
  if(hero==null||float.IsNaN(guestYaw)||float.IsInfinity(guestYaw))return;
  float horizontal=-Mathf.Sin(guestYaw*Mathf.Deg2Rad);if(Math.Abs(horizontal)<.05f)return;
  if(facingType!=hero.GetType()){facingType=hero.GetType();changeDirection=AccessTools.Method(facingType,"changeDir",new[]{typeof(int)});}
  changeDirection?.Invoke(hero,new object[]{horizontal>0?1:-1});
 }
 /// <summary>Feed directional skills without invoking the native walking motor.</summary>
 public void Direction(Component? hero,float yaw,bool firstPerson,Vector2 movement){
  if(hero==null)return;var property=AccessTools.Property(hero.GetType(),"InputDirValue");if(property==null)return;
  var direction=property.GetValue(hero,null)??Activator.CreateInstance(property.PropertyType);
  float x=movement.x,z=movement.y;
  if(firstPerson){float angle=(yaw-180)*Mathf.Deg2Rad,c=Mathf.Cos(angle),sin=Mathf.Sin(angle);float nx=x*c-z*sin;z=x*sin+z*c;x=nx;}
  AccessTools.Field(property.PropertyType,"left").SetValue(direction,Math.Max(0,-x));AccessTools.Field(property.PropertyType,"right").SetValue(direction,Math.Max(0,x));
  AccessTools.Field(property.PropertyType,"up").SetValue(direction,Math.Max(0,-z));AccessTools.Field(property.PropertyType,"down").SetValue(direction,Math.Max(0,z));
  property.SetValue(hero,direction,null);
 }
 private void BindAnimation(object? anim){
  nativeAnim=anim;stateField=null;normalizedTime=fullPathHash=null;isSkill=isAttack=isJumpAttack=isDashAttack=isSpecial=isHurt=isName=null;
  if(anim==null)return;
  var type=anim.GetType();stateField=AccessTools.Field(type,"cur_stateInfo");isSkill=AccessTools.Method(type,"IsDoingSkill");isAttack=AccessTools.Method(type,"isCommonAttack");isJumpAttack=AccessTools.Method(type,"isJumpAttack");isDashAttack=AccessTools.Method(type,"IsDashAttack");isSpecial=AccessTools.Method(type,"isSpecialAction");isHurt=AccessTools.Method(type,"IsBeAttacked");
  var stateType=stateField?.FieldType;if(stateType==null)return;
  normalizedTime=AccessTools.Property(stateType,"normalizedTime");fullPathHash=AccessTools.Property(stateType,"fullPathHash");isName=AccessTools.Method(stateType,"IsName",new[]{typeof(string)});
 }
 private bool True(MethodInfo? method)=>nativeAnim!=null&&method!=null&&method.Invoke(nativeAnim,null) is bool value&&value;
 private bool Named(object state,string name)=>isName!=null&&isName.Invoke(state,new object[]{name}) is bool value&&value;
 private void Send(int code,int progress,int category,int flags){link.PushInput((Proto.InputType)13,(ushort)code,progress,category,flags);lastCode=code;lastFlags=flags&~8;}
 private static int Category(string category)=>category.ToUpperInvariant() switch{"BOW"=>1,"GUN"=>2,"STAFF"=>3,"SINGLE_HANDED_MELEE"=>4,"SHW"=>4,"TWO_HANDED_MELEE"=>5,"SPEAR"=>6,"DUAL_SWORD"=>7,"DUAL_BLADE"=>7,"DUAL_WEAPON"=>7,_=>0};
 public void Release(){NativeWeaponVisible=false;Action=0;observedHero=null;BindAnimation(null);lastProgress=0;Send(0,0,0,0);}
}
