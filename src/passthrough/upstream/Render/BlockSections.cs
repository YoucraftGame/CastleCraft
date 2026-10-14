using System.Collections.Generic;
using PeakCraft.Link;
using PeakCraft.World;
using UnityEngine;
using UnityEngine.Rendering;
namespace PeakCraft.Render;

/// <summary>Persistent meshes split by floor-depth row, including a separate ground-fluid pass.</summary>
internal sealed unsafe class BlockSections
{
    private readonly Atlas atlas;
    private readonly Dictionary<(int,int,int),Section> sections=new();
    private GameObject? root;
    private sealed class Section {public readonly Row?[] rows=new Row?[32];}
    private sealed class Row
    {
        public readonly GameObject drawable;
        public readonly Mesh mesh;
        public readonly MeshRenderer renderer;
        public readonly List<Vector3> positions=new();
        public readonly List<Vector2> uvs=new();
        public readonly List<Color32> colours=new();
        public readonly List<int> solid=new(),cutout=new(),blended=new();
        public readonly List<Material> materials=new(3);
        public Row(Transform parent)
        {
            drawable=new GameObject("CastleCraft block depth row"){hideFlags=HideFlags.HideAndDontSave};drawable.transform.SetParent(parent,false);
            mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
            drawable.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=drawable.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        }
        public void Reset(){positions.Clear();uvs.Clear();colours.Clear();solid.Clear();cutout.Clear();blended.Clear();materials.Clear();}
        public void Destroy(){Object.Destroy(mesh);Object.Destroy(drawable);}
    }
    public BlockSections(Atlas atlas){this.atlas=atlas;}
    public int Count=>sections.Count;
    public void SetVisible(bool visible){if(root!=null&&root.activeSelf!=visible)root.SetActive(visible);}
    public void Clear(){foreach(var section in sections.Values)Remove(section);sections.Clear();}
    private static void Remove(Section section){foreach(var row in section.rows)row?.Destroy();}
    public void SetSection(in Proto.RenSectionHdr header,Proto.RenVertex* vertices)
    {
        var key=(header.sx,header.sy,header.sz);
        int count=(int)header.vertexCount/3*3;
        if(count==0||!atlas.Usable)
        {
            if(sections.TryGetValue(key,out var old)){Remove(old);sections.Remove(key);}return;
        }
        if(root==null){root=new GameObject("CastleCraft block sections"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(root);}
        if(!sections.TryGetValue(key,out var section)){section=new Section();sections.Add(key,section);}
        foreach(var row in section.rows)row?.Reset();
        for(int i=0;i<count;i+=3)
        {
            var triangle=vertices+i;
            var surface=(triangle->flags&2)!=0?Surface.Translucent:atlas.Classify(0,triangle[0].u,triangle[0].v,triangle[1].u,triangle[1].v,triangle[2].u,triangle[2].v);
            if(surface==Surface.Empty)continue;
            // Horizontal fluid surfaces belong below bodies occupying that floor.
            // Solid blocks still occlude a body standing behind the block's front.
            int depth=Mathf.Clamp(Mathf.FloorToInt((triangle[0].z+triangle[1].z+triangle[2].z)/3f-.0001f),0,15);
            bool fluid=(triangle->flags&512)!=0;
            int index=depth+(fluid?16:0);
            var row=section.rows[index]??(section.rows[index]=new Row(root.transform));
            int first=row.positions.Count;
            for(int k=0;k<3;k++)
            {
                var v=triangle[k];uint color=v.color;
                row.positions.Add(RenderSpace.Relative(v.x,v.y,v.z));row.uvs.Add(new Vector2(v.u,v.v));
                row.colours.Add(new Color32((byte)color,(byte)(color>>8),(byte)(color>>16),(byte)(color>>24)));
            }
            var indices=surface==Surface.Solid?row.solid:surface==Surface.Cutout?row.cutout:row.blended;
            indices.Add(first);indices.Add(first+2);indices.Add(first+1);
        }
        var origin=RenderSpace.ToWorld(header.sx*16,header.sy*16,header.sz*16);
        for(int index=0;index<section.rows.Length;index++)
        {
            var row=section.rows[index];if(row==null)continue;
            if(row.positions.Count==0){row.drawable.SetActive(false);continue;}
            var mesh=row.mesh;mesh.Clear();mesh.SetVertices(row.positions);mesh.SetUVs(0,row.uvs);mesh.SetColors(row.colours);
            mesh.subMeshCount=(row.solid.Count>0?1:0)+(row.cutout.Count>0?1:0)+(row.blended.Count>0?1:0);
            int sub=0;
            void Add(List<int> indices,Surface surface){if(indices.Count==0)return;mesh.SetTriangles(indices,sub++,false);row.materials.Add(atlas.Material(0,surface,surface!=Surface.Solid)!);}
            Add(row.solid,Surface.Solid);Add(row.cutout,Surface.Cutout);Add(row.blended,Surface.Translucent);
            mesh.RecalculateNormals();mesh.RecalculateBounds();row.renderer.sharedMaterials=row.materials.ToArray();
            row.drawable.layer=Coords.GeometryLayer;row.drawable.transform.position=origin;
            row.renderer.sortingLayerID=Coords.SortingLayer;
            row.renderer.sortingOrder=RenderSpace.SortingOrder(header.sz*16+(index%16)+(index>=16?-.05:.5),1);
            row.drawable.SetActive(true);
        }
    }
}
