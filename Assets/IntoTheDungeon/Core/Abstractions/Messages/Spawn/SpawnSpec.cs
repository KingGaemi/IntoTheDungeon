using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    public struct ViewOverride
    {
        public short SortingLayerId;
        public short OrderInLayer;
    }
    public struct SpawnSpec
    {
        public PhysicsHandle PhysHandle;
        public int SceneLinkId;
        public string Name;
        public Vec2 Pos, Dir;
        public List<ISpawnInit> Inits;
        public ViewOverride? ViewOverride;
    }
}