using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Abstractions; // InformationComponent, TransformComponent 등이 있는 네임스페이스
// using IntoTheDungeon.Features.Character; // HealthComponent 등 스탯 컴포넌트 네임스페이스

namespace IntoTheDungeon.Unity.DebugTools
{
    public class EntityStatusViewer : MonoBehaviour
    {
        [Header("Entity Info")]
        public int EntityIndex = -1;
        public string EntityName;
        public int RecipeHash;

        [Header("Transform (Logic)")]
        public Vector2 LogicPosition;
        public Vector2 LogicDirection;

        // [Header("Status")]
        // public float CurrentHp;
        // public float MaxHp;

        private IWorld _world;
        private Entity _entity;
        private bool _isBound = false;

        // 뷰 브릿지가 뷰 마커를 바인딩할 때 이 함수를 호출해 엔진 레퍼런스를 넘겨주도록 합니다.
        public void BindEntity(IWorld world, Entity entity)
        {
            _world = world;
            _entity = entity;
            EntityIndex = entity.Index;
            _isBound = true;
        }

        private void FixedUpdate()
        {
            if (!_isBound || _world == null) return;

            var em = _world.EntityManager;

            // 1. 기본 정보 읽기
            if (em.TryGetComponent<InformationComponent>(_entity, out var info))
            {
                RecipeHash = info.RecipeId.Value;
                // 이름 테이블 서비스가 있다면 ID로 이름을 찾아 EntityName에 넣을 수도 있습니다.
            }

            // 2. 로직 상의 트랜스폼 읽기
            if (em.TryGetComponent<TransformComponent>(_entity, out var trans))
            {
                LogicPosition = new Vector2(trans.Position.X, trans.Position.Y);
                LogicDirection = new Vector2(trans.Direction.X, trans.Direction.Y);
            }

            // 3. 체력/스탯 등 프로젝트에 존재하는 컴포넌트 읽기 (주석 해제 후 사용)
            /*
            if (em.TryGetComponent<CharacterStatComponent>(_entity, out var stats))
            {
                CurrentHp = stats.Hp;
                MaxHp = stats.MaxHp;
            }
            */
        }
    }
}