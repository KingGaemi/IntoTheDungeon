namespace IntoTheDungeon.Core.ECS.Entities
{
    internal struct EntityMetadata
    {
        public Chunk Chunk;           // Entity가 속한 Chunk
        public int IndexInChunk;      // Chunk 내 인덱스
        public int Version;           // Version 번호

        public bool IsAlive => Chunk != null;
    }
}
