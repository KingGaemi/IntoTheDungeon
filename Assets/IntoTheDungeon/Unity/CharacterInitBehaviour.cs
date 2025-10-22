
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Core;
using UnityEngine;

namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public sealed class CharacterInitBehaviour : MonoBehaviour, ISpawnInitProvider
    {
        public int maxHp = 100;
        public float moveSpeed = 3.5f;
        public float attackSpeed = 1.0f;
        public Sprite idleSprite;
        public string skinToken;

        public IEnumerable<ISpawnInit> BuildInits()
        {
            yield return new CharacterStatsInit { MaxHp = maxHp, MoveSpeed = moveSpeed, AttackSpeed = attackSpeed };
            if (idleSprite) yield return new SpriteInit { Sprite = idleSprite };
        }
    }
}
