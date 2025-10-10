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
        [SerializeField] float multiplier = 1.0f;           // 필요시 배수 / optional scale

        [SerializeField] int hashWindup, hashActive, hashRecovery;
        [SerializeField] float lenWindup, lenActive, lenRecovery; 
        readonly string attackSpeedParam = "AttackSpeed";
        readonly string movementSpeedParam = "MovementSpeed";
        readonly string deathParam = "Death";
        readonly string attackParam = "Attack";
        readonly string runParam = "Run";
        readonly string phaseProgressParam = "PhaseProgress";
        readonly string attackingParam = "Attacking";
        readonly string attackBlendParam = "AttackBlend";
        readonly string windupDoneParam = "WindupDone";
        // readonly string recoveryDoneParam = "RecoveryDone";
        // readonly string attackWindupParam =  "AttackWindup";
        // readonly string attackRecoveryParam = "AttackRecovery";
        readonly string windupParam = "Windup";
        readonly string recoveryParam = "Recovery";



        private bool _hasAnimationSync;

        int hashAttacking;
        int hashAttackSpeed;
        int hashMovementSpeed;
        int hashDeath;
        int hashPhaseProgress;
        int hashWindupDone;
        // int hashRecoveryDone;

        // int hashAttackWindup;
        // int hashAttackRecovery;


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
            
            var clips = _animator.runtimeAnimatorController.animationClips;
            foreach (var c in clips)
            {
                switch (c.name)
                {
                    case "AttackWindup": lenWindup = c.length; break;
                    case "AttackRecovery": lenRecovery = c.length; break;
                }
            }
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

            ref var sync = ref World.EntityManager.GetComponent<AnimationSyncComponent>(Entity);            
            if (sync.DirtyFlag == 1)
            {
                float duration = Mathf.Max(0.001f, sync.PhaseDuration);
                switch (sync.CurrentPhase)
                {
                    case ActionPhase.Windup:
                        _animator.SetTrigger(hashAttack);
                        _animator.SetBool(hashAttacking, true);
                        _animator.speed = (lenWindup > 0.001f) ? lenWindup / duration : 1f;
                        _animator.SetFloat(hashWindup, sync.PhaseProgress);
                        break;
                    case ActionPhase.Recovery:
                        // _animator.CrossFade(hashRecovery, 0f, 0, 0f);
                        _animator.SetBool(hashWindupDone, true);
                        _animator.SetFloat(hashRecovery, sync.PhaseProgress);
                        _animator.speed = (lenRecovery > 0.001f) ? lenRecovery / duration : 1f;
                        break;
                    case ActionPhase.Cooldown:
                        if(sync.WholeProgress>= 0.99f)
                        _animator.SetBool(hashAttacking, false);
                        _animator.SetBool(hashWindupDone, false);
                        _animator.speed = 1f;
                        break;
                    case ActionPhase.None:
                        _animator.SetBool(hashAttacking, false);
                        _animator.SetBool(hashWindupDone, false);
                        _animator.speed = 1f;
                        break;
                }
                sync.DirtyFlag = 0;
            }
            
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
            hashWindup = Animator.StringToHash(windupParam);
            hashRecovery = Animator.StringToHash(recoveryParam);
            hashWindupDone = Animator.StringToHash(windupDoneParam);
            // hashRecoveryDone = Animator.StringToHash(recoveryDoneParam);
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
