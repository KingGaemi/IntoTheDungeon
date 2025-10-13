namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public enum ViewOpKind { Spawn, Despawn }

    // 고정 길이 헤더 + 확장 가능한 사양
    public struct ViewOp
    {
        public ViewOpKind Kind;
        public Entity Entity;
        public ViewSpawnSpec Spawn; // Despawn 시엔 무시
    }

    // 유니티가 해석할 스폰 사양
    public struct ViewSpawnSpec
    {
        public int PrefabId;          // 프리팹 카탈로그 키
        public int SortingLayerId;    // 선택 사항
        public int OrderInLayer;      // 선택 사항
        public BehaviourSpec[] Behaviours; // 부착할 Behaviour들(옵션)
    }

    // 이름+바이너리 페이로드로 확장 가능
    public struct BehaviourSpec
    {
        public string TypeName;   // "IntoTheDungeon.View.ProjectileView" 등
        public byte[] Payload;    // 필요 시 옵션 데이터(없으면 null)
    }
}
