using System;
using System.Collections.Generic;
namespace PeakCraft.World;
internal static class CastleStageClassification {
 internal static bool Vertical(IEnumerable<string> names){
  foreach(var part in names){var name=part.ToLowerInvariant();if(name.Contains("wall")||name.Contains("gate")||name.Contains("door")||name.Contains("column")||name.Contains("roof"))return true;}
  return false;
 }
 internal static bool Billboard(bool actor,IEnumerable<string> partHierarchy)=>actor&&!Vertical(partHierarchy);
}
