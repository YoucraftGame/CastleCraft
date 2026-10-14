using System.Reflection;
using UnityEngine;
namespace PeakCraft.World;
/// One native shadow centre is the floor anchor. Hero roots and animated bodies are not feet.
internal static class CastleGround {
 private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static Collider2D? Shadow(Component owner)=>owner.GetType().GetProperty("ShadowCollider",Flags)?.GetValue(owner,null) as Collider2D;
 public static Vector3 Position(Component owner){
  var shadow=Shadow(owner);if(shadow!=null&&shadow.enabled)return new Vector3(shadow.bounds.center.x,shadow.bounds.center.y,owner.transform.position.z);
  var transform=owner.GetType().GetProperty("ShadowTransform",Flags)?.GetValue(owner,null) as Transform??owner.transform.Find("shadow");
  return transform!=null?new Vector3(transform.position.x,transform.position.y,owner.transform.position.z):owner.transform.position;
 }
 public static void Place(Component owner,Vector3 feet){var offset=Position(owner)-owner.transform.position;owner.transform.position=new Vector3(feet.x-offset.x,feet.y-offset.y,owner.transform.position.z);}
}
