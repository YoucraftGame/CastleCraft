using System;
using System.Collections.Generic;
using UnityEngine;
namespace PeakCraft.Render;
/// Alpha masks are geometry so transparent texels never hide the room behind them.
internal static class OpaqueClip {
 public static Vector3[] Triangle((int width,int height,byte[] alpha) map,Vector2 a,Vector2 b,Vector2 c){
        float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
        if (Mathf.Abs(denominator)<1e-12f) {
            // A single-texel UV may cover a complete native custom-model face.
            int px=Mathf.Clamp((int)(a.x*map.width),0,map.width-1),py=Mathf.Clamp((int)(a.y*map.height),0,map.height-1);
            return map.alpha[py*map.width+px]>=26 ? new[]{Vector3.right,Vector3.up,Vector3.forward} : Array.Empty<Vector3>();
        }
        int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))*map.width),0,map.width-1);
        int x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))*map.width),0,map.width);
        int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))*map.height),0,map.height-1);
        int y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))*map.height),0,map.height);
        var rectangles=new List<RectInt>();
        var previous=new Dictionary<(int,int),int>();
        var current=new Dictionary<(int,int),int>();
        for (int y=y0;y<y1;y++)
        {
            current.Clear();
            for (int x=x0;x<x1;)
            {
                if (map.alpha[y*map.width+x]<26) {x++;continue;}
                int left=x++;
                while(x<x1&&map.alpha[y*map.width+x]>=26)x++;
                var run=(left,x);
                if (previous.TryGetValue(run,out int index))
                {
                    var rectangle=rectangles[index];rectangle.height++;rectangles[index]=rectangle;
                }
                else {index=rectangles.Count;rectangles.Add(new RectInt(left,y,x-left,1));}
                current[run]=index;
            }
            var swap=previous;previous=current;current=swap;
        }
        var output=new List<Vector3>();
        var polygon=new List<Vector2>(8);
        var clipped=new List<Vector2>(8);
        Vector3 Weights(Vector2 point)
        {
            float x=((b.y-c.y)*(point.x-c.x)+(c.x-b.x)*(point.y-c.y))/denominator;
            float y=((c.y-a.y)*(point.x-c.x)+(a.x-c.x)*(point.y-c.y))/denominator;
            return new Vector3(x,y,1-x-y);
        }
        foreach(var rectangle in rectangles)
        {
            polygon.Clear();polygon.Add(a);polygon.Add(b);polygon.Add(c);
            for(int side=0;side<4&&polygon.Count>0;side++)
            {
                bool horizontal=side<2,minimum=side==0||side==2;
                float edge=horizontal?(side==0?rectangle.xMin:rectangle.xMax)/(float)map.width
                                     :(side==2?rectangle.yMin:rectangle.yMax)/(float)map.height;
                clipped.Clear();
                var last=polygon[polygon.Count-1];
                float lastAxis=horizontal?last.x:last.y;
                bool lastInside=minimum?lastAxis>=edge:lastAxis<=edge;
                foreach(var point in polygon)
                {
                    float axis=horizontal?point.x:point.y;
                    bool inside=minimum?axis>=edge:axis<=edge;
                    if(inside!=lastInside)clipped.Add(Vector2.LerpUnclamped(last,point,(edge-lastAxis)/(axis-lastAxis)));
                    if(inside)clipped.Add(point);
                    last=point;lastAxis=axis;lastInside=inside;
                }
                var swap=polygon;polygon=clipped;clipped=swap;
            }
            for(int i=1;i+1<polygon.Count;i++)
            {
                // A rectangle can just touch the original diagonal. Skip those
                // zero-area triangles rather than sending useless vertices.
                var aa=Weights(polygon[0]);var bb=Weights(polygon[i]);var cc=Weights(polygon[i+1]);
                if(Vector3.Cross(bb-aa,cc-aa).sqrMagnitude<1e-16f)continue;
                output.Add(aa);output.Add(bb);output.Add(cc);
            }
        }
        return output.ToArray();
 }
}
