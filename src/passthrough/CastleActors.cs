using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using PeakCraft.Link;

namespace PeakCraft.World;

internal sealed class CastleActors {
 public static void InstallPatches(Harmony harmony) {
  // Plugin installs explicit patches rather than PatchAll, so register this one explicitly too.
  harmony.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Teran.Creature"),"MoveSpeed"),
   postfix:new HarmonyMethod(typeof(NativeFluidDrag),"Postfix"));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.Creature"),"BeAttackedByDebuff"),prefix:new HarmonyMethod(typeof(CastleActors),nameof(MirroredDebuffDamage)));
 }
 private static readonly Dictionary<(int actor,string effect),float> mirroredDamageStatuses=new();
 [ThreadStatic] private static bool forwardingPoison;
 private static bool MirroredDebuffDamage(object __instance,object __1,ref int __result){
  if(!forwardingPoison&&__instance is Component actor&&Plugin.Instance!=null&&Plugin.Instance.PreserveAvatar&&
   mirroredDamageStatuses.TryGetValue((actor.GetInstanceID(),__1.ToString()),out float until)&&Time.unscaledTime<until){__result=0;return false;}
  return true;
 }
 private const uint RectangularFootprint = 1u << 4;
 private const uint FireHit = 1u << 3;
 private const uint PoisonHit = 1u << 8, WitherHit = 1u << 9, FreezeHit = 1u << 10;
 private readonly Dictionary<uint,NativeActor> actors = new();
 private readonly Dictionary<Type,ActorApi> apis = new();
 private readonly Type monsterType=AccessTools.TypeByName("Teran.Monster");
 private readonly Type scareType=AccessTools.TypeByName("Teran.ScareCrow");
 private readonly Type breakType=AccessTools.TypeByName("Teran.BreakableEntity");
 private readonly Type npcType=AccessTools.TypeByName("Teran.Npc");
 private readonly Type itemType=AccessTools.TypeByName("Teran.SceneItem");
 private readonly Type groundItemType=AccessTools.TypeByName("Teran.Item");
 private readonly Type groundMotionType=AccessTools.TypeByName("Teran.ItemOnGround");
 private readonly Type creatureType=AccessTools.TypeByName("Teran.Creature");
 private readonly Type attackType=AccessTools.TypeByName("Teran.AttackInfo");
 private readonly Type damageType=AccessTools.TypeByName("Teran.DamageInfo");
 private readonly Type debuffType=AccessTools.TypeByName("Teran.Debuff+DebuffType");
 private readonly Dictionary<string,FieldInfo> attackFields = new();
 private readonly Dictionary<string,object> debuffs = new();
 private readonly MemoryStream stream = new(Proto.MaxActors * 64);
 private BinaryWriter? writer;
 private float nextScan, nextDiagnostic;
 private readonly CastleActorScanSchedule discovery=new();
 private readonly HashSet<Component> discovered=new();
 private Type[]? discoveryTypes;
 internal double ScanMs {get;private set;}
 internal double StateMs {get;private set;}
 internal double SerializeMs {get;private set;}
 private readonly Dictionary<Type,(PropertyInfo? hp,PropertyInfo? maximum)> healthApis=new();
 private static double Elapsed(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
 private int hits, fireHits;
 private Component[] candidates=Array.Empty<Component>();
 private static readonly Dictionary<int,FluidDrag> fluidDrag = new();
 private readonly struct FluidDrag {
  public readonly float Factor, Until;
  public FluidDrag(float factor,float until) { Factor=factor; Until=until; }
 }
 private sealed class ActorApi {
  internal readonly PropertyInfo? Alive, Body, Shadow, Kind;
  internal readonly FieldInfo? Broken, Inspector, PickedUp;
  internal readonly PropertyInfo? PickedUpProperty;
  internal readonly MethodInfo? Attack, DebuffAllowed, GiveDebuff, CanInteraction, DebuffDamage;
  internal ActorApi(Type type,Type attackType) {
   Alive=AccessTools.Property(type,"IsAlive"); Body=AccessTools.Property(type,"BodyCollider");
   Shadow=AccessTools.Property(type,"ShadowCollider"); Kind=AccessTools.Property(type,"MyCreateureType");
   PickedUp=AccessTools.Field(type,"isPickup");PickedUpProperty=AccessTools.Property(type,"isPickup");CanInteraction=AccessTools.Method(type,"IsCanInteraction",Type.EmptyTypes);
   Broken=AccessTools.Field(type,"m_isBreak"); Inspector=AccessTools.Field(type,"propertiesInspector");
   Attack=AccessTools.Method(type,"BeAttacked",new[]{attackType,typeof(float),typeof(bool)});
   DebuffDamage=AccessTools.Method(type,"BeAttackedByDebuff");
   DebuffAllowed=AccessTools.Method(type,"IsDebuffTakeEffect"); GiveDebuff=AccessTools.Method(type,"GetDebuff");
  }
 }
 private sealed class NativeActor {
  internal readonly Component Component;
  internal readonly ActorApi Api;
  internal readonly byte[] Name = new byte[24];
  internal readonly uint EnchantmentFlags;
  internal readonly Collider2D? Body, Shadow;
  internal NativeActor(Component component,ActorApi api) {
   Component=component; Api=api;
   EnchantmentFlags=CastleEnchantmentTargets.Classify(component.GetComponentsInChildren<Component>(true).Where(c=>c!=null).Select(c=>c.GetType().FullName??""));
   Body=api.Body?.GetValue(component,null) as Collider2D;
   if(Body==null) Body=component.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.gameObject.name=="noumenon");
   Shadow=api.Shadow?.GetValue(component,null) as Collider2D;
   var utf=Encoding.UTF8.GetBytes(component.name); Array.Copy(utf,Name,Math.Min(23,utf.Length));
  }
  internal bool Alive => Component!=null && !(bool)(Api.PickedUp?.GetValue(Component)??Api.PickedUpProperty?.GetValue(Component,null)??false) && (Api.Alive!=null ? (bool)Api.Alive.GetValue(Component,null)
   : Api.Broken==null || !(bool)Api.Broken.GetValue(Component));
  internal Bounds BodyBounds => Body!=null && Body.enabled ? Body.bounds
   : new Bounds(Component.transform.position+Vector3.up*.5f,new Vector3(1,1,.4f));
  internal bool Hostile => Api.Kind?.GetValue(Component,null)?.ToString()=="Enemy";
 }

 public void Update(PeakLink link,bool active) {
  ScanMs=StateMs=SerializeMs=0;
  if(active){
   long scanStart=System.Diagnostics.Stopwatch.GetTimestamp();
   RefreshCandidates();ScanMs=Elapsed(scanStart);
  }
  if(active && Time.unscaledTime<nextScan)return;
  nextScan=Time.unscaledTime+.05f;
  if(!active) {
   actors.Clear(); candidates=Array.Empty<Component>(); discovered.Clear();discovery.Reset();fluidDrag.Clear();mirroredDamageStatuses.Clear();
   link.WriteActors(Array.Empty<byte>(),0); return;
  }
  long stateStart=System.Diagnostics.Stopwatch.GetTimestamp();
  var seen=new HashSet<uint>();
  foreach(var component in candidates) {
   if(component==null || !component.gameObject.activeInHierarchy)continue;
   if(breakType.IsInstanceOfType(component) && component.GetComponent(creatureType)!=null)continue;
   CastleHunterMirror.ObserveActor(component);
   uint id=unchecked((uint)component.GetInstanceID());
   if(!actors.TryGetValue(id,out var actor)) {
    Type type=component.GetType();
    if(!apis.TryGetValue(type,out var api))apis[type]=api=new ActorApi(type,attackType);
    actor=new NativeActor(component,api); actors[id]=actor;
    if(CastleHunterMirror.IsHunter(component))Plugin.Log.LogInfo($"Final hunter mirror identified: actor={id}, AI={string.Join(",",component.GetComponents<Component>().Where(c=>c!=null&&CastleHunterIdentity.Matches(c.GetType().FullName)).Select(c=>c.GetType().FullName))}");
   }
   if(CastleHunterState.Present(actor.Alive,CastleHunterMirror.IsReplacement(component)))seen.Add(id);
  }
  foreach(var id in actors.Keys.Where(id=>!seen.Contains(id)).ToArray()) {actors.Remove(id);fluidDrag.Remove(unchecked((int)id));}
  foreach(var key in mirroredDamageStatuses.Keys.Where(k=>!seen.Contains(unchecked((uint)k.actor))||Time.unscaledTime>=mirroredDamageStatuses[k]).ToArray())mirroredDamageStatuses.Remove(key);
  StateMs=Elapsed(stateStart);
  long serializeStart=System.Diagnostics.Stopwatch.GetTimestamp();
  writer ??= new BinaryWriter(stream);
  stream.Position=0;stream.SetLength(0);
  foreach(var pair in actors) {
   NativeActor actor=pair.Value;
   Geometry(actor,out float x,out float y,out float z,out float width,out float height,out float depth);
   writer.Write(pair.Key);
   var inspector=actor.Api.Inspector?.GetValue(actor.Component);
   var healthApi=HealthApi(inspector);
   int nativeMaximum=inspector==null||healthApi.maximum==null?0:Convert.ToInt32(healthApi.maximum.GetValue(inspector,null));
   bool interactive=(npcType!=null&&npcType.IsInstanceOfType(actor.Component))||(itemType!=null&&itemType.IsInstanceOfType(actor.Component))||(groundItemType!=null&&groundItemType.IsInstanceOfType(actor.Component));
   bool creature=IsCombatCreature(actor.Component);
   writer.Write(actor.EnchantmentFlags | CastleHunterMirror.Flags(actor.Component) | (nativeMaximum>0&&nativeMaximum<=ushort.MaxValue?512u:0u) | RectangularFootprint | (creature?64u:0u) | (creature && actor.Alive && actor.Hostile ? 1u : 0u) | (interactive?32u:0u));
   writer.Write(x);writer.Write(y);writer.Write(z);
   // Flag bit4 gives the formerly unused yaw word a rectangular floor-depth meaning.
   // Older guests simply keep their square hitbox; actor record size/protocol stays v11.
   writer.Write(depth);writer.Write(width);writer.Write(height);
   float healthFraction=1f;
   if(inspector!=null&&healthApi.hp!=null&&healthApi.maximum!=null)healthFraction=Mathf.Clamp01(Convert.ToSingle(healthApi.hp.GetValue(inspector,null))/Math.Max(1,nativeMaximum));
   writer.Write(healthFraction);
   // Native-health flag gives the unused level word its actual max-HP meaning.
   writer.Write((ushort)(nativeMaximum>0&&nativeMaximum<=ushort.MaxValue?nativeMaximum:1));writer.Write(CastleHunterMirror.State(actor.Component));writer.Write(actor.Name);
  }
  link.WriteActors(stream.ToArray(),actors.Count);
  SerializeMs=Elapsed(serializeStart);
  if(Time.unscaledTime>=nextDiagnostic) {
   nextDiagnostic=Time.unscaledTime+10;
   Plugin.Log.LogInfo($"CastleCraft combat native: actors={actors.Count}, hits={hits}, fire/lava={fireHits}, fluidContacts={fluidDrag.Count}");
   hits=fireHits=0;
  }
 }

 private (PropertyInfo? hp,PropertyInfo? maximum) HealthApi(object? inspector){
  if(inspector==null)return default;
  var type=inspector.GetType();
  if(!healthApis.TryGetValue(type,out var api))healthApis[type]=api=(AccessTools.Property(type,"CurrentHp"),AccessTools.Property(type,"maxHp"));
  return api;
 }
 private void RefreshCandidates(){
  discoveryTypes??=new[]{monsterType,scareType,breakType,npcType,itemType,groundMotionType,fallenHunterType}.Where(t=>t!=null).ToArray();
  int index=discovery.Take(Time.unscaledTime,Time.frameCount,discoveryTypes.Length);
  if(index<0)return;
  if(index==0)discovered.Clear();
  foreach(var obj in Resources.FindObjectsOfTypeAll(discoveryTypes[index])){
   var c=obj as Component;
   if(c!=null&&groundMotionType!=null&&groundMotionType.IsInstanceOfType(c))c=c.GetComponent(groundItemType);
   if(c!=null&&c.gameObject.scene.IsValid()&&c.gameObject.scene.isLoaded&&c.gameObject.activeInHierarchy&&discovered.Count<Proto.MaxActors)discovered.Add(c);
  }
  // Keep the previous complete sweep until all types are present; never erase other types mid-sweep.
  if(index==discoveryTypes.Length-1)candidates=discovered.ToArray();
 }


 internal void AcknowledgeHunter(uint id){if(actors.TryGetValue(id,out var actor))CastleHunterMirror.Acknowledge(actor.Component);}
 internal void HealHunter(uint id,float amount){
  if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||!actors.TryGetValue(id,out var actor)||!actor.Alive||!CastleHunterMirror.IsReplacement(actor.Component))return;
  object inspector=actor.Api.Inspector?.GetValue(actor.Component);if(inspector==null)return;
  var hp=AccessTools.Property(inspector.GetType(),"CurrentHp");var maximum=AccessTools.Property(inspector.GetType(),"maxHp");
  if(hp?.CanWrite==true&&maximum!=null){long total=Convert.ToInt32(hp.GetValue(inspector,null))+(long)Math.Ceiling(Math.Min(amount,int.MaxValue));hp.SetValue(inspector,(int)Math.Min(Convert.ToInt32(maximum.GetValue(inspector,null)),total),null);}
 }

 private void Geometry(NativeActor actor,out float x,out float y,out float z,out float width,out float height,out float depth) {
  Bounds body=actor.BodyBounds;
  if(groundItemType!=null&&groundItemType.IsInstanceOfType(actor.Component)) {
   var sprite=actor.Component.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.enabled&&r.sprite!=null&&r.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0);
   if(sprite!=null)body=sprite.bounds;
  }
  Bounds? shadow=actor.Shadow!=null && actor.Shadow.enabled ? actor.Shadow.bounds : null;
  float floorY=CastleGround.Position(actor.Component).y;
  // Body X may be offset substantially from a boss's root. Height remains height;
  // castle floor Y becomes Minecraft Z and must come from the shadow, not body center.
  x=body.center.x/Coords.Scale+(float)Coords.RoomX;
  y=(float)Coords.YOffset+Math.Max(0,(body.min.y-floorY)/Coords.Scale);
  z=-floorY/Coords.DepthScale;
  if(CastleHunterMirror.IsReplacement(actor.Component)) {
   var feet=CastleGround.Position(actor.Component);
   x=feet.x/Coords.Scale+(float)Coords.RoomX;z=-feet.y/Coords.DepthScale;
   // Native noumenon baseline is not the collider bottom. Preserve actual jump lift.
   var noumenon=AccessTools.Field(actor.Component.GetType(),"m_NoumenonTransform")?.GetValue(actor.Component) as Transform;
   float baseline=Convert.ToSingle(AccessTools.Field(actor.Component.GetType(),"bodyHeight")?.GetValue(actor.Component)??0f);
   float lift=noumenon==null||noumenon.parent==null?0:noumenon.parent.TransformVector(Vector3.up*(noumenon.localPosition.y-baseline)).y;
   y=(float)Coords.YOffset+Math.Max(0,lift)/Coords.Scale;
  }
  width=Mathf.Clamp(body.size.x/Coords.Scale,.25f,64f);
  height=Mathf.Clamp(body.size.y/Coords.Scale,.25f,64f);
  depth=Mathf.Clamp(shadow.HasValue ? shadow.Value.size.y/Coords.DepthScale : Math.Min(width,.9f),.25f,64f);
 }

 public bool Target(Vector2 point,out Vector3 mc) {
  NativeActor? closest=null;float smallest=float.MaxValue;
  foreach(var actor in actors.Values) {
   if(!actor.Alive)continue;
   Bounds bounds=actor.BodyBounds;
   if(point.x<bounds.min.x || point.x>bounds.max.x || point.y<bounds.min.y || point.y>bounds.max.y)continue;
   float area=bounds.size.x*bounds.size.y;
   if(area<smallest){smallest=area;closest=actor;}
  }
  if(closest!=null) {
   Geometry(closest,out float x,out float y,out float z,out _,out float height,out _);
   mc=new Vector3(x,y+height*.5f,z);return true;
  }
  mc=default;return false;
 }

 public void Hit(Proto.GuestEvent e,Component? hero,CastleCollision collision,uint epoch) {
  if(!actors.TryGetValue(e.formId,out var actor) || !actor.Alive)return;
  Component target=actor.Component;
  if(e.type==9) {
   if(e.flags!=epoch||!IsCombatCreature(target)||!CastleExternalMotion.Valid(e.a,e.b)||!collision.Ready)return;
   Geometry(actor,out float x,out _,out float z,out _,out _,out _);
   if(!CastleExternalMotion.ValidOrigin(e.c,e.d,x,z))return;
   var from=CastleGround.Position(target);
   var to=from+new Vector3(e.a*Coords.Scale,-e.b*Coords.DepthScale,0);
   var size=actor.Shadow!=null?(Vector2)actor.Shadow.bounds.size:new Vector2(.5f,.25f);
   Physics2D.SyncTransforms();var safe=collision.Constrain(from,to,size,0);
   if(collision.Contains(safe))CastleGround.Place(target,safe);
   return;
  }
  if(e.type==7) {
   if(e.a>0 || e.b>0)fluidDrag[target.GetInstanceID()]=new FluidDrag(Mathf.Clamp(e.c,.1f,1f),Time.unscaledTime+.8f);
   else fluidDrag.Remove(target.GetInstanceID());
   return;
  }
  if(e.type==6 && creatureType.IsInstanceOfType(target)) {
   var kind=Enum.ToObject(debuffType,(int)e.a);
   ApplyDebuff(actor,kind,Mathf.Clamp(e.b/20f,.1f,30f),hero);return;
  }
  if(e.type!=Proto.EvHitActor || e.a<=0 || float.IsNaN(e.a) || float.IsInfinity(e.a))return;
  if(breakType.IsInstanceOfType(target)){if(hero!=null)BreakHit(e,target,hero);return;}
  if(actor.Api.Attack==null)return;
  // Poison retains its non-lethal native damage path; fire and wither still may kill.
  if((e.flags&PoisonHit)!=0&&(e.flags&WitherHit)==0){
   if(actor.Api.DebuffDamage==null)return;
   bool previous=forwardingPoison;forwardingPoison=true;
   try{actor.Api.DebuffDamage.Invoke(target,new object?[]{Math.Max(1,(int)Math.Min(1000000,Math.Round(e.a*5))),Debuff("Poisoned"),hero?.transform});}
   finally{forwardingPoison=previous;}
   ++hits;if(actor.Alive)ApplyDebuff(actor,Debuff("Poisoned"),1.2f,hero);return;
  }
  bool fire=(e.flags&FireHit)!=0,damageOverTime=(e.flags&(FireHit|WitherHit|FreezeHit))!=0;
  object info=Activator.CreateInstance(attackType);
  void Set(string name,object? value) {
   if(!attackFields.TryGetValue(name,out var field))attackFields[name]=field=AccessTools.Field(attackType,name);
   field.SetValue(info,value);
  }
  Set("origin_damage",Math.Max(1,(int)Math.Min(1000000,Math.Round(e.a*5))));
  Set("hard_value",damageOverTime?0:Math.Max(1,(int)Math.Round(e.d*5)));
  Set("isCritial",(e.flags&1)!=0);Set("attacker",hero?.transform);
  Set("action_name",fire?"MinecraftFire":"MinecraftAttack");Set("attackerName",hero?.name ?? "MinecraftEnvironment");
  Set("prop_name",fire?"MinecraftFire":"MinecraftEquipment");Set("isFlyingObj",(e.flags&2)!=0);
  // BeAttackedByDebuff is intentionally non-lethal in Lost Castle (it clamps HP to 1),
  // and Burned immunity is status immunity, not immunity to every fire-source hit.
  // The native attack pipeline preserves boss protection, defence, death, talents and drops.
  float direction=e.b!=0?Math.Sign(e.b):hero!=null?Math.Sign(target.transform.position.x-hero.transform.position.x):1;
  object result=actor.Api.Attack.Invoke(target,new object[]{info,fire?0f:direction*.1f,true});
  if(!damageOverTime && Convert.ToInt32(result)>0) {
   // Native BodyCollideDetector invokes hard-value handling separately from BeAttacked.
   var hard=AccessTools.Method(target.GetType(),"HardValueHandle",new[]{typeof(int),typeof(float),typeof(Vector3),typeof(float),typeof(int),typeof(float)});
   var push=new Vector3(e.b*Coords.Scale,-e.c*Coords.DepthScale,0);
   if(push.sqrMagnitude<.00001f)push=Vector3.right*direction;
   int strength=Math.Max(1,(int)Math.Round(e.d*5));
   hard?.Invoke(target,new object[]{strength,Mathf.Clamp(e.d,.1f,2),push.normalized,0f,Convert.ToInt32(result),.8f});
   target.SendMessage("CreatureBeAttackedHardValue",strength,SendMessageOptions.DontRequireReceiver);
  }
  ++hits;if(fire)++fireHits;
  Plugin.Log.LogDebug($"Minecraft native hit {target.name}: damage={e.a}, flags={e.flags}, applied={result}");
  if(actor.Alive) {
   if(fire)ApplyDebuff(actor,Debuff("Burned"),1.2f,hero);
   if((e.flags&(PoisonHit|WitherHit))!=0)ApplyDebuff(actor,Debuff("Poisoned"),1.2f,hero);
   if((e.flags&FreezeHit)!=0)ApplyDebuff(actor,Debuff("Freezing"),1.2f,hero);
  }
 }
 public void FocusInteraction(uint id,Component? hero){
  if(hero==null||!actors.TryGetValue(id,out var actor))return;
  // Native PickUp prioritizes dropped items, then scene items, then NPCs.
  // Clear other focuses so the mouse target is the one native input operates on.
  foreach(var focus in new[]{"FocusItemOnGround","FocusSceneItem","FocusNpc"})AccessTools.Property(hero.GetType(),focus)?.SetValue(hero,null,null);
  string property=npcType!=null&&npcType.IsInstanceOfType(actor.Component)?"FocusNpc":groundItemType!=null&&groundItemType.IsInstanceOfType(actor.Component)?"FocusItemOnGround":"FocusSceneItem";
  AccessTools.Property(hero.GetType(),property)?.SetValue(hero,actor.Component,null);
 }
 public bool CanInteract(uint id,Component? hero){
  if(hero==null||!actors.TryGetValue(id,out var actor)||!actor.Alive)return false;
  if(!(npcType!=null&&npcType.IsInstanceOfType(actor.Component))&&!(itemType!=null&&itemType.IsInstanceOfType(actor.Component))&&!(groundItemType!=null&&groundItemType.IsInstanceOfType(actor.Component)))return false;
  if(actor.Api.CanInteraction!=null&&!(bool)actor.Api.CanInteraction.Invoke(actor.Component,null))return false;
  var delta=actor.Component.transform.position-hero.transform.position;
  return delta.x*delta.x+delta.y*delta.y*4<6.25f;
 }
 private object Debuff(string name) {
  if(!debuffs.TryGetValue(name,out var value))debuffs[name]=value=Enum.Parse(debuffType,name);
  return value;
 }
 private static void ApplyDebuff(NativeActor actor,object kind,float duration,Component? hero) {
  if(actor.Api.DebuffAllowed==null || actor.Api.GiveDebuff==null)return;
  if(!(bool)actor.Api.DebuffAllowed.Invoke(actor.Component,new[]{kind}))return;
  if(kind.ToString()=="Burned"||kind.ToString()=="Poisoned")mirroredDamageStatuses[(actor.Component.GetInstanceID(),kind.ToString())]=Time.unscaledTime+duration+.1f;
  actor.Api.GiveDebuff.Invoke(actor.Component,new object?[]{kind,0,duration,1f,hero?.transform});
 }
 private void BreakHit(Proto.GuestEvent e,Component target,Component hero) {
  var go=new GameObject("CastleCraft Minecraft hit");go.hideFlags=HideFlags.HideAndDontSave;
  try {
   var info=go.AddComponent(damageType);
   AccessTools.Field(damageType,"damage").SetValue(info,Math.Max(1,(int)Math.Round(e.a*5)));
   AccessTools.Field(damageType,"hard_value").SetValue(info,1);
   AccessTools.Field(damageType,"creature").SetValue(info,hero.transform);
   AccessTools.Field(damageType,"action_name").SetValue(info,"MinecraftAttack");
   AccessTools.Field(damageType,"prop_name").SetValue(info,"MinecraftEquipment");
   AccessTools.Method(breakType,"BeAttacked",new[]{damageType,typeof(Transform),typeof(Vector3)}).Invoke(target,new object[]{info,hero.transform,target.transform.position});
   ++hits;
  }finally{UnityEngine.Object.Destroy(go);}
 }

 [HarmonyPatch]
 private static class NativeFluidDrag {
  private static MethodBase TargetMethod()=>AccessTools.PropertyGetter(AccessTools.TypeByName("Teran.Creature"),"MoveSpeed");
  private static void Postfix(Component __instance,ref float __result) {
   int id=__instance.GetInstanceID();
   if(!fluidDrag.TryGetValue(id,out var drag))return;
   if(Time.unscaledTime>drag.Until){fluidDrag.Remove(id);return;}
   // Multiply the current native speed, preserving buffs and debuffs; never overwrite base stats.
   __result*=drag.Factor;
  }
 }
 // Fallen hunters inherit HeroBase, not Monster; never include the sibling Hero player type.
 private readonly Type fallenHunterType=AccessTools.TypeByName("Teran.FallenHunter");
 private bool IsCombatCreature(Component component)=>monsterType.IsInstanceOfType(component)||(fallenHunterType!=null&&fallenHunterType.IsInstanceOfType(component));
}

