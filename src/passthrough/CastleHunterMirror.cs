using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace PeakCraft.World;
/// Existing actor stream carries identity/action; render receipt hides only the native body.
internal static class CastleHunterMirror {
 internal static readonly Type HunterType=AccessTools.TypeByName("Teran.Monster");
 private static readonly Type first=AccessTools.TypeByName("Teran.AI_Boss_EvilHunter"),second=AccessTools.TypeByName("Teran.AI_Boss_EvilHunter_P2");
 private static readonly Dictionary<Component,float> ready=new();
 private static readonly Dictionary<Renderer,bool> hidden=new();
 internal static readonly CastleHunterChoice Choice=new();
 internal static bool IsReplacement(Component c)=>Choice.Active&&IsHunter(c)&&c.gameObject.scene.handle==Choice.SceneHandle;
 internal static void ObserveActor(Component c){
  if(!IsHunter(c))return;
  var animator=c.GetComponent<Animator>();
  Choice.Recognize(c.gameObject.scene.handle,c.gameObject.activeInHierarchy&&animator!=null&&animator.runtimeAnimatorController!=null);
 }
 internal static bool IsHunter(Component c)=>c!=null&&HunterType!=null&&HunterType.IsInstanceOfType(c)
  &&(first!=null&&c.GetComponent(first)!=null||second!=null&&c.GetComponent(second)!=null);
 internal static uint Flags(Component c){
  if(!IsReplacement(c))return 0;
  uint flags=1024;
  var ai=c.GetComponent(first)??c.GetComponent(second);
  bool running=ai!=null&&(bool)(AccessTools.Field(ai.GetType(),"isStartAI")?.GetValue(ai)??false);
  bool dying=ai!=null&&(bool)(AccessTools.Field(ai.GetType(),"isBossDied")?.GetValue(ai)??false);
  if(!running||dying)flags|=32768; // Presentation continues; combat actions are inhibited.
  var animator=c.GetComponent<Animator>();
  if(animator!=null){
   var state=animator.GetCurrentAnimatorStateInfo(0);
   string targetField=state.IsName("Base Layer.skill")?"cast_skill_target":state.IsName("Base Layer.dashAttack")?"cast_dashAtk_target":"";
   for(int i=0;i<4;i++)if(state.IsName("Base Layer.attack"+i))flags|=2048;
   for(int i=0;i<4;i++)if(state.IsName("Base Layer.attack"+i))targetField="cast_attack"+i+"_target";
   if(state.IsName("Base Layer.dashAttack"))flags|=2048;
   if(state.IsName("Base Layer.skill"))flags|=2048|16384;
   // Actual SHW coroutine sets Monster.damage_factor=.3 during attack1.
   if(state.IsName("Base Layer.attack1")&&Convert.ToSingle(AccessTools.Field(c.GetType(),"damage_factor")?.GetValue(c)??1f)<.99f)flags|=4096;
   float progress=state.loop?Mathf.Repeat(state.normalizedTime,1):Mathf.Clamp01(state.normalizedTime);
   flags|=(uint)Math.Round(progress*65535)<<16;
   var target=ai==null||targetField==""?null:AccessTools.Field(ai.GetType(),targetField)?.GetValue(ai) as Transform;
   if(CastleMobTargets.CurrentEpoch>=0)Plugin.Instance.Link.PushInput((PeakCraft.Link.Proto.InputType)23,0,c.GetInstanceID(),CastleMobTargets.TargetId(target),CastleMobTargets.CurrentEpoch);
  }
  int direction=Convert.ToInt32(AccessTools.Method(c.GetType(),"getDir",Type.EmptyTypes)?.Invoke(c,null)??1);
  if(direction>0)flags|=8192;
  return flags;
 }
 internal static ushort State(Component c){
  if(!IsReplacement(c))return 0;
  var animator=c.GetComponent<Animator>();int action=-1;
  if(animator!=null){var state=animator.GetCurrentAnimatorStateInfo(0);for(int i=0;i<CastleHunterState.Names.Length;i++)if(state.IsName("Base Layer."+CastleHunterState.Names[i])){action=i;break;}}
  return CastleHunterState.Encode(action,second!=null&&c.GetComponent(second)!=null);
 }
 internal static void Acknowledge(Component c){if(IsReplacement(c)){if(!ready.ContainsKey(c))Plugin.Log.LogInfo($"Hunter mirror render receipt: actor={c.GetInstanceID()}, nativeBody replacement enabled");ready[c]=Time.unscaledTime+1;}}
 internal static void Update(bool active){
  foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;
  hidden.Clear();
  foreach(var c in new List<Component>(ready.Keys)){
   if(!active||c==null||!IsReplacement(c)||Time.unscaledTime>ready[c]){ready.Remove(c);continue;}
   var shadow=CastleGround.Shadow(c);
   foreach(var r in c.GetComponentsInChildren<Renderer>(true)){
    if(r is ParticleSystemRenderer||shadow!=null&&r.transform.IsChildOf(shadow.transform))continue;
    hidden[r]=r.enabled;r.enabled=false;
   }
  }
 }
}
