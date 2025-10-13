using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Unity.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterView : MonoBehaviour, IViewComponent
    {
        static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
        static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        static readonly int HashVelocityY = Animator.StringToHash("VelocityY");
        static readonly int HashAttack = Animator.StringToHash("Attack");
        static readonly int HashHit = Animator.StringToHash("Hit");
        static readonly int HashDeath = Animator.StringToHash("Death");

        const float FlipThreshold = 0.01f;

        SpriteRenderer _renderer;
        Animator _animator;
        Transform _transform;
        Entity _entity;
        IEntityManager _em;

        bool _facingRight = true;
        Vector2 _lastVelocity;

        public ViewBridge ViewBridge { get; set; }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _transform = transform;
        }

        public void Initialize(Entity entity, IEntityManager em, byte[] payload)
        {
            _entity = entity;
            _em = em;

            if (payload != null && payload.Length >= 8)
            {
                // payload 파싱 예시: [teamId(4), level(4)]
                int teamId = System.BitConverter.ToInt32(payload, 0);
                int level = System.BitConverter.ToInt32(payload, 4);
                
                // 팀 색상 적용
                if (_renderer != null && teamId > 0)
                {
                    _renderer.color = GetTeamColor(teamId);
                }
            }
        }

        void LateUpdate()
        {
            if (!IsEntityValid()) return;

            SyncTransform();
            SyncAnimation();
        }

        void SyncTransform()
        {
            if (_em.TryGetComponent(_entity, out TransformComponent transform))
            {
                _transform.position = new Vector3(transform.Position.X, transform.Position.Y, 0);
            }
        }

        void SyncAnimation()
        {
            if (!_animator.isActiveAndEnabled) return;

            // Velocity 기반 애니메이션
            if (_em.TryGetComponent(_entity, out KinematicComponent vel))
            {
                var velocity = new Vector2(vel.Velocity.X, vel.Velocity.Y);
                float speed = velocity.magnitude;

                _animator.SetBool(HashIsMoving, speed > FlipThreshold);
                _animator.SetFloat(HashVelocityX, velocity.x);
                _animator.SetFloat(HashVelocityY, velocity.y);

                // Flip 처리
                if (Mathf.Abs(velocity.x) > FlipThreshold)
                {
                    bool shouldFaceRight = velocity.x > 0;
                    if (_facingRight != shouldFaceRight)
                    {
                        _facingRight = shouldFaceRight;
                        Vector3 scale = _transform.localScale;
                        scale.x = _facingRight ? 1 : -1;
                        _transform.localScale = scale;
                    }
                }

                _lastVelocity = velocity;
            }

            // State 기반 트리거
            if (_em.TryGetComponent(_entity, out StateComponent state))
            {
                switch (state.Current.Action)
                {
                    case ActionState.Attack:
                        _animator.SetTrigger(HashAttack);
                        break;
                    case ActionState.Hit:
                        _animator.SetTrigger(HashHit);
                        break;
                    case ActionState.Death:
                        _animator.SetTrigger(HashDeath);
                        break;
                }
            }
        }

        bool IsEntityValid()
        {
            return ViewBridge != null 
                && ViewBridge.IsEntityValid(_entity);
        }

        Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                1 => new Color(0.3f, 0.6f, 1f),    // 파란 팀
                2 => new Color(1f, 0.3f, 0.3f),    // 빨간 팀
                _ => Color.white
            };
        }

        void OnDestroy()
        {
            _entity = default;
            _em = null;
        }
    }
}