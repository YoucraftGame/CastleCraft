using System;
using System.Collections.Generic;
using PeakCraft.Link;
using UnityEngine;
namespace PeakCraft.Render;

/// <summary>One persistent model per captured entity so native sprites can sort between them.</summary>
internal sealed unsafe class CapturedScene
{
    private readonly Atlas atlas;
    private readonly Dictionary<uint,Group> groups=new();
    private readonly List<Group> used=new();
    private sealed class Group
    {
        public readonly CapturedModel model;
        public readonly List<Proto.RenBatch> batches=new();
        public Proto.RenBatch[] batchBuffer=Array.Empty<Proto.RenBatch>();
        public Proto.RenVertex[] vertices=Array.Empty<Proto.RenVertex>();
        public int count,order;
        public bool active;
        public Group(Atlas atlas){model=new CapturedModel(atlas,"scene");}
    }
    public CapturedScene(Atlas atlas){this.atlas=atlas;}
    public void Set(uint batchCount,uint vertexCount,Proto.RenBatch* batches,Proto.RenVertex* vertices,double originZ)
    {
        foreach(var group in groups.Values){group.active=false;group.count=0;group.batches.Clear();}
        used.Clear();
        for(uint b=0;b<batchCount;b++)
        {
            var batch=batches[b];
            if(batch.first>=vertexCount||batch.count<3)continue;
            int count=(int)Math.Min(batch.count,vertexCount-batch.first)/3*3;
            if(count==0)continue;
            uint key=batch.flags>>8;
            if(!groups.TryGetValue(key,out var group))
            {
                // The exporter bounds entity/block-entity count; also bound bad/old senders.
                if(groups.Count>=512)continue;
                group=new Group(atlas);groups.Add(key,group);
            }
            if(!group.active)
            {
                group.active=true;used.Add(group);
                var first=vertices[batch.first];
                double floor=(first.flags&256)!=0?(short)(first.flags>>16)/256.0:first.z;
                group.order=RenderSpace.SortingOrder(originZ+floor);
            }
            int needed=group.count+count;
            if(group.vertices.Length<needed)Array.Resize(ref group.vertices,Mathf.NextPowerOfTwo(needed));
            fixed(Proto.RenVertex* target=group.vertices)
                Buffer.MemoryCopy(vertices+batch.first,target+group.count,
                    (group.vertices.Length-group.count)*(long)sizeof(Proto.RenVertex),count*(long)sizeof(Proto.RenVertex));
            batch.first=(uint)group.count;batch.count=(uint)count;batch.flags&=255;
            group.batches.Add(batch);group.count=needed;
        }
        foreach(var group in used)
        {
            if(group.batchBuffer.Length<group.batches.Count)Array.Resize(ref group.batchBuffer,Mathf.NextPowerOfTwo(group.batches.Count));
            group.batches.CopyTo(group.batchBuffer);
            fixed(Proto.RenBatch* b=group.batchBuffer)fixed(Proto.RenVertex* v=group.vertices)
                group.model.Set((uint)group.batches.Count,(uint)group.count,b,v);
        }
        foreach(var group in groups.Values)if(!group.active)group.model.SetVisible(false);
    }
    public void Draw(Matrix4x4 matrix)
    {
        foreach(var group in groups.Values)if(group.active)group.model.Draw(matrix,sortingOrder:group.order);
    }
    public void SetVisible(bool visible)
    {
        foreach(var group in groups.Values)group.model.SetVisible(visible&&group.active);
    }
    public void Hide()
    {
        foreach(var group in groups.Values){group.active=false;group.model.Hide();}
    }
}
