using System;
using System.Collections.Generic;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IArchetype
    {
        public IReadOnlyList<Type> ComponentTypes { get; }
        public IReadOnlyList<IChunk> Chunks { get; }
        
    }

}