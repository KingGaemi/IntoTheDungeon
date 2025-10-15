using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public sealed class CharacterCoreBehaviour : MonoBehaviour, IAuthoringProvider
    {
        public int maxHp = 100;
        public float movementSpeed = 5f;
        public float attackSpeed = 5f;

        public RecipeId recipeId;

        public IAuthoring BuildAuthoring()
        {
            var root = GetComponentInParent<EntityRootBehaviour>();
            var t = root.transform;
            var rad = t.eulerAngles.z * Mathf.Deg2Rad;

            var rb = root.GetComponent<Rigidbody2D>(); // 여기서만 Mono 탐색
            var pos = (Vector2)t.position;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            return new CharacterCoreAuthoring
            {
                maxHp = maxHp,
                movementSpeed = movementSpeed,
                attackSpeed = attackSpeed,
                rb = rb,
                pos = pos,
                dir = dir,
                displayName = root.name
            };
        }
    }
}