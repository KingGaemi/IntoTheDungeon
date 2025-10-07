using System;
using System.Collections.Generic;

namespace IntoTheDungeon.Core.ECS.Entities
{
    public interface IArchetype
    {
        public IReadOnlyList<Type> ComponentTypes { get; }
        public IReadOnlyList<IChunk> Chunks { get; }
        
    }

}