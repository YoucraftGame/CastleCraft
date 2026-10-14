using System.Collections.Generic;
using PeakCraft.Link;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakCraft.Render;

/// <summary>
/// Geometry Minecraft's own entity renderer produced, sent as batches (one texture each) of
/// triangles: the player's model in third person, the player's standing body (for PEAK's mirrors
/// in first person), and the scene of other entities and particles.
/// </summary>
internal sealed unsafe class CapturedModel
{
    private readonly Atlas atlas;
    private readonly string name;
    private readonly List<Vector3> positions = new();
    private readonly List<Vector2> uvs = new();
    private readonly List<Color32> colours = new();
    private readonly List<int> solid = new();
    private readonly List<int> cutout = new();
    private readonly List<int> blended = new();
    private readonly List<Material> materials = new();
    private Mesh? mesh;
    private bool logged;
    private bool drawLogged;
    private GameObject? drawable;
    private MeshRenderer? drawableRenderer;
    private GameObject? spriteObject;
    private MeshRenderer? spriteRenderer;
    private Camera? spriteCamera;
    private RenderTexture? spriteTexture;
    private RenderTexture? portraitTexture;
    private Camera? portraitCamera;
    private float nextPortrait;
    public RenderTexture? Portrait=>portraitTexture;
    public void DisposePortrait(){if(portraitCamera!=null)UnityEngine.Object.Destroy(portraitCamera.gameObject);if(portraitTexture!=null){portraitTexture.Release();UnityEngine.Object.Destroy(portraitTexture);}portraitCamera=null;portraitTexture=null;}
    public void UpdatePortrait(){
        if(!Shown||mesh==null||Time.unscaledTime<nextPortrait)return;
        nextPortrait=Time.unscaledTime+.1f;
        if(drawable==null){drawable=new GameObject("CastleCraft native "+name);drawable.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(drawable);drawable.AddComponent<MeshFilter>().sharedMesh=mesh;drawableRenderer=drawable.AddComponent<MeshRenderer>();drawableRenderer.sharedMaterials=materialArray;}
        if(portraitCamera==null){
            portraitTexture=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave};portraitTexture.Create();
            var obj=new GameObject("CastleCraft live portrait camera");obj.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(obj);portraitCamera=obj.AddComponent<Camera>();portraitCamera.enabled=false;portraitCamera.orthographic=true;portraitCamera.orthographicSize=.43f;portraitCamera.aspect=1;portraitCamera.nearClipPlane=.1f;portraitCamera.farClipPlane=12;portraitCamera.cullingMask=1<<PeakCraft.World.Coords.PortraitLayer;portraitCamera.clearFlags=CameraClearFlags.SolidColor;portraitCamera.backgroundColor=new Color(.14f,.14f,.15f,1);portraitCamera.targetTexture=portraitTexture;
            portraitCamera.transform.position=new Vector3(0,1.35f,-5);
        }
        bool active=drawable.activeSelf;int layer=drawable.layer;var position=drawable.transform.position;var rotation=drawable.transform.rotation;var scale=drawable.transform.localScale;
        drawable.SetActive(true);drawable.layer=PeakCraft.World.Coords.PortraitLayer;drawable.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);drawable.transform.localScale=Vector3.one;
        // Exported poses vary with skin geometry, scale, crouching and camera mode.
        // Aim at the upper body in the actual mesh instead of a fixed empty point.
        var bounds=mesh.bounds;
        float radius=Mathf.Max(.12f,Mathf.Max(bounds.size.x*.55f,bounds.size.y*.3f));
        var target=new Vector3(bounds.center.x,bounds.max.y-bounds.size.y*.22f,bounds.center.z);
        portraitCamera.orthographicSize=radius;
        portraitCamera.transform.position=target+Vector3.back*Mathf.Max(2f,bounds.size.z+1f);
        try{portraitCamera.Render();}finally{drawable.layer=layer;drawable.transform.SetPositionAndRotation(position,rotation);drawable.transform.localScale=scale;drawable.SetActive(active);}
    }

    public CapturedModel(Atlas atlas, string name)
    {
        this.atlas = atlas;
        this.name = name;
    }

    public bool Shown { get; private set; }

    public void Hide() {Shown = false; SetVisible(false);}
    public void SetVisible(bool visible) {if(drawable!=null)drawable.SetActive(visible);if(spriteObject!=null)spriteObject.SetActive(visible);if(spriteCamera!=null)spriteCamera.gameObject.SetActive(visible);}

    private readonly List<Vector3> normals = new();
    private readonly List<VertexBinding> bindings = new();
    private readonly List<Submesh> submeshes = new();
    private Vector3[] sourcePositions = System.Array.Empty<Vector3>();
    private Vector3[] sourceNormals = System.Array.Empty<Vector3>();
    private Color32[] sourceColours = System.Array.Empty<Color32>();
    private ulong topologyHash;
    private bool topologyValid;
    private Material[] materialArray = System.Array.Empty<Material>();
    private int meshRevision, renderedRevision = -1,renderedPixels=-1;
    private struct VertexBinding { public int source; public Vector3 weights; }
    private sealed class Submesh { public readonly List<int> indices = new(); public Material material = null!; }

    /// <summary>Posed vertices change each frame; UVs, cutouts and index buffers usually do not.</summary>
    public void Set(uint batchCount, uint vertexCount, Proto.RenBatch* batches, Proto.RenVertex* vertices)
    {
        Shown = batchCount > 0 && vertexCount > 0 && atlas.Usable;
        if (!Shown) { SetVisible(false); return; }
        if (mesh == null)
        {
            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }
        int count=(int)vertexCount;
        if(sourcePositions.Length<count)
        {
            int capacity=Mathf.NextPowerOfTwo(count);
            sourcePositions=new Vector3[capacity];sourceNormals=new Vector3[capacity];sourceColours=new Color32[capacity];
        }
        ulong hash=1469598103934665603UL;
        void Hash(uint value) {hash=(hash^value)*1099511628211UL;}
        Hash(batchCount);Hash(vertexCount);Hash(RenderSpace.FirstPerson?1u:0u);
        for(int i=0;i<count;i++)
        {
            var v=vertices[i];
            sourcePositions[i]=name=="scene"?RenderSpace.Relative(v.x,v.y,v.z):new Vector3(v.x,v.y,-v.z)*PeakCraft.World.Coords.Scale;
            sourceColours[i]=new Color32((byte)v.color,(byte)(v.color>>8),(byte)(v.color>>16),(byte)(v.color>>24));
            Hash((v.color>>24)>=250?1u:0u);
            Hash((uint)System.BitConverter.SingleToInt32Bits(v.u));Hash((uint)System.BitConverter.SingleToInt32Bits(v.v));
        }
        for(int i=0;i+2<count;i+=3)
        {
            var normal=Vector3.Cross(sourcePositions[i+2]-sourcePositions[i],sourcePositions[i+1]-sourcePositions[i]).normalized;
            sourceNormals[i]=sourceNormals[i+1]=sourceNormals[i+2]=normal;
        }
        for(uint b=0;b<batchCount;b++){Hash(batches[b].texture);Hash((uint)atlas.TopologyRevision(batches[b].texture));Hash(batches[b].first);Hash(batches[b].count);Hash(batches[b].flags);}
        bool rebuild=!topologyValid||hash!=topologyHash;
        if(rebuild)
        {
            topologyHash=hash;topologyValid=true;
            bindings.Clear();uvs.Clear();materials.Clear();
            for(int i=0;i<count;i++){bindings.Add(new VertexBinding{source=i,weights=Vector3.right});uvs.Add(new Vector2(vertices[i].u,vertices[i].v));}
            int sub=0;
            for(uint batch=0;batch<batchCount;batch++)
            {
                solid.Clear();cutout.Clear();blended.Clear();
                uint texture=batches[batch].texture;
                bool translucent=(batches[batch].flags&1)!=0;
                uint end=(uint)System.Math.Min((ulong)batches[batch].first+batches[batch].count,vertexCount);
                for(uint i=batches[batch].first;i+2<end;i+=3)
                {
                    var v=&vertices[i];
                    bool particle=(v[0].flags&0x70)==0;
                    // Animated particle alpha must be evaluated by the shader; a blank
                    // atlas frame must not permanently cull later nonblank frames.
                    // Vanilla skins may use a translucent render type even where their
                    // texels and vertices are opaque. Those faces still need depth in
                    // the host's private avatar camera, or the body looks hollow.
                    bool opaqueAvatar=CastleSurfaceRules.OpaqueModel(particle,name!="scene"||atlas.BinaryAlpha(texture),v[0].color>>24,v[1].color>>24,v[2].color>>24);
                    Surface surface=opaqueAvatar?Surface.Cutout:translucent?Surface.Translucent:particle?Surface.Cutout:atlas.Classify(texture,v[0].u,v[0].v,v[1].u,v[1].v,v[2].u,v[2].v);
                    if(surface==Surface.Empty)continue;
                    // Animated atlas sprites and particle billboards keep their alpha
                    // shader. Static entity skins/armor use cached opaque rectangles.
                    if(surface==Surface.Cutout&&texture!=0&&!particle)
                    {
                        int source=(int)i;
                        Vector2 a=uvs[source],b=uvs[source+1],c=uvs[source+2];
                        var pieces=atlas.OpaquePieces(texture,a,b,c);
                        for(int k=0;k+2<pieces.Length;k+=3)
                        {
                            int first=bindings.Count;
                            for(int j=0;j<3;j++)
                            {
                                var weights=pieces[k+j];bindings.Add(new VertexBinding{source=source,weights=weights});
                                uvs.Add(a*weights.x+b*weights.y+c*weights.z);
                            }
                            solid.Add(first);solid.Add(first+2);solid.Add(first+1);
                        }
                    }
                    else
                    {
                        var indices=surface==Surface.Solid?solid:surface==Surface.Cutout?cutout:blended;
                        indices.Add((int)i);indices.Add((int)i+2);indices.Add((int)i+1);
                    }
                }
                PrepareSubmesh(solid,texture,Surface.Solid,ref sub);
                PrepareSubmesh(cutout,texture,Surface.Cutout,ref sub);
                PrepareSubmesh(blended,texture,Surface.Translucent,ref sub);
            }
            while(submeshes.Count>sub)submeshes.RemoveAt(submeshes.Count-1);
            materialArray=materials.ToArray();
            mesh.Clear();
        }
        positions.Clear();colours.Clear();normals.Clear();
        Vector3 min=new(float.MaxValue,float.MaxValue,float.MaxValue),max=new(float.MinValue,float.MinValue,float.MinValue);
        for(int i=0;i<bindings.Count;i++)
        {
            var binding=bindings[i];int source=binding.source;
            Vector3 p;
            Color32 colour;
            if(i<count){p=sourcePositions[source];colour=sourceColours[source];}
            else
            {
                var w=binding.weights;
                p=sourcePositions[source]*w.x+sourcePositions[source+1]*w.y+sourcePositions[source+2]*w.z;
                var a=sourceColours[source];var b=sourceColours[source+1];var c=sourceColours[source+2];
                colour=new Color32((byte)(a.r*w.x+b.r*w.y+c.r*w.z),(byte)(a.g*w.x+b.g*w.y+c.g*w.z),
                    (byte)(a.b*w.x+b.b*w.y+c.b*w.z),(byte)(a.a*w.x+b.a*w.y+c.a*w.z));
            }
            positions.Add(p);colours.Add(colour);normals.Add(sourceNormals[source]);
            min=Vector3.Min(min,p);max=Vector3.Max(max,p);
        }
        // Exactly one upload for each changing stream. The previous implementation
        // re-uploaded the entire vertex buffer for every texture and every pass.
        mesh.SetVertices(positions);mesh.SetColors(colours);mesh.SetNormals(normals);
        if(rebuild)
        {
            mesh.SetUVs(0,uvs);mesh.subMeshCount=submeshes.Count;
            for(int sub=0;sub<submeshes.Count;sub++)mesh.SetTriangles(submeshes[sub].indices,sub,false);
            if(drawableRenderer!=null)drawableRenderer.sharedMaterials=materialArray;
        }
        mesh.bounds=new Bounds((min+max)*.5f,max-min);meshRevision++;
        if(!logged){logged=true;Plugin.Log.LogInfo($"{name}: native model received ({batchCount} batches, {vertexCount} source vertices, {bindings.Count} uploaded vertices)");}
    }

    private void PrepareSubmesh(List<int> indices,uint texture,Surface surface,ref int sub)
    {
        if(indices.Count==0)return;
        // Built-in Legacy cutout shaders have hard-coded backface culling. Keep
        // complete native avatar surfaces and make both windings explicit.
        if(name!="scene") {
            int frontCount=indices.Count;
            for(int i=0;i<frontCount;i+=3){indices.Add(indices[i]);indices.Add(indices[i+2]);indices.Add(indices[i+1]);}
        }
        var material=atlas.Material(texture,surface,true);if(material==null)return;
        if(sub==submeshes.Count)submeshes.Add(new Submesh());
        var target=submeshes[sub++];target.indices.Clear();target.indices.AddRange(indices);target.material=material;materials.Add(material);
    }

    /// <summary>Draws for one camera, or for every camera when <paramref name="camera"/> is null.</summary>
    public void Draw(Matrix4x4 matrix, Camera? camera = null, int layer = -1, int? sortingOrder = null)
    {
        if (!Shown || mesh == null)
        {
            return;
        }
        if(drawable==null){drawable=new GameObject("CastleCraft native " + name);drawable.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(drawable);drawable.AddComponent<MeshFilter>().sharedMesh=mesh;drawableRenderer=drawable.AddComponent<MeshRenderer>();drawableRenderer.sharedMaterials=materialArray;}
        drawable.layer=layer<0?(name=="scene"?PeakCraft.World.Coords.GeometryLayer:PeakCraft.World.Coords.RenderLayer):layer;
        drawable.transform.position=matrix.GetColumn(3);
        drawable.transform.rotation=matrix.rotation;
        drawable.transform.localScale=matrix.lossyScale;
        drawableRenderer!.sortingLayerID=PeakCraft.World.Coords.SortingLayer;
        drawableRenderer.sortingOrder=sortingOrder??PeakCraft.World.Coords.SortingOrder;
        drawable.SetActive(true);
        if(name!="scene")DrawSprite(matrix);
        if(!drawLogged){drawLogged=true;Plugin.Log.LogInfo($"Native {name} mesh bounds={mesh.bounds}, triangles={mesh.triangles.Length}, world={drawable.transform.position}, scale={drawable.transform.localScale}, cameras={string.Join(";", System.Linq.Enumerable.Select(Camera.allCameras,c=>$"{c.name}: rotation={c.transform.eulerAngles},near={c.nearClipPlane},far={c.farClipPlane},ortho={c.orthographic},size={c.orthographicSize}"))}");}
    }
    private void DrawSprite(Matrix4x4 matrix){
        if(spriteObject==null){
            spriteTexture=new RenderTexture(atlas.AvatarResolution,atlas.AvatarResolution,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};spriteTexture.Create();
            var cameraObject=new GameObject("CastleCraft orthographic native avatar");cameraObject.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(cameraObject);spriteCamera=cameraObject.AddComponent<Camera>();spriteCamera.enabled=false;spriteCamera.orthographic=true;spriteCamera.orthographicSize=1.75f;spriteCamera.aspect=1;spriteCamera.nearClipPlane=.1f;spriteCamera.farClipPlane=20;spriteCamera.cullingMask=1<<PeakCraft.World.Coords.AvatarLayer;spriteCamera.clearFlags=CameraClearFlags.SolidColor;spriteCamera.backgroundColor=Color.clear;spriteCamera.targetTexture=spriteTexture;spriteCamera.transform.position=new Vector3(0,1.2f,-5);
            spriteObject=new GameObject("CastleCraft full Minecraft character sprite");spriteObject.hideFlags=HideFlags.HideAndDontSave;UnityEngine.Object.DontDestroyOnLoad(spriteObject);var quad=new Mesh{hideFlags=HideFlags.HideAndDontSave};quad.vertices=new[]{new Vector3(-1.75f,-.55f,0),new Vector3(1.75f,-.55f,0),new Vector3(1.75f,2.95f,0),new Vector3(-1.75f,2.95f,0)};quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};quad.triangles=new[]{0,1,2,0,2,3};quad.RecalculateBounds();spriteObject.AddComponent<MeshFilter>().sharedMesh=quad;spriteRenderer=spriteObject.AddComponent<MeshRenderer>();spriteRenderer.sharedMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=spriteTexture,hideFlags=HideFlags.HideAndDontSave};
        }
        drawable!.layer=PeakCraft.World.Coords.AvatarLayer;drawable.transform.position=Vector3.zero;drawableRenderer!.sortingOrder=0;
        spriteRenderer!.sharedMaterial.color=Plugin.Instance.AvatarTint;
        spriteObject.layer=PeakCraft.World.Coords.RenderLayer;spriteObject.transform.position=matrix.GetColumn(3);spriteRenderer!.sortingLayerID=PeakCraft.World.Coords.SortingLayer;spriteRenderer.sortingOrder=PeakCraft.World.Coords.SortingOrder;spriteObject.SetActive(true);spriteCamera!.gameObject.SetActive(true);if(renderedRevision!=meshRevision||renderedPixels!=atlas.PixelRevision){spriteCamera.Render();renderedRevision=meshRevision;renderedPixels=atlas.PixelRevision;}
    }
}

