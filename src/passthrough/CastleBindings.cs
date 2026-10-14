using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;

namespace PeakCraft.World;

/// <summary>Routes one physical input to one game action. Mouse combat is an explicit mode.</summary>
internal sealed class CastleBindings {
 private static readonly string[] ActionNames={"PickUp","UseItem","DiscardItem","NextItem","PreItem","Skill","TAB","Dialog_show","Attack","SP_ATK"};
 private readonly HashSet<KeyCode> reserved=new();
 private readonly HashSet<KeyCode> hotbarKeys=new();
 private readonly Dictionary<string,ConfigEntry<string>> custom=new();
 private readonly List<Binding> actions=new();
 private readonly HashSet<string> held=new();
 private readonly HashSet<string> activeActions=new();
 private readonly HashSet<KeyCode> blockedMouse=new();
 private readonly List<string> releaseBuffer=new();
 private ConfigEntry<string> mouseMode=null!,toggleSetting=null!,attackMouse=null!,specialMouse=null!,skillMouse=null!;
 private KeyCode toggleKey=KeyCode.F6,attackKey=KeyCode.Mouse0,specialKey=KeyCode.Mouse1,skillKey=KeyCode.Mouse2;
 private bool castleMouse=false;
 private ConfigEntry<bool> equipmentEnabled=null!;
 public bool CastleEquipmentEnabled=>equipmentEnabled.Value;
 private KeyCode forward=KeyCode.W,back=KeyCode.S,left=KeyCode.A,right=KeyCode.D,jump=KeyCode.Space;
 public bool StruggleLeft=>Pressed(left)>0;
 public bool StruggleRight=>Pressed(right)>0;
 public bool MovementKey(KeyCode key)=>key==forward||key==back||key==left||key==right||key==jump;
 public Vector2 Movement=>new Vector2(Pressed(right)-Pressed(left),Pressed(back)-Pressed(forward));
 private static float Pressed(KeyCode key)=>key!=KeyCode.None&&Input.GetKey(key)?1f:0f;
 private string actionHint="";
 private sealed class Binding {
  public string Action="";
  public KeyCode Key;
  public KeyCode[] Modifiers=Array.Empty<KeyCode>();
  public bool Matches=>Modifiers.All(Input.GetKey);
  public string Label=>string.Join("+",Modifiers.Select(k=>k.ToString()).Concat(new[]{Key.ToString()}));
 }
 public bool CastleMouse=>castleMouse;
 public string ToggleLabel=>toggleKey.ToString();
 public bool AimingAtBlock{get;set;}
 public bool ModeChanged{get;private set;}
 public string Hint=>$"Ctrl+{toggleKey} 城堡装备：{(CastleEquipmentEnabled?"启用":"已脱离")} · {toggleKey} 虚拟武器槽：{(castleMouse?"失落城堡":"Minecraft")} · 右键保留副手 · LeftAlt+右键 城堡蓄力 · "+actionHint;
 public void Initialize(ConfigFile config){
  foreach(string action in ActionNames)custom[action]=config.Bind("Controls",action,action=="Attack"?"LeftAlt+Mouse0":action=="SP_ATK"?"LeftAlt+Mouse1":"Auto","Auto avoids Minecraft bindings; or use Unity key names, e.g. Return, LeftAlt+E. Keyboard controls also work in either mouse mode.");
  mouseMode=config.Bind("Controls","MouseMode","Minecraft","Castle: mouse buttons operate the equipped Lost Castle weapon. Minecraft: ordinary Minecraft mining/attack/use. Switch with ToggleMouseMode; saved independently of room changes.");
  toggleSetting=config.Bind("Controls","ToggleMouseMode","F6","Switch the active main weapon only; Minecraft block and offhand input stays available. F5/F8 remain camera/character keys.");
  attackMouse=config.Bind("Controls","CastleMouseAttack","Mouse0","Castle mouse mode: normal attack (held uses the native combo repeat rules).");
  specialMouse=config.Bind("Controls","CastleMouseSpecial","Mouse1","Castle mouse mode: special attack; press, hold and release are forwarded to the native weapon.");
  skillMouse=config.Bind("Controls","CastleMouseSkill","Mouse2","Castle mouse mode: equipped weapon skill, using its native cooldown and talent rules.");
  equipmentEnabled=config.Bind("Equipment","CastleEquipmentEnabled",false,"Independent castle equipment permission. Ctrl+ToggleMouseMode detaches/restores it. Minecraft gear is never changed. Castle armor and passive bag statistics are isolated; castle HP/defence/speed never stack with Minecraft. Selecting the castle weapon explicitly enables it.");
  castleMouse=equipmentEnabled.Value&&mouseMode.Value.Equals("Castle",StringComparison.OrdinalIgnoreCase);
  attackKey=MouseKey(attackMouse.Value,KeyCode.Mouse0);specialKey=MouseKey(specialMouse.Value,KeyCode.Mouse1);skillKey=MouseKey(skillMouse.Value,KeyCode.Mouse2);
  if(attackKey==specialKey||attackKey==skillKey||specialKey==skillKey){attackKey=KeyCode.Mouse0;specialKey=KeyCode.Mouse1;skillKey=KeyCode.Mouse2;Plugin.Log.LogWarning("Castle mouse buttons overlap; using Mouse0/Mouse1/Mouse2.");}
 }
 private static KeyCode MouseKey(string value,KeyCode fallback){var key=Parse(value);return key>=KeyCode.Mouse0&&key<=KeyCode.Mouse6?key:fallback;}
 private static KeyCode Parse(string value){
  value=value.Trim();
  if(value.StartsWith("key.mouse.",StringComparison.OrdinalIgnoreCase))return value.Substring(10).ToLowerInvariant() switch{"left"=>KeyCode.Mouse0,"right"=>KeyCode.Mouse1,"middle"=>KeyCode.Mouse2,"4"=>KeyCode.Mouse3,"5"=>KeyCode.Mouse4,"6"=>KeyCode.Mouse5,"7"=>KeyCode.Mouse6,_=>KeyCode.None};
  value=value.Replace("key.keyboard.","");
  value=value.ToLowerInvariant() switch{"left.shift"=>"LeftShift","right.shift"=>"RightShift","left.control"=>"LeftControl","right.control"=>"RightControl","left.alt"=>"LeftAlt","right.alt"=>"RightAlt","enter"=>"Return","backspace"=>"Backspace","page.up"=>"PageUp","page.down"=>"PageDown","left.bracket"=>"LeftBracket","right.bracket"=>"RightBracket","grave.accent"=>"BackQuote","equal"=>"Equals","apostrophe"=>"Quote",_=>value};
  if(value.Length==1&&char.IsDigit(value[0]))value="Alpha"+value;
  return Enum.TryParse(value,true,out KeyCode key)?key:KeyCode.None;
 }
 public void Read(string text){
  reserved.Clear();hotbarKeys.Clear();reserved.Add(KeyCode.F5);reserved.Add(KeyCode.F8);
  foreach(var line in text.Split('\n')){
   int sep=line.IndexOf('=');if(sep<0)continue;var key=Parse(line.Substring(sep+1));if(key!=KeyCode.None)reserved.Add(key);
   if(line.Substring(0,sep).Trim().StartsWith("key.hotbar.",StringComparison.Ordinal)&&key!=KeyCode.None)hotbarKeys.Add(key);
   switch(line.Substring(0,sep).Trim()){case "key.forward":forward=key;break;case "key.back":back=key;break;case "key.left":left=key;break;case "key.right":right=key;break;case "key.jump":jump=key;break;}
  }
 }
 public void Configure(object dd){
  actions.Clear();
  var requestedToggle=Parse(toggleSetting.Value);
  var candidates=new[]{requestedToggle,KeyCode.F6,KeyCode.F7,KeyCode.F9,KeyCode.F10,KeyCode.F11};
  toggleKey=candidates.FirstOrDefault(k=>k!=KeyCode.None&&k!=KeyCode.F5&&k!=KeyCode.F8&&!reserved.Contains(k));
  if(toggleKey==KeyCode.None){toggleKey=KeyCode.F6;Plugin.Log.LogWarning("All mouse-mode keys are bound in Minecraft. CastleCraft reserves F6 while character control is active; configure ToggleMouseMode to change this.");}
  var mapping=AccessTools.Field(dd.GetType(),"inputMapping")?.GetValue(dd);
  var raw= mapping==null?null:AccessTools.Field(mapping.GetType(),"key_mapping")?.GetValue(mapping) as IEnumerable;
  var bindings=raw?.Cast<object>().ToArray()??Array.Empty<object>();
  var enumType=AccessTools.TypeByName("Teran.InputController+KeyType");var inKey=AccessTools.TypeByName("InControl.Key");
  var spare=new[]{KeyCode.R,KeyCode.G,KeyCode.Z,KeyCode.X,KeyCode.V,KeyCode.B,KeyCode.N,KeyCode.M,KeyCode.O,KeyCode.P,KeyCode.H,KeyCode.K,KeyCode.L,KeyCode.J,KeyCode.U,KeyCode.Y,KeyCode.Return};
  foreach(string action in ActionNames){
   var configured=custom[action].Value;
   if(!configured.Equals("Auto",StringComparison.OrdinalIgnoreCase)){
    var combo=configured.Split('+').Select(Parse).ToArray();var primary=combo.LastOrDefault();
    bool valid=combo.Length>0&&!combo.Any(k=>k==KeyCode.None)&&primary!=KeyCode.F5&&primary!=KeyCode.F8&&primary!=toggleKey;
    if(valid&&(combo.Length>1||!reserved.Contains(primary))&&!actions.Any(b=>b.Key==primary&&b.Modifiers.SequenceEqual(combo.Take(combo.Length-1)))){actions.Add(new Binding{Action=action,Key=primary,Modifiers=combo.Take(combo.Length-1).ToArray()});continue;}
    Plugin.Log.LogWarning("Invalid/conflicting castle control; using automatic binding: "+action);
   }
   var key=KeyCode.None;int index=Convert.ToInt32(Enum.Parse(enumType,action))+31;
   if(index<bindings.Length&&AccessTools.Field(bindings[index].GetType(),"bindings")?.GetValue(bindings[index]) is IEnumerable nativeKeys){foreach(var native in nativeKeys){var candidate=Parse(Enum.GetName(inKey,Convert.ToInt32(native))??"");if(Free(candidate)){key=candidate;break;}}}
   if(key==KeyCode.None)key=spare.FirstOrDefault(Free);
   if(key!=KeyCode.None)actions.Add(new Binding{Action=action,Key=key});
   else {var fallback=spare.FirstOrDefault(k=>!actions.Any(b=>b.Key==k));if(fallback!=KeyCode.None)actions.Add(new Binding{Action=action,Key=fallback,Modifiers=new[]{KeyCode.LeftAlt}});}
  }
  actionHint=string.Join(" · ",actions.Select(b=>$"{b.Label} {Label(b.Action)}"));Plugin.Log.LogInfo("Resolved castle controls: "+Hint);
 }
 private bool Free(KeyCode key)=>key!=KeyCode.None&&key!=toggleKey&&key<KeyCode.Mouse0&&!reserved.Contains(key)&&!actions.Any(b=>b.Key==key);
 private string? MouseAction(KeyCode key)=>key==attackKey?"Attack":key==specialKey?"SP_ATK":key==skillKey?"Skill":null;
 public void Pump(Action<string> invoke,Action<string> release,bool gameplay=true){
  ModeChanged=false;
  if(!gameplay){ReleaseAll(release);return;}
  if(Input.GetKeyDown(toggleKey)){
   ReleaseAll(release);
   if(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)){equipmentEnabled.Value=!equipmentEnabled.Value;if(!equipmentEnabled.Value)castleMouse=false;}
   else{castleMouse=!castleMouse;if(castleMouse)equipmentEnabled.Value=true;}
   mouseMode.Value=castleMouse?"Castle":"Minecraft";ModeChanged=true;
   for(int i=0;i<=6;i++){var key=(KeyCode)((int)KeyCode.Mouse0+i);if(Input.GetKey(key))blockedMouse.Add(key);}
  }
  // Selecting the native Minecraft hotbar is also selecting its main weapon.
  // Do this before dispatching attacks so a simultaneous click cannot hit with Castle.
  if(castleMouse&&(Input.mouseScrollDelta.y!=0||hotbarKeys.Any(k=>Input.GetKeyDown(k)))){
   ReleaseAll(release);castleMouse=false;mouseMode.Value="Minecraft";ModeChanged=true;
   Plugin.Log.LogInfo("Minecraft hotbar selection restored Minecraft main-weapon input.");
  }
  blockedMouse.RemoveWhere(k=>!Input.GetKey(k));activeActions.Clear();
  foreach(var binding in actions){
   if(blockedMouse.Contains(binding.Key)||!binding.Matches||(!CastleEquipmentEnabled&&EquipmentAction(binding.Action)))continue;
   Dispatch(binding.Action,Input.GetKey(binding.Key),Input.GetKeyDown(binding.Key),invoke);
  }
  if(castleMouse&&!AimingAtBlock&&!Input.GetKey(KeyCode.LeftAlt))DispatchMouse(attackKey,invoke);
  if(CastleEquipmentEnabled&&Input.GetKey(KeyCode.LeftAlt)){DispatchMouse(attackKey,invoke);DispatchMouse(specialKey,invoke);DispatchMouse(skillKey,invoke);}
  releaseBuffer.Clear();foreach(var action in held)if(!activeActions.Contains(action))releaseBuffer.Add(action);
  foreach(var action in releaseBuffer){held.Remove(action);release(action);}
 }
 private void DispatchMouse(KeyCode key,Action<string> invoke){if(!blockedMouse.Contains(key))Dispatch(MouseAction(key)!,Input.GetKey(key),Input.GetKeyDown(key),invoke);}
 private void Dispatch(string action,bool pressed,bool down,Action<string> invoke){
  if(action=="Attack"||action=="SP_ATK"){
   if(pressed&&activeActions.Add(action)){held.Add(action);invoke(action);}
  }else if(down)invoke(action);
 }
 public void ReleaseAll(Action<string> release){foreach(var action in held)release(action);held.Clear();activeActions.Clear();}
 public bool Consumes(KeyCode key)=>key==toggleKey||blockedMouse.Contains(key)||(CastleEquipmentEnabled&&Input.GetKey(KeyCode.LeftAlt)&&MouseAction(key)!=null)||(castleMouse&&!AimingAtBlock&&key==attackKey)||actions.Any(b=>b.Key==key&&b.Matches&&(CastleEquipmentEnabled||!EquipmentAction(b.Action)));
 private static bool EquipmentAction(string action)=>action=="Attack"||action=="SP_ATK"||action=="Skill";
 private static string Label(string action)=>action switch{"PickUp"=>"拾取/交互","UseItem"=>"城堡道具","DiscardItem"=>"丢弃","NextItem"=>"下个道具","PreItem"=>"上个道具","Skill"=>"城堡技能","TAB"=>"城堡属性","Dialog_show"=>"对话","Attack"=>"城堡武器攻击","SP_ATK"=>"城堡武器特殊/蓄力",_=>action};
}
