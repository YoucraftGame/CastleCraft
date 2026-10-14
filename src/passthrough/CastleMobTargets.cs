using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using PeakCraft.Link;
namespace PeakCraft.World;

/// Native Creature targets live in the same hero_layer as native friendly summons.
internal sealed class CastleMobTarget : MonoBehaviour {
 internal uint MinecraftId;
 internal bool Alive;
 internal int Epoch;
 internal BoxCollider2D Body=null!,Shadow=null!;
}
internal sealed class CastleMobTargets {
 private static readonly Type creatureType=AccessTools.TypeByName("Teran.Creature");
 private static readonly Type bodyType=AccessTools.TypeByName("Teran.BodyCollideDetector");
 // TypeByName enumerates loaded assemblies/types. Resolve metadata once, never per mob update.
 private static readonly Type heroType=AccessTools.TypeByName("Teran.Hero");
 private static readonly System.Reflection.PropertyInfo ddInstance=AccessTools.Property(AccessTools.TypeByName("Teran.DDSystem"),"Instance");
 private static readonly object playerKind=Enum.Parse(AccessTools.TypeByName("Teran.Creature+CreatureType"),"Player");
 private static readonly Dictionary<Component,CastleMobTarget> native=new();
 private readonly Dictionary<uint,Component> targets=new();
 private readonly List<PeakLink.WorldEntity> records=new();
 private float next;private int epoch=-1;
 private static PeakLink? transport;
 internal static int CurrentEpoch {get;private set;}=-1;
 internal static int TargetId(Transform? target){if(target==null)return -1;var marker=target.GetComponentInParent<CastleMobTarget>();if(marker!=null)return marker.Alive?unchecked((int)marker.MinecraftId):-1;return heroType!=null&&target.GetComponentInParent(heroType)!=null?0:-1;}
 private static int nonce;
 private static readonly Type viewType=AccessTools.TypeByName("Photon.Pun.PhotonView"),networkType=AccessTools.TypeByName("Photon.Pun.PhotonNetwork");
 private static readonly System.Reflection.MethodInfo? allocateView=viewType==null?null:AccessTools.Method(networkType,"AllocateViewID",new[]{viewType});
 private float nextTimingLog;
 private int timingSamples,timingCreated;
 private double maxTotalMs,maxReadMs,maxLookupMs,maxSyncMs;
 private static double Elapsed(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
 private static object? Get(object obj,string name)=>CastleMobTargetAccess.Get(obj,name);
 private static void Set(object obj,string name,object value)=>CastleMobTargetAccess.Set(obj,name,value);
 internal static void Install(Harmony harmony){
  harmony.Patch(AccessTools.PropertyGetter(creatureType,"IsAlive"),prefix:new HarmonyMethod(typeof(CastleMobTargets),nameof(Alive)));
  harmony.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Teran.Entity"),"IsOwner"),prefix:new HarmonyMethod(typeof(CastleMobTargets),nameof(Owner)));
  harmony.Patch(AccessTools.Method(creatureType,"BeAttacked",new[]{AccessTools.TypeByName("Teran.AttackInfo"),typeof(float),typeof(bool)}),prefix:new HarmonyMethod(typeof(CastleMobTargets),nameof(Hit)));
  harmony.Patch(AccessTools.Method(creatureType,"GetDebuff",new[]{AccessTools.TypeByName("Teran.Debuff+DebuffType"),typeof(int),typeof(float),typeof(float),typeof(Transform)}),prefix:new HarmonyMethod(typeof(CastleMobTargets),nameof(Debuff)));
 }
 private static bool Alive(Component __instance,ref bool __result){if(!native.TryGetValue(__instance,out var marker))return true;__result=marker!=null&&marker.Alive;return false;}
 private static bool Owner(Component __instance,ref bool __result){if(!native.ContainsKey(__instance))return true;__result=true;return false;}
 private static bool Hit(Component __instance,object __0,ref int __result){
  if(!native.TryGetValue(__instance,out var marker))return true;
  __result=0;if(marker==null||!marker.Alive||transport==null)return false;
  int damage=Convert.ToInt32(Get(__0,"origin_damage")??0);if(damage<=0)return false;
  var attacker=Get(__0,"attacker") as Transform;
  var owner=attacker==null?null:attacker.GetComponentInParent(creatureType);
  int kind=((bool)(Get(__0,"isFlyingObj")??false)?1:0)|(Convert.ToInt32(Get(__0,"hard_value")??0)>=5?4:0);
  Send(marker,21,kind,damage,owner==null?0:owner.GetInstanceID());
  __result=damage;return false;
 }
 private static void Send(CastleMobTarget marker,int type,int kind,int amount,int attacker){
  if(transport==null)return;int serial=(++nonce)&4095;
  transport.PushInput((Proto.InputType)20,(ushort)serial,unchecked((int)marker.MinecraftId),marker.Epoch,attacker);
  transport.PushInput((Proto.InputType)type,(ushort)((serial<<4)|(kind&15)),unchecked((int)marker.MinecraftId),amount,0);
 }
 private static bool Debuff(Component __instance,object __0,int __1,float __2){
  if(!native.TryGetValue(__instance,out var marker))return true;
  if(marker==null||!marker.Alive)return false;
  string name=__0.ToString();int kind=CastleMobTargetRules.Debuff(name),duration=CastleMobTargetRules.Duration(name,__2);
  if(kind!=0&&duration>0)Send(marker,22,kind,duration,0);return false;
 }
 internal void Update(PeakLink link,Component? hero,bool active,int currentEpoch){
  transport=link;
  CurrentEpoch=active?currentEpoch:-1;
  foreach(var c in native.Keys.Where(c=>c==null).ToArray())native.Remove(c);
  if(!active||hero==null){Clear();return;}
  if(currentEpoch!=epoch){Clear();epoch=currentEpoch;}
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+.05f;
  long started=System.Diagnostics.Stopwatch.GetTimestamp();
  if(!link.ReadWorldEntities(records))return;
  double readMs=Elapsed(started);
  // Most rooms have no Minecraft allies. Avoid native singleton/reflection work in that case,
  // but still remove any targets from the preceding snapshot.
  bool hasAlly=false;
  foreach(var e in records)if(e.kind==7&&e.yaw==currentEpoch&&CastleMobTargetRules.Valid(e.id,currentEpoch,currentEpoch,e.pitch,e.x,e.y,e.z,e.scale,e.ext0,e.ext1)){hasAlly=true;break;}
  if(!hasAlly){Clear();return;}
  long lookupStart=System.Diagnostics.Stopwatch.GetTimestamp();
  var dd=ddInstance?.GetValue(null,null);
  var ctrl=dd==null?null:Get(dd,"stageCtrl");var stage=ctrl==null?null:Get(ctrl,"cur_stage");
  var layer=stage==null?null:Get(stage,"hero_layer") as Transform;if(layer==null)return;
  double lookupMs=Elapsed(lookupStart);long syncStart=System.Diagnostics.Stopwatch.GetTimestamp();int created=0;
  var seen=new HashSet<uint>();
  foreach(var e in records){
   if(e.kind!=7||e.yaw!=currentEpoch||!CastleMobTargetRules.Valid(e.id,currentEpoch,currentEpoch,e.pitch,e.x,e.y,e.z,e.scale,e.ext0,e.ext1))continue;
   seen.Add(e.id);
   if(!targets.TryGetValue(e.id,out var c)||c==null){c=Create(e.id,layer,hero);targets[e.id]=c;created++;}
   var marker=native[c];marker.Alive=true;marker.Epoch=currentEpoch;
   c.transform.position=Coords.Feet(e.x,e.z);
   marker.Body.transform.localPosition=new Vector3(0,Math.Max(0,e.y-(float)Coords.YOffset)*Coords.Scale,0);
   marker.Body.size=new Vector2(Mathf.Clamp(e.scale,.1f,32)*Coords.Scale,Mathf.Clamp(e.ext0,.1f,64)*Coords.Scale);
   marker.Body.offset=new Vector2(0,marker.Body.size.y*.5f);
   marker.Shadow.size=new Vector2(marker.Body.size.x,Mathf.Clamp(e.scale,.1f,32)*Coords.DepthScale);
   Set(c,"m_bodySize",new Vector3(marker.Body.size.x,marker.Body.size.y,marker.Shadow.size.y));
   var inspector=Get(c,"propertiesInspector");if(inspector!=null){Set(inspector,"maxHp",Math.Max(1,(int)Math.Min(1000000,e.ext1*5)));Set(inspector,"CurrentHp",Math.Max(1,(int)Math.Min(1000000,e.pitch*5)));}
  }
  foreach(var id in targets.Keys.Where(id=>!seen.Contains(id)).ToArray())Remove(id);
  timingSamples++;timingCreated+=created;
  maxTotalMs=Math.Max(maxTotalMs,Elapsed(started));maxReadMs=Math.Max(maxReadMs,readMs);
  maxLookupMs=Math.Max(maxLookupMs,lookupMs);maxSyncMs=Math.Max(maxSyncMs,Elapsed(syncStart));
  if(Time.unscaledTime>=nextTimingLog){
   nextTimingLog=Time.unscaledTime+5;
   Plugin.Log.LogInfo($"CastleCraft target sync: targets={targets.Count}, samples={timingSamples}, created={timingCreated}, max_ms={maxTotalMs:F3}, read_max_ms={maxReadMs:F3}, lookup_max_ms={maxLookupMs:F3}, sync_max_ms={maxSyncMs:F3}");
   timingSamples=timingCreated=0;maxTotalMs=maxReadMs=maxLookupMs=maxSyncMs=0;
  }
 }
 private Component Create(uint id,Transform layer,Component hero){
  var obj=new GameObject("CastleCraft Minecraft combat target "+id);obj.SetActive(false);obj.transform.SetParent(layer,false);obj.layer=hero.gameObject.layer;
  var marker=obj.AddComponent<CastleMobTarget>();marker.MinecraftId=id;
  if(viewType!=null){var view=obj.AddComponent(viewType);allocateView?.Invoke(null,new object[]{view});}
  var rigid=obj.AddComponent<Rigidbody2D>();rigid.bodyType=RigidbodyType2D.Kinematic;rigid.gravityScale=0;rigid.simulated=true;rigid.useFullKinematicContacts=true;
  var own=Get(hero,"BodyCollider") as Collider2D;
  var body=new GameObject("noumenon");body.transform.SetParent(obj.transform,false);body.layer=own==null?obj.layer:own.gameObject.layer;if(own!=null)body.tag=own.tag;
  marker.Body=body.AddComponent<BoxCollider2D>();marker.Body.isTrigger=true;
  var shadow=new GameObject("shadow");shadow.transform.SetParent(obj.transform,false);marker.Shadow=shadow.AddComponent<BoxCollider2D>();marker.Shadow.isTrigger=true;marker.Shadow.enabled=false;
  var c=(Component)obj.AddComponent(creatureType);native[c]=marker;
  ((Behaviour)c).enabled=false; // Native Creature.Update/Start must never move or kill the MC body.
  Set(c,"m_NoumenonTransform",body.transform);Set(c,"m_bodyCollider",marker.Body);Set(c,"m_shadowTransform",shadow.transform);Set(c,"m_shadowCollider",marker.Shadow);
  Set(c,"MyCreateureType",playerKind);Set(c,"MyTeamType",Get(hero,"MyTeamType")!);Set(c,"CanFindByMonsterAI",true);
  body.AddComponent(bodyType);
  obj.SetActive(true);Set(c,"isHasPun",false);marker.Alive=true;
  Plugin.Log.LogInfo($"Minecraft native combat target registered: entity={id}, hero_layer={layer.name}");return c;
 }
 private void Remove(uint id){var c=targets[id];targets.Remove(id);if(c==null)return;if(native.TryGetValue(c,out var marker))marker.Alive=false;c.gameObject.SetActive(false);UnityEngine.Object.Destroy(c.gameObject);}
 private void Clear(){foreach(var id in targets.Keys.ToArray())Remove(id);}
 internal void Dispose(){transport=null;Clear();}
}
