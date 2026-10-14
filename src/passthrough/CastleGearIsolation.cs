using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace PeakCraft.World;

/// <summary>Mask only stat recalculation. Native inventory, lifetime and visuals retain their gear.</summary>
internal sealed class CastleGearIsolation {
 private Component? observed;
 private bool isolated,selected;
 [ThreadStatic] private static object? filteredWeapon;
 [ThreadStatic] private static bool weaponSelected;
 [ThreadStatic] private static object? filteredHero;
 internal sealed class Scope {
  internal object Hero=null!;
  internal FieldInfo? Armor,Weapon;
  internal object? SavedArmor,SavedWeapon,PreviousFilter,PreviousHero;
  internal bool PreviousSelected;
 }
 internal static void Install(Harmony harmony){
  var hero=AccessTools.TypeByName("Teran.HeroBase");
  harmony.Patch(AccessTools.DeclaredMethod(hero,"rectifyPropertiesInspector"),
   prefix:new HarmonyMethod(typeof(CastleGearIsolation),nameof(BeforeStats)),
   finalizer:new HarmonyMethod(typeof(CastleGearIsolation),nameof(AfterStats)));
  harmony.Patch(AccessTools.Method(hero,"ResetHardValueByArmor"),
   prefix:new HarmonyMethod(typeof(CastleGearIsolation),nameof(BeforeArmor)),
   finalizer:new HarmonyMethod(typeof(CastleGearIsolation),nameof(AfterStats)));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.Equipment"),"getValueForAttrType"),
   postfix:new HarmonyMethod(typeof(CastleGearIsolation),nameof(Attribute)));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.BagMgr"),"GetValueForAttrType"),
   postfix:new HarmonyMethod(typeof(CastleGearIsolation),nameof(BagAttribute)));
 }
 private static bool Isolated(object hero)=>Plugin.Instance!=null&&Plugin.Instance.IsolateCastleGear(hero);
 private static Scope? Mask(object hero,bool full){
  if(!Isolated(hero))return null;
  var s=new Scope{Hero=hero,Armor=AccessTools.Field(hero.GetType(),"m_Armor"),
   PreviousFilter=filteredWeapon,PreviousSelected=weaponSelected,PreviousHero=filteredHero};
  s.SavedArmor=s.Armor?.GetValue(hero);s.Armor?.SetValue(hero,null);
  if(full){
   filteredHero=hero;
   s.Weapon=AccessTools.Field(hero.GetType(),"m_Weapon");s.SavedWeapon=s.Weapon?.GetValue(hero);
   filteredWeapon=s.SavedWeapon;weaponSelected=Plugin.Instance.NativeGearSelected;
   if(!weaponSelected)s.Weapon?.SetValue(hero,null);
  }
  return s;
 }
 private static void BeforeStats(object __instance,out Scope? __state)=>__state=Mask(__instance,true);
 private static void BeforeArmor(object __instance,out Scope? __state)=>__state=Mask(__instance,false);
 private static void AfterStats(Scope? __state){
  if(__state==null)return;
  try{__state.Armor?.SetValue(__state.Hero,__state.SavedArmor);__state.Weapon?.SetValue(__state.Hero,__state.SavedWeapon);}
  finally{filteredWeapon=__state.PreviousFilter;weaponSelected=__state.PreviousSelected;filteredHero=__state.PreviousHero;}
 }
 private static void Attribute(object __instance,object __0,ref int __result){
  if(ReferenceEquals(__instance,filteredWeapon)&&!CastleGearRules.NativeWeaponAttribute(__0.ToString(),weaponSelected))__result=0;
 }
 private static void BagAttribute(int __0,ref int __result){
  if(filteredHero!=null&&Convert.ToInt32(AccessTools.Property(filteredHero.GetType(),"NetId").GetValue(filteredHero,null))==__0)__result=0;
 }
 internal void Update(Component? hero,bool active,bool nativeSelected){
  if(hero==observed&&active==isolated&&nativeSelected==selected)return;
  observed=hero;isolated=active;selected=nativeSelected;
  if(hero!=null)AccessTools.Method(hero.GetType(),"rectifyPropertiesInspector")?.Invoke(hero,null);
 }
}
