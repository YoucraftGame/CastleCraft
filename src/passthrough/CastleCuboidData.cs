using System;
namespace PeakCraft.World;
// Generated models use Minecraft's 16-unit element/face format, without a runtime JSON dependency.
internal readonly struct CastleCuboid {
 internal readonly float X0,Y0,Z0,X1,Y1,Z1;
 internal readonly string Palette;
 internal CastleCuboid(float x0,float y0,float z0,float x1,float y1,float z1,string palette){X0=x0;Y0=y0;Z0=z0;X1=x1;Y1=y1;Z1=z1;Palette=palette;}
 internal bool Valid=>X1>X0&&Y1>Y0&&Z1>Z0&&X0>=-16&&Y0>=-16&&Z0>=-16&&X1<=32&&Y1<=32&&Z1<=32;
}
