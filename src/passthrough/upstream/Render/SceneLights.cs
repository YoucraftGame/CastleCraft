using System.Collections.Generic;
using BepInEx.Configuration;
using PeakCraft.World;
using UnityEngine;
namespace PeakCraft.Render;

/// <summary>Bounded point-light pool for guest torches, lava and other light-emitting blocks.</summary>
internal sealed unsafe class SceneLights
{
    private readonly Dictionary<(int,int,int),List<Emitter>> sections=new();
    private readonly List<Emitter> candidates=new();
    private readonly List<Light> pool=new();
    private readonly int maximum;
    private bool dirty=true;
    private float nextUpdate;
    private Vector3 focus;
    private GameObject? root;
    private struct Emitter{public Vector3 position;public Color color;public float intensity,range,distance;}
    private struct Cell{public Vector3 sum;public Vector3 rgb;public int count,emission;}
    public SceneLights(ConfigFile config)
    {
        maximum=Mathf.Clamp(config.Bind("Render","MaxGuestLights",24,
            "Maximum visible Minecraft point lights. Nearby emitters in a 4-block cell share one light; zero disables imported lights.").Value,0,64);
    }
    public void Set(byte* payload,uint bytes)
    {
        if(bytes<16)return;int sx=*(int*)payload,sy=*(int*)(payload+4),sz=*(int*)(payload+8),count=*(int*)(payload+12);
        if(count<0||count>4096||bytes<16L+count*8L)return;
        var key=(sx,sy,sz);dirty=true;
        if(count==0){sections.Remove(key);return;}
        var cells=new Cell[64];
        for(int i=0;i<count;i++)
        {
            byte* p=payload+16+i*8;int x=p[0],y=p[1],z=p[2],emission=p[3];
            if(x>15||y>15||z>15||emission==0)continue;
            uint color=*(uint*)(p+4);int index=(x>>2)|((y>>2)<<2)|((z>>2)<<4);
            ref var cell=ref cells[index];cell.sum+=new Vector3(sx*16+x+.5f,sy*16+y+.5f,sz*16+z+.5f);
            cell.rgb+=new Vector3(color&255,(color>>8)&255,(color>>16)&255);cell.count++;cell.emission=Mathf.Max(cell.emission,emission);
        }
        if(!sections.TryGetValue(key,out var emitters)){emitters=new List<Emitter>(64);sections.Add(key,emitters);}emitters.Clear();
        foreach(var cell in cells)if(cell.count>0)
        {
            var position=cell.sum/cell.count;var rgb=cell.rgb/(cell.count*255f);
            emitters.Add(new Emitter{position=RenderSpace.ToWorld(position.x,position.y,position.z),color=new Color(rgb.x,rgb.y,rgb.z),
                intensity=.8f*cell.emission/15f,range=Mathf.Max(1f,cell.emission*Coords.Scale*.6f)});
        }
    }
    public void Update(bool visible,Vector3 near)
    {
        if(root!=null)root.SetActive(visible&&maximum>0);
        if(!visible||maximum==0)return;
        bool moved=(near-focus).sqrMagnitude>1f;
        if(!dirty&&!moved)return;
        if(Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.1f;focus=near;dirty=false;
        candidates.Clear();
        foreach(var entries in sections.Values)foreach(var entry in entries)
        {
            var candidate=entry;candidate.distance=(candidate.position-near).sqrMagnitude;
            if(candidate.distance<=400f)candidates.Add(candidate);
        }
        candidates.Sort((a,b)=>a.distance.CompareTo(b.distance));
        int active=Mathf.Min(maximum,candidates.Count);
        if(active>0&&root==null){root=new GameObject("CastleCraft imported scene lights"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(root);}
        while(pool.Count<active)
        {
            var obj=new GameObject("Minecraft light"){hideFlags=HideFlags.HideAndDontSave};obj.transform.SetParent(root!.transform,false);
            var light=obj.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForceVertex;pool.Add(light);
        }
        for(int i=0;i<pool.Count;i++)
        {
            var light=pool[i];light.enabled=i<active;if(!light.enabled)continue;
            var emitter=candidates[i];light.transform.position=emitter.position;light.color=emitter.color;
            light.range=emitter.range;light.intensity=emitter.intensity;
            light.cullingMask=(1<<Coords.RenderLayer)|(1<<Coords.GeometryLayer);
        }
    }
    public void Clear(){sections.Clear();candidates.Clear();foreach(var light in pool)light.enabled=false;dirty=true;}
}
