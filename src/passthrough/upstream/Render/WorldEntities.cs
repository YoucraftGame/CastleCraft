using System.Collections.Generic;
using PeakCraft.Link;
using PeakCraft.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakCraft.Render;

/// <summary>
/// The light entities Minecraft lists in the world-entities table instead of capturing as
/// geometry: arrows in flight and stuck, tridents, dropped and thrown items. Each is a few quads
/// built here from its atlas rectangles, each in a persistent mesh sorted by its floor depth. Block cracks and contact
/// shadows in the same table are not drawn.
/// </summary>
internal sealed class WorldEntities
{
    private const uint Arrow = 1, Item = 2, Trident = 3, Block = 4, Shadow = 6;
    private Material? shadowMaterial;
    // Minecraft's arrow model: 16 x 5 texels at 0.9 / 16 blocks per texel, shifted 4 texels back.
    private const float Texel = 0.05625f;

    private readonly PeakLink link;
    private readonly Atlas atlas;
    private readonly List<PeakLink.WorldEntity> entities = new();
    private readonly List<Vector3> positions = new();
    private readonly List<Vector2> uvs = new();
    private readonly List<Color32> colours = new();
    private readonly List<int> indices = new();
    private bool logged;
    private int frame;
    private readonly Dictionary<uint,Drawable> drawables=new();
    private readonly List<uint> expired=new();
    private sealed class Drawable
    {
        public readonly GameObject obj;
        public readonly Mesh mesh;
        public readonly MeshRenderer renderer;
        public int vertexCount,seen;
        public ulong uvHash;
        public float lastSeen;
        public Drawable()
        {
            obj=new GameObject("CastleCraft dropped item or projectile"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(obj);
            mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=obj.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        public void Destroy(){Object.Destroy(mesh);Object.Destroy(obj);}
    }
    public void Hide(){foreach(var drawable in drawables.Values)drawable.obj.SetActive(false);}
    public WorldEntities(PeakLink link,Atlas atlas){this.link=link;this.atlas=atlas;}
    public void Draw()
    {
        if(!atlas.Usable){Hide();return;}
        // A producer write in progress is not a despawn: preserve the last complete frame.
        if(!link.ReadWorldEntities(entities))return;
        frame++;
        foreach(var e in entities)
        {
            positions.Clear();uvs.Clear();colours.Clear();indices.Clear();
            switch(e.kind)
            {
                case Arrow:AddArrow(Vector3.zero,e);break;
                case Item:case Trident:AddSprite(Vector3.zero,e);break;
                case Block:AddCube(Vector3.zero,e);break;
                case Shadow:AddShadow(e);break;
            }
            if(indices.Count==0)continue;
            if(!drawables.TryGetValue(e.id,out var drawable)){drawable=new Drawable();drawables.Add(e.id,drawable);}
            drawable.seen=frame;drawable.lastSeen=Time.unscaledTime;
            ulong hash=1469598103934665603UL;
            foreach(var uv in uvs){hash=(hash^(uint)System.BitConverter.SingleToInt32Bits(uv.x))*1099511628211UL;hash=(hash^(uint)System.BitConverter.SingleToInt32Bits(uv.y))*1099511628211UL;}
            bool topology=drawable.vertexCount!=positions.Count;
            if(topology)drawable.mesh.Clear();
            drawable.mesh.SetVertices(positions);drawable.mesh.SetColors(colours);
            if(topology||drawable.uvHash!=hash){drawable.mesh.SetUVs(0,uvs);drawable.uvHash=hash;}
            if(topology){drawable.mesh.SetTriangles(indices,0,false);drawable.vertexCount=positions.Count;}
            drawable.mesh.RecalculateNormals();drawable.mesh.RecalculateBounds();
            drawable.renderer.sharedMaterial=e.kind==Shadow?ShadowMaterial():atlas.Material(0,Surface.Cutout,true);
            drawable.obj.transform.position=RenderSpace.ToWorld(e.x,e.y,e.z);drawable.obj.layer=Coords.GeometryLayer;
            drawable.renderer.sortingLayerID=Coords.SortingLayer;drawable.renderer.sortingOrder=RenderSpace.SortingOrder(e.z,5);drawable.obj.SetActive(true);
        }
        expired.Clear();
        foreach(var pair in drawables)if(pair.Value.seen!=frame)
        {
            pair.Value.obj.SetActive(false);if(Time.unscaledTime-pair.Value.lastSeen>5f)expired.Add(pair.Key);
        }
        foreach(uint id in expired){drawables[id].Destroy();drawables.Remove(id);}
        if(!logged&&entities.Count>0){logged=true;Plugin.Log.LogInfo($"entities: drawing Minecraft arrows/items with individual depth ({entities.Count} this frame)");}
    }
    private Material? ShadowMaterial(){
        if(shadowMaterial!=null)return shadowMaterial;
        var shader=Shader.Find("Sprites/Default");if(shader==null)return null;
        shadowMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave,mainTexture=Texture2D.whiteTexture};
        shadowMaterial.renderQueue=(int)RenderQueue.Transparent;return shadowMaterial;
    }
    private void AddShadow(in PeakLink.WorldEntity e){
        float radius=Mathf.Clamp(e.scale*.5f,.12f,4),strength=Mathf.Clamp01(e.yaw);
        var center=new Color32(0,0,0,(byte)(90*strength));var edge=new Color32(0,0,0,0);
        positions.Add(Vector3.zero);uvs.Add(Vector2.zero);colours.Add(center);
        const int sides=24;
        for(int i=0;i<sides;i++){
            float a=i*Mathf.PI*2/sides;
            positions.Add(RenderSpace.Relative(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));uvs.Add(Vector2.zero);colours.Add(edge);
        }
        for(int i=0;i<sides;i++){indices.AddRange(new[]{0,1+i,1+(i+1)%sides,0,1+(i+1)%sides,1+i});}
    }
    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float v0, float u1, float v1, Color32 colour)
    {
        int first = positions.Count;
        positions.Add(RenderSpace.Relative(a.x,a.y,-a.z));
        positions.Add(RenderSpace.Relative(b.x,b.y,-b.z));
        positions.Add(RenderSpace.Relative(c.x,c.y,-c.z));
        positions.Add(RenderSpace.Relative(d.x,d.y,-d.z));
        uvs.Add(new Vector2(u0, v1));
        uvs.Add(new Vector2(u1, v1));
        uvs.Add(new Vector2(u1, v0));
        uvs.Add(new Vector2(u0, v0));
        for (int i = 0; i < 4; i++)
        {
            colours.Add(colour);
        }
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
        indices.Add(first);
        indices.Add(first + 2);
        indices.Add(first + 3);
    }

    // Two crossed side views along the flight direction, plus the square back plate.
    private void AddArrow(Vector3 at, in PeakLink.WorldEntity e)
    {
        float yaw = e.yaw * Mathf.Deg2Rad, pitch = e.pitch * Mathf.Deg2Rad;
        // Minecraft's arrow heading (x = sin yaw, z = cos yaw), with Z flipped for Unity.
        var forward = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch));
        Vector3 side = Vector3.Cross(Mathf.Abs(forward.y) > 0.99f ? Vector3.right : Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, side);
        Vector3 tail = at - forward * (12f * Texel), tip = at + forward * (4f * Texel);
        var white = new Color32(255, 255, 255, 255);
        for (int k = 0; k < 2; k++)
        {
            Vector3 across = (k == 0 ? side + up : side - up).normalized * (2.5f * Texel);
            Quad(tail - across, tip - across, tip + across, tail + across, e.uv0, e.uv1, e.uv2, e.uv3, white);
        }
        Vector3 back = at - forward * (11f * Texel);
        Vector3 s = side * (2.5f * Texel), u = up * (2.5f * Texel);
        Quad(back - s - u, back + s - u, back + s + u, back - s + u, e.uv4, e.uv5, e.uv6, e.uv7, white);
    }

    // A dropped item, thrown item or trident: its icon as a flat sprite turning about the vertical.
    private void AddSprite(Vector3 at, in PeakLink.WorldEntity e)
    {
        float size = e.scale > 0f ? e.scale : 0.5f;
        Vector3 right = Quaternion.Euler(0f, e.yaw, 0f) * Vector3.right * (size * 0.5f);
        Vector3 up = Vector3.up * size;
        Quad(at - right, at + right, at + right + up, at - right + up, e.uv0, e.uv1, e.uv2, e.uv3, new Color32(255, 255, 255, 255));
    }

    // A dropped block: a small spinning cube with the block's side, top and bottom textures.
    private void AddCube(Vector3 at, in PeakLink.WorldEntity e)
    {
        float size = e.scale > 0f ? e.scale : 0.25f;
        Quaternion spin = Quaternion.Euler(0f, e.yaw, 0f);
        Vector3 x = spin * Vector3.right * (size * 0.5f), z = spin * Vector3.forward * (size * 0.5f), y = Vector3.up * size;
        var white = new Color32(255, 255, 255, 255);
        Color32 tint = e.tint != 0 ? new Color32((byte)e.tint, (byte)(e.tint >> 8), (byte)(e.tint >> 16), 255) : white;
        Quad(at - x - z, at + x - z, at + x - z + y, at - x - z + y, e.uv0, e.uv1, e.uv2, e.uv3, white);
        Quad(at + x - z, at + x + z, at + x + z + y, at + x - z + y, e.uv0, e.uv1, e.uv2, e.uv3, white);
        Quad(at + x + z, at - x + z, at - x + z + y, at + x + z + y, e.uv0, e.uv1, e.uv2, e.uv3, white);
        Quad(at - x + z, at - x - z, at - x - z + y, at - x + z + y, e.uv0, e.uv1, e.uv2, e.uv3, white);
        Quad(at - x - z + y, at + x - z + y, at + x + z + y, at - x + z + y, e.uv4, e.uv5, e.uv6, e.uv7, tint);
        Quad(at - x + z, at + x + z, at + x - z, at - x - z, e.uv8, e.uv9, e.uv10, e.uv11, white);
    }
}

