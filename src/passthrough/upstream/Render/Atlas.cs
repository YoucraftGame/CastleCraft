using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakCraft.Render;

/// <summary>How a face is drawn, decided per triangle from its texture's alpha (see <see cref="Atlas.Classify"/>).</summary>
internal enum Surface
{
    /// <summary>Every texel opaque: PEAK's opaque pass, depth prepass and all.</summary>
    Solid,

    /// <summary>Holes in it (leaves, a helmet's face opening, an arrow's sprite): blended, but writing depth.</summary>
    Cutout,

    /// <summary>Minecraft's translucent layer (stained glass, water): blended, no depth write.</summary>
    Translucent,

    /// <summary>Nothing to see (a skin's unused hat layer): not drawn at all.</summary>
    Empty,
}

/// <summary>
/// Minecraft's textures and the materials that draw with them (KTD11). BepInEx cannot compile
/// shaders, so the material is borrowed: URP's particle shaders ship with PEAK, multiply vertex
/// colour (tint and ambient occlusion ride on it) and are lit by PEAK's lights.
///
/// PEAK's build of those shaders has no alpha-clip variant (PEAK never uses it, so it was stripped):
/// a clipped material draws its transparent texels black. Cutout is done without it: texture alpha
/// is kept on the CPU, every triangle is classified, and only faces that really have holes go
/// through a blended material that still writes depth.
/// </summary>
internal sealed unsafe class Atlas
{
    private static readonly string[] Candidates =
    {
          "Legacy Shaders/VertexLit",
        "Sprites/Default",
        "Unlit/Transparent",
        "Particles/Standard Unlit",
    };

    private readonly Shader? shader;
    private readonly float brightness;
    private readonly Dictionary<uint, Texture2D> textures = new(); // 0 = the block atlas
    private readonly Dictionary<uint, (int width, int height, byte[] alpha)> alphas = new();
    private readonly Dictionary<ulong, Material> materials = new();
    private readonly Dictionary<(int, int), Texture2D> stamps = new();
      private readonly Dictionary<ulong,Surface> classifications=new();
    private readonly Dictionary<(uint,float,float,float,float,float,float),Vector3[]> opaquePieces = new();
    public int Revision { get; private set; }
    private readonly Dictionary<uint,int> topologyRevisions=new();
    public int TopologyRevision(uint id)=>topologyRevisions.TryGetValue(id,out int revision)?revision:0;
    public int PixelRevision {get;private set;}
    public int AvatarResolution { get; }

    // A transparent texel must not write depth. The retail game's shader bundle
    // lacks an alpha-test variant, so cutout faces use opaque rectangles. Merge
    // adjacent texels BEFORE clipping: a 64x64 opaque patch is two triangles,
    // not 8192 triangles, and the weights are reused for every animation pose.
    public Vector3[] OpaquePieces(uint id, Vector2 a, Vector2 b, Vector2 c)
    {
        var key = (id,a.x,a.y,b.x,b.y,c.x,c.y);
        if (opaquePieces.TryGetValue(key,out var cached)) return cached;
        if (!alphas.TryGetValue(id,out var map)) return Array.Empty<Vector3>();
        cached=OpaqueClip.Triangle(map,a,b,c);opaquePieces[key]=cached;return cached;
    }

    public Atlas(ConfigFile config)
    {
        AvatarResolution = Mathf.Clamp(config.Bind("Render", "AvatarResolution", 512,
            "Native avatar render target size (256-2048). Textures retain their original pixels; this prevents the 256px render target from degrading armor detail.").Value,256,2048);
        ConfigEntry<string> wanted = config.Bind("Render", "BlockShader", "",
            "Shader for Minecraft's blocks and skin. Empty picks URP's particle shaders (vertex colour, lit by PEAK).");
        brightness = config.Bind("Render", "BlockBrightness", 1.0f,
            "Scales the colour of Minecraft's blocks under PEAK's lights (PEAK's sun is brighter than Minecraft's textures expect).").Value;
        if (wanted.Value.Length > 0)
        {
            shader = Shader.Find(wanted.Value);
        }
        foreach (string name in Candidates)
        {
            if (shader != null && shader.isSupported)
            {
                break;
            }
            shader = Shader.Find(name);
        }
        if (shader == null)
        {
            Plugin.Log.LogError("blocks: no usable shader found in PEAK; Minecraft's blocks will not be drawn");
        }
        else
        {
            Plugin.Log.LogInfo($"blocks: drawing with PEAK's shader '{shader.name}'");
        }
    }

    public bool Usable => shader != null;
    private readonly Dictionary<uint,bool> binaryAlphas=new();
    public bool BinaryAlpha(uint id){
        if(binaryAlphas.TryGetValue(id,out bool value))return value;
        if(!alphas.TryGetValue(id,out var map))return false;
        foreach(byte alpha in map.alpha)if(alpha>=26&&alpha<250){binaryAlphas[id]=false;return false;}
        binaryAlphas[id]=true;return true;
    }

    /// <summary>kRenAtlas / kRenTexture: a whole texture, RGBA8, top row first.</summary>
    public void SetTexture(uint id, int width, int height, byte* pixels)
    {
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        {
            return;
        }
        if (!textures.TryGetValue(id, out Texture2D? texture) || texture.width != width || texture.height != height)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
            texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = id == 0 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            textures[id] = texture;
            foreach (KeyValuePair<ulong, Material> pair in materials)
            {
                if ((uint)(pair.Key >> 8) == id)
                {
                    pair.Value.SetTexture("_BaseMap", texture);
                    pair.Value.mainTexture = texture;
                }
            }
            Plugin.Log.LogInfo(id == 0 ? $"blocks: atlas {width}x{height}" : $"blocks: texture {id} {width}x{height}");
        }
        bool replacing = alphas.ContainsKey(id);
        var alpha = new byte[width * height];
        for (int i = 0; i < alpha.Length; i++)
        {
            alpha[i] = pixels == null ? (byte)0 : pixels[i * 4 + 3];
        }
        alphas[id] = (width, height, alpha);binaryAlphas.Remove(id);
        if(replacing){classifications.Clear();opaquePieces.Clear();}
        Revision++;
        topologyRevisions[id]=TopologyRevision(id)+1;PixelRevision++;
        // Loaded as sent: Unity's row 0 is then Minecraft's top row, so Minecraft's v is used unchanged.
        if (pixels == null) texture.LoadRawTextureData(new byte[width * height * 4]);
        else texture.LoadRawTextureData((IntPtr)pixels, width * height * 4);
        texture.Apply(updateMipmaps: false);
    }

    /// <summary>kRenAtlasRegion: an animated sprite's current frame.</summary>
    public void SetRegion(int x, int y, int width, int height, byte* pixels)
    {
        if (!textures.TryGetValue(0, out Texture2D? texture) || width <= 0 || height <= 0 || x < 0 || y < 0
            || x + width > texture.width || y + height > texture.height)
        {
            return;
        }
        // A small texture per sprite size, copied into the atlas on the GPU: re-uploading the whole
        // atlas for every animated sprite every frame would move hundreds of megabytes.
        if (!stamps.TryGetValue((width, height), out Texture2D? stamp))
        {
            stamp = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false) { hideFlags = HideFlags.HideAndDontSave };
            stamps[(width, height)] = stamp;
        }
        stamp.LoadRawTextureData((IntPtr)pixels, width * height * 4);
        stamp.Apply(updateMipmaps: false);
        Graphics.CopyTexture(stamp, 0, 0, 0, 0, width, height, texture, 0, 0, x, y);
        bool alphaChanged=false;
        if (alphas.TryGetValue(0, out var map))
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    int index=(y + row) * map.width + x + col;
                    byte value=pixels[(row * width + col) * 4 + 3];
                    if(map.alpha[index]!=value){map.alpha[index]=value;alphaChanged=true;}
                }
        PixelRevision++;
        if(alphaChanged){binaryAlphas.Remove(0);classifications.Clear();opaquePieces.Clear();Revision++;topologyRevisions[0]=TopologyRevision(0)+1;}
    }

    /// <summary>
    /// Looks at the texels a triangle covers (a grid of points across it, pulled slightly inwards so
    /// neighbouring sprites don't bleed in): all opaque is <see cref="Surface.Solid"/>, none is
    /// <see cref="Surface.Empty"/>, a mix is <see cref="Surface.Cutout"/>.
    /// </summary>
    public Surface Classify(uint id, float u0, float v0, float u1, float v1, float u2, float v2)
    {
        ulong key=id;
        key=(key^(uint)BitConverter.SingleToInt32Bits(u0))*1099511628211UL;
        key=(key^(uint)BitConverter.SingleToInt32Bits(v0))*1099511628211UL;
        key=(key^(uint)BitConverter.SingleToInt32Bits(u1))*1099511628211UL;
        key=(key^(uint)BitConverter.SingleToInt32Bits(v1))*1099511628211UL;
        key=(key^(uint)BitConverter.SingleToInt32Bits(u2))*1099511628211UL;
        key=(key^(uint)BitConverter.SingleToInt32Bits(v2))*1099511628211UL;
        if(classifications.TryGetValue(key,out var cached))return cached;
        if (!alphas.TryGetValue(id, out (int width, int height, byte[] alpha) map))
        {
            return Surface.Solid;
        }
        const int steps = 5;
        bool anyOpaque = false, anyClear = false;
        for (int i = 0; i <= steps; i++)
        {
            for (int j = 0; j <= steps - i; j++)
            {
                // Barycentric weights, shrunk towards the middle by a twentieth.
                float a = (i + 0.05f * steps) / (steps * 1.15f), b = (j + 0.05f * steps) / (steps * 1.15f), c = 1f - a - b;
                int x = Mathf.Clamp((int)((u0 * a + u1 * b + u2 * c) * map.width), 0, map.width - 1);
                int y = Mathf.Clamp((int)((v0 * a + v1 * b + v2 * c) * map.height), 0, map.height - 1);
                if (map.alpha[y * map.width + x] < 26)
                {
                    anyClear = true;
                }
                else
                {
                    anyOpaque = true;
                }
            }
        }
        var result=!anyOpaque ? Surface.Empty : anyClear ? Surface.Cutout : Surface.Solid;classifications[key]=result;return result;
    }

    /// <summary>The material for a texture and surface kind, culled or two-sided.</summary>
    public Material? Material(uint textureId, Surface surface, bool twoSided)
    {
        if (shader == null || surface == Surface.Empty)
        {
            return null;
        }
        ulong key = ((ulong)textureId << 8) | (ulong)surface | (twoSided ? 4ul : 0ul);
        if (materials.TryGetValue(key, out Material? material))
        {
            return material;
        }
        Shader selected=shader;
        if(shader.name=="Legacy Shaders/VertexLit"&&surface==Surface.Cutout) {
            selected=Shader.Find("Legacy Shaders/Transparent/Cutout/VertexLit")??Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse")??Shader.Find("Sprites/Default")??shader;
        } else if(shader.name=="Legacy Shaders/VertexLit"&&surface==Surface.Translucent)selected=Shader.Find("Sprites/Default")??shader;
        material = new Material(selected) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = false };
        Plugin.Log.LogInfo($"Guest texture material id={textureId}, surface={surface}, shader={selected.name}");
        textures.TryGetValue(textureId, out Texture2D? texture);
        material.SetTexture("_BaseMap", texture);
        material.mainTexture = texture;
        if(selected.name.StartsWith("Legacy Shaders/")){
            material.color=Color.white;material.SetColor("_Emission",new Color(.08f,.08f,.08f,1));material.SetColor("_SpecColor",Color.black);
            if(surface==Surface.Cutout)material.SetFloat("_Cutoff",.1f);
            material.renderQueue=(int)(surface==Surface.Solid?RenderQueue.Geometry:surface==Surface.Cutout?RenderQueue.AlphaTest:RenderQueue.Transparent);
            materials[key]=material;return material;
        }
        if (selected.name == "Sprites/Default") {
            material.color = Color.white;
            material.renderQueue = (int)RenderQueue.Transparent;
            materials[key] = material;
            return material;
        }
        material.SetColor("_BaseColor", new Color(brightness, brightness, brightness, 1f));
        material.SetFloat("_ColorMode", 0f); // multiply by vertex colour
        material.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_SpecularHighlights", 0f);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.SetColor("_SpecColor", Color.black);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_AlphaClip", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        if (surface == Surface.Solid)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
        }
        else
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", surface == Surface.Cutout ? 1f : 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            // Cutout before true translucents, so glass behind leaves still blends over them.
            material.renderQueue = (int)RenderQueue.Transparent - (surface == Surface.Cutout ? 50 : 0);
        }
        materials[key] = material;
        return material;
    }
}
