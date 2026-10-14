namespace PeakCraft.World;

/// <summary>Castle gear never adds HP, defence or speed to the Minecraft player.</summary>
internal static class CastleGearRules {
 internal static bool NativeWeaponAttribute(string attribute,bool selected)=>selected&&
  (attribute=="ATTACK"||attribute=="CRITICAL"||attribute=="ReductionOfCD");
}
