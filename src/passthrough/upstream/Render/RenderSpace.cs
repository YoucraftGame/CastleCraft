using PeakCraft.World;
using UnityEngine;
namespace PeakCraft.Render;
/// <summary>Keep guest geometry in the same projected world as Castle's native scene.</summary>
internal static class RenderSpace
{
    // Both cameras use the same geometry. Perspective changes the camera, never
    // the collision/world coordinate contract underneath an existing room.
    public static bool FirstPerson => false;
    public static Vector3 Relative(double x,double y,double z)=>Coords.Relative(x,y,z);
    public static Vector3 ToWorld(double x,double y,double z)=>Coords.ToUnity(x,y,z);
    public static int SortingOrder(double floorZ,int bias=4)
    {
        // Native Teran sorting uses floor Y, not rendered head/particle height.
        return Mathf.Clamp((int)((float)floorZ*Coords.DepthScale*100f)*10+6000+bias,-32760,32760);
    }
}
