using System;
using PeakCraft.Link;
namespace PeakCraft;
// Continue only the current MC render interval; never extrapolate beyond its latest tick.
internal static class CastlePose {
 public static void Sample(in Proto.GuestState state,long now,long frequency,out double x,out double y,out double z){
  x=state.x;y=state.y;z=state.z;
  if(state.tickQpc==0||frequency<=0||state.tickMs<=1)return;
  double age=(now-state.tickQpc)*1000.0/frequency;
  if(age<0||age>=250)return;
  double t=Math.Min(1,age/state.tickMs),nx=state.prevX+(state.curX-state.prevX)*t,ny=state.prevY+(state.curY-state.prevY)*t,nz=state.prevZ+(state.curZ-state.prevZ)*t;
  if(double.IsNaN(nx)||double.IsInfinity(nx)||double.IsNaN(ny)||double.IsInfinity(ny)||double.IsNaN(nz)||double.IsInfinity(nz))return;
  if((nx-x)*(nx-x)+(ny-y)*(ny-y)+(nz-z)*(nz-z)>=16)return;
  x=nx;y=ny;z=nz;
 }
}
