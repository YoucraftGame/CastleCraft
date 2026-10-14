using System.Collections.Generic;
namespace PeakCraft.World;
internal static class CastleEnchantmentTargets {
 internal const uint Undead=128,Arthropod=256;
 // Native AI components are authoritative; a scene name or a translated display name is not.
 internal static uint Classify(IEnumerable<string> nativeComponents){
  uint flags=0;
  foreach(var type in nativeComponents){
   if(type=="Teran.AI_Skull"||type.StartsWith("Teran.AI_Skull_")||
    type=="Teran.AI_Ghost"||type=="Teran.AI_GhostInArms"||type=="Teran.AI_ZombieBoar"||type=="Teran.AI_Boss_GhostGeneral")flags|=Undead;
   if(type=="Teran.AI_Spider"||type.StartsWith("Teran.AI_Spider_")||type=="Teran.AI_Boss_DevilSpider"||type=="Teran.AI_CharacterSummon_SoulSpider")flags|=Arthropod;
  }
  return flags;
 }
}
