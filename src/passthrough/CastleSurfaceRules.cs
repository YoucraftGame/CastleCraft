namespace PeakCraft.Render;
internal static class CastleSurfaceRules {
 internal static bool OpaqueModel(bool particle,bool binaryAlpha,uint a,uint b,uint c)=>!particle&&binaryAlpha&&a>=250&&b>=250&&c>=250;
}
