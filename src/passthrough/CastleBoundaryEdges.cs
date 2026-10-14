using System;
using System.Collections.Generic;
namespace PeakCraft.World;
internal sealed class CastleBoundaryEdges {
 private sealed class Edge {internal int Count;internal int A,B,C,D;}
 private readonly Dictionary<(long,long,long,long),Edge> edges=new();
 private static long Snap(double v)=>(long)Math.Round(v*100000);
 internal void Add(double ax,double ay,double bx,double by,int a,int b,int c,int d){
  var x=Snap(ax);var y=Snap(ay);var u=Snap(bx);var v=Snap(by);
  if(x==u&&y==v)return;
  var key=x<u||x==u&&y<v?(x,y,u,v):(u,v,x,y);
  if(edges.TryGetValue(key,out var edge))edge.Count++;
  else edges[key]=new Edge{Count=1,A=a,B=b,C=c,D=d};
 }
 internal IEnumerable<int[]> Triangles(){
  foreach(var edge in edges.Values)if(edge.Count==1)
   yield return new[]{edge.A,edge.B,edge.C,edge.A,edge.C,edge.D};
 }
}
