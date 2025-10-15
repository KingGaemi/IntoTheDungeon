using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Entities
{
    public sealed class ViewOpQueue : IViewOpQueue
    {
        readonly ViewOp[] _buffer;
        int _count;

        public ViewOpQueue(int capacity = 512)
        {
            _buffer = new ViewOp[capacity];
            _count = 0;
        }

        public void EnqueueSpawn(Entity e, in SpawnData data)
        {
            if (_count >= _buffer.Length) return;
            int idx = ViewDataStores.Spawn.Count;
            ViewDataStores.Spawn.Add(data);
            _buffer[_count++] = new ViewOp { Kind = ViewOpKind.Spawn, Entity = e, DataIndex = idx };
        }

        public void EnqueueSetTransform(Entity e, in TransformData transformData)
        {
            if (_count >= _buffer.Length) return;
            int idx = ViewDataStores.Xform.Count;
            ViewDataStores.Xform.Add(transformData);
            _buffer[_count++] = new ViewOp { Kind = ViewOpKind.SetTransform, Entity = e, DataIndex = idx };
        }

        public void EnqueueDespawn(Entity e)
        {
            if (_count >= _buffer.Length) return;
            int idx = ViewDataStores.Despawn.Count;
            _buffer[_count++] = new ViewOp { Kind = ViewOpKind.Despawn, Entity = e, DataIndex = idx };
        }

        public int Drain(ViewOp[] sink, ref int count)
        {
            int n = Math.Min(_count, sink.Length - count);
            Array.Copy(_buffer, 0, sink, count, n);
            count += n;
            _count = 0;
            return n;
        }

        public void Enqueue(Entity e, in TransformData transformData)
        {
            EnqueueSetTransform(e, transformData);


        }
        public void Enqueue(Entity e)
        {

            EnqueueDespawn(e);



        }
        public void Enqueue(Entity e, in SpawnData spawnData)
        {
            EnqueueSpawn(e, spawnData);
        }


    }
}