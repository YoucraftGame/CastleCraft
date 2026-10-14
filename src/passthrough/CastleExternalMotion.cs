using System;
namespace PeakCraft;
internal static class CastleExternalMotion {
 public const double MaxStep=.25;
 public static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
 public static bool Valid(double x,double z)=>Finite(x)&&Finite(z)&&x*x+z*z<=MaxStep*MaxStep+1e-6;
 public static bool ValidOrigin(double ox,double oz,double x,double z)=>Finite(ox)&&Finite(oz)&&(ox-x)*(ox-x)+(oz-z)*(oz-z)<=4;
}
