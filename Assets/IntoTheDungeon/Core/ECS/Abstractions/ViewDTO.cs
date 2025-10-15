namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public enum ViewOpKind { None, Spawn, Despawn, SetTransform }

    // 고정 길이 헤더 + 확장 가능한 사양
    public struct ViewOp
    {
        public ViewOpKind Kind;
        public Entity Entity;
        public int DataIndex;
    }
    public struct SetTransformOp
    {
        public float X, Y, RotDeg;
        public SetTransformOp(float x, float y, float r) { X = x; Y = y; RotDeg = r; }
    }

    // 유니티가 해석할 스폰 사양
    public struct SpawnData
    {
        public int PrefabId;
        public short SortingLayerId;
        public short OrderInLayer;
        public BehaviourSpec[] Behaviours;

    }

    public struct TransformData
    {
        public float X, Y, RotDeg;
    }


    public struct BehaviourSpec
    {
        public int TypeId;
        public byte[] Payload;
    }
}
