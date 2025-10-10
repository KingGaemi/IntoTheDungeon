using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;

using UnityEngine;
public sealed class SpriteViewBridge : EntityBehaviour
{
    public Sprite[] Catalog;                // index = SpriteId
    public Material DefaultMaterial;
    readonly Dictionary<Entity, GameObject> _go = new();

    void Update()
    {
        // 스폰/디스폰 처리는 ViewBridge와 동일하다고 가정

        foreach(var chunks in World.EntityManager.GetChunks(typeof(SpriteComponent)))
        {
            var entities = chunks.GetEntities();
            var sprites = chunks.GetComponentArray<SpriteComponent>();
            
            for(int i = 0; i < chunks.Count; i++)
            {
                var e = entities[i];
                ref var sp = ref sprites[i];
                if (!_go.TryGetValue(e, out var go)) continue;
                var r = go.GetComponent<SpriteRenderer>();
                var s = (sp.SpriteId >= 0 && sp.SpriteId < Catalog.Length) ? Catalog[sp.SpriteId] : null;
                if (r.sprite != s) r.sprite = s;

                r.sortingLayerID = sp.SortingLayer;
                r.sortingOrder   = sp.OrderInLayer;
                r.flipX = sp.FlipX; r.flipY = sp.FlipY;

                // Tint 적용
                byte a=(byte)(sp.TintRGBA>>24), rr=(byte)(sp.TintRGBA>>16), gg=(byte)(sp.TintRGBA>>8), bb=(byte)sp.TintRGBA;
                var col = new Color32(rr, gg, bb, a);
                if (r.sharedMaterial == null) r.sharedMaterial = DefaultMaterial;
                r.color = col;
            }

        }
        // 스프라이트 동기화


        foreach (var chunks in World.EntityManager.GetChunks(typeof(SpriteAnimComponent)))
        {
            var entities = chunks.GetEntities();
            var animComponents = chunks.GetComponentArray<SpriteAnimComponent>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var e = entities[i];
                ref var anim = ref animComponents[i];
                // 애니메이션: AnimId→프레임 테이블 조회 후 SpriteId 갱신

                // var newSpriteId = AnimDB.Sample(anim.AnimId, anim.Time);
                // ref var sp = ref World.EntityManager.GetComponent<SpriteComponent>(e);
                // if (sp.SpriteId != newSpriteId)
                // { sp.SpriteId = newSpriteId; World.EntityManager.SetComponent(e, sp); }
            }
        }
    }
}
