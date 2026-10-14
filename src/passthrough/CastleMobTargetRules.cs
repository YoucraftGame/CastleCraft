using System;
namespace PeakCraft.World;
internal static class CastleMobTargetRules {
 internal static int Debuff(string name)=>name switch{"Poisoned"=>2,"Burned"=>3,"Freezing"=>4,"Electrical"=>5,"Confused"=>6,"Slippery"=>7,"Bleeding"=>8,"Frozen"=>9,"Petrochemical"=>10,_=>0};
 internal static int Duration(string name,float seconds){if(seconds<0)seconds=name switch{"Burned"=>4,"Electrical"=>3,"Freezing"=>5,"Poisoned"=>5,"Confused"=>5,"Slippery"=>8,"Frozen"=>4,"Bleeding"=>6,"Petrochemical"=>7,_=>0};return float.IsNaN(seconds)||float.IsInfinity(seconds)?0:(int)(Math.Clamp(seconds,0,60)*1000);}
 internal static bool Valid(uint id,int packetEpoch,int roomEpoch,float health,float x,float y,float z,float width,float height,float maximum)=>id!=0&&packetEpoch==roomEpoch&&Finite(health)&&health>0&&Finite(x)&&Finite(y)&&Finite(z)&&Finite(width)&&width>0&&Finite(height)&&height>0&&Finite(maximum)&&maximum>=health;
 private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
}
