using System;
namespace PeakCraft.World;
[Serializable] internal sealed class CastleStatus {
 public float health,maximum,absorption;
 public int armor,hurt,lifeAck;
 public uint arrivalSeq,arrivalEpoch;
 public double arrivalX,arrivalY,arrivalZ;
 public bool burning,creative;
 public Effect[] effects=Array.Empty<Effect>();
 [Serializable] public sealed class Effect{public string id="";public int level,ticks;}
}
