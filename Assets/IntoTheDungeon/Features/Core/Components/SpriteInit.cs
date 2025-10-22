using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Unity.Catalogs;

namespace IntoTheDungeon.Features.Core
{
    public struct SpriteInit : ISpawnInit
    {
        public UnityEngine.Sprite Sprite;
        public bool FlipX, FlipY;
        public UnityEngine.Color Color;
        public float Scale;
        public void Apply(IWorld world, Entity e)
        {
            var em = world.EntityManager;

            // StatusComponent add/replace
            if (em.HasComponent<SpriteComponent>(e))
            {
                if (!world.TryGet(out ISpriteCatalog catalog) || Sprite == null) return;
                if (!catalog.TryGetId(Sprite, out var sid)) return;

                var sc = em.HasComponent<SpriteComponent>(e)
                    ? em.GetComponent<SpriteComponent>(e)
                    : default;

                sc.SpriteId = sid;
                sc.FlipX = FlipX;
                sc.FlipY = FlipY;
                sc.TintRGBA = ToARGB(Color);
                em.SetComponent(e, sc);
            }
        }
        public static uint ToARGB(UnityEngine.Color c)
        {
            byte a = (byte)Mathx.RoundToInt(c.a * 255f);
            byte r = (byte)Mathx.RoundToInt(c.r * 255f);
            byte g = (byte)Mathx.RoundToInt(c.g * 255f);
            byte b = (byte)Mathx.RoundToInt(c.b * 255f);
            return (uint)(a << 24 | r << 16 | g << 8 | b);
        }
    }
}