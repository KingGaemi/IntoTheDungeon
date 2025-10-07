using UnityEngine;
using IntoTheDungeon.Features.Core;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Runtime.World;

namespace IntoTheDungeon.Features.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator),
                    typeof(SpriteRenderer))]
    public class CharacterAnimator : EntityBehaviour
    {

        [SerializeField] Animator _animator;               // Visual의 Animator
        [SerializeField] SpriteRenderer sprite;
        EventNotifier notifier;
        AnimationSyncComponent animationSync;
        [SerializeField] float multiplier = 1.0f;           // 필요시 배수 / optional scale
        readonly string attackSpeedParam = "AttackSpeed";
        readonly string movementSpeedParam = "MovementSpeed";
        readonly string deathParam = "Death";
        readonly string attackParam = "Attack";
        readonly string runParam = "Run";
        readonly string phaseProgressParam = "PhaseProgress";
        readonly string attackingParam = "Attacking";
        readonly string attackBlendParam = "AttackBlend";



        private bool _hasAnimationSync;

        int hashAttacking;
        int hashAttackSpeed;
        int hashMovementSpeed;
        int hashDeath;
        int hashPhaseProgress;

        int hashAttack;
        int hashAttackBlend;
        int hashRun;



        void Start()
        {
        
            // 한 번만 체크
            _hasAnimationSync = EntityRoot.World?.EntityManager
                .HasComponent<AnimationSyncComponent>(EntityRoot.Entity) ?? false;
            
            if (!_hasAnimationSync)
            {
                Debug.LogWarning($"[{name}] AnimationSyncComponent not found. Animator disabled.");
                enabled = false;  // Update 비활성화
            }
        }
        void Reset()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!sprite) sprite = GetComponent<SpriteRenderer>();
            TryLinkNotifier();

        }

        override protected void OnAwake()
        {   
            if (!_animator) _animator = GetComponent<Animator>();
            if (!sprite) sprite = GetComponent<SpriteRenderer>();
            TryLinkNotifier();
            CacheAnimatorHashes();
        }

        void OnEnable()
        {
            if (notifier != null)
            {
                notifier.OnStateChanged += OnStateChanged;
                notifier.OnASChange += HandleAttackSpeedChanged;
                notifier.OnMSChange += HandleMovementSpeedChanged;
            }
            else
            {
                Debug.LogWarning("EventReceiver is null in OnEnable!", this);
            }

        }
        private void LateUpdate()  // System 이후 실행
        {

            ref var sync = ref EntityRoot.World.EntityManager.GetComponent<AnimationSyncComponent>(EntityRoot.Entity);

            if (sync.DirtyFlag == 1)
            {
                if (sync.CurrentPhase == ActionPhase.Startup)
                {

                    _animator.SetBool(hashAttacking, true);
                    _animator.SetTrigger(hashAttack);
                }else if (sync.CurrentPhase == ActionPhase.None)
                {
                    // Idle로 복귀
                    _animator.SetBool(hashAttacking, false);
                }
                // else if (sync.CurrentPhase == ActionPhase.None)
                // {
                //     // Idle로 복귀
                //     _animator.SetBool(hashAttacking, false);
                // }
                

            }
            
            // 매 프레임 진행률 업데이트 (부드러운 애니메이션)
            _animator.SetFloat(hashPhaseProgress, sync.WholeProgress);
        }
        private void OnDisable()
        {
            if (notifier != null)
            {
                notifier.OnStateChanged -= OnStateChanged;
                notifier.OnASChange -= HandleAttackSpeedChanged;
                notifier.OnMSChange -= HandleMovementSpeedChanged;
            }
        }

        private void OnStateChanged(StateSnapshot curr, ChangeMask mask)
        {
            // Action 변경 체크
            if ((mask & ChangeMask.Action) != 0)
            {
                HandleAction(curr.Action);
            }
            
            // Movement 변경 체크
            if ((mask & ChangeMask.Movement) != 0)
            {
                HandleMovement(curr.Movement);
            }
            
            // Facing 변경 체크
            if ((mask & ChangeMask.Facing) != 0)
            {
                HandleFacing(curr.Facing);
            }
        }
        private void TryLinkNotifier()
        {
            if (notifier != null) return;
            
            var root = GetComponentInParent<EntityRootBehaviour>(true);
            if (root != null && root.Notifier != null)
            {
                notifier = root.Notifier;
            }
        }

        private void CacheAnimatorHashes()
        {
            hashAttack = Animator.StringToHash(attackParam);
            hashRun = Animator.StringToHash(runParam);
            hashAttackSpeed = Animator.StringToHash(attackSpeedParam);
            hashMovementSpeed = Animator.StringToHash(movementSpeedParam);
            hashDeath = Animator.StringToHash(deathParam);
            hashPhaseProgress = Animator.StringToHash(phaseProgressParam);
            hashAttacking = Animator.StringToHash(attackingParam);
            hashAttackBlend = Animator.StringToHash(attackBlendParam);
        }

        private void HandleAction(ActionState action)
        {
            switch (action)
            {
                case ActionState.Attack:
                    // _animator.SetTrigger(hashAttack);
                    break;
                    
                case ActionState.Idle:
                    // 필요시 처리
                    break;
            }
        }

        private void HandleMovement(MovementState movement)
        {
            switch (movement)
            {
                case MovementState.Idle:
                    _animator.SetBool(hashRun, false);
                    break;
                
                case MovementState.Move:
                case MovementState.Walk:
                case MovementState.Run:
                    _animator.SetBool(hashRun, true);
                    break;
                
                case MovementState.Jump:
                    // Jump 처리
                    break;
            }
        }

        private void HandleFacing(Facing2D facing)
        {
            sprite.flipX = facing == Facing2D.Left;
        }

        void HandleAttackSpeedChanged(float spd)
        {
            if (_animator)
                _animator.SetFloat(hashAttackSpeed, spd * multiplier);
        }

        void HandleMovementSpeedChanged(float spd)
        {
            if (_animator)
                _animator.SetFloat(hashMovementSpeed, spd * multiplier);
        }

        void HandleDeath()
        {
            if (_animator)
                _animator.SetTrigger(hashDeath);
        }

    }
}
