using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Unity.View;
using IntoTheDungeon.Features.View;

namespace IntoTheDungeon.Features.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class CharacterAnimator : MonoBehaviour,
        IViewComponent,
        IStateEventListener,
        IStatusEventListener,
        IAnimationEventListener // 
    {
        public RecipeId recipeId;
        public short sortingLayerId;
        public short orderInLayer;


        [SerializeField] Animator _animator;
        [SerializeField] SpriteRenderer _sprite;
        [SerializeField] float _multiplier = 1f;




        // Animation Parameters
        static readonly int HashAttack = Animator.StringToHash("Attack");
        static readonly int HashRun = Animator.StringToHash("Run");
        static readonly int HashAttackSpeed = Animator.StringToHash("AttackSpeed");
        static readonly int HashMovementSpeed = Animator.StringToHash("MovementSpeed");
        static readonly int HashDeath = Animator.StringToHash("Death");
        static readonly int HashPhaseProgress = Animator.StringToHash("PhaseProgress");
        static readonly int HashAttacking = Animator.StringToHash("Attacking");
        static readonly int HashWindup = Animator.StringToHash("Windup");
        static readonly int HashRecovery = Animator.StringToHash("Recovery");
        static readonly int HashWindupDone = Animator.StringToHash("WindupDone");

        // Animation Clip Lengths
        float _lenWindup;
        float _lenRecovery;

        // State Tracking
        MovementState _lastMovement;
        Facing2D _lastFacing;

        public ViewBridge ViewBridge { get; }

        void Reset()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!_sprite) _sprite = GetComponent<SpriteRenderer>();
        }

        void Awake()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!_sprite) _sprite = GetComponent<SpriteRenderer>();
            CacheClipLengths();
        }


        // ============================================
        // Event Handlers 
        // ============================================

        public void OnStateChanged(in StateChangedEvent evt)
        {
            // Movement 변경
            if (evt.HasMovement && _lastMovement != evt.MovementState)
            {
                _lastMovement = evt.MovementState;
                HandleMovement(evt.MovementState);
            }

            // Facing 변경
            if (evt.HasFacing && _lastFacing != evt.Snapshot.Facing)
            {
                _lastFacing = evt.Snapshot.Facing;
                HandleFacing(evt.Snapshot.Facing);
            }

            // Control 변경 (Death)
            if (evt.HasControl && evt.ControlState == ControlState.Dead)
            {
                HandleDeath();
            }

            // Action 변경 (Attack)
            if (evt.HasAction && evt.ActionState == ActionState.Attack)
            {
                _animator.SetTrigger(HashAttack);
            }
        }

        public void OnStatusChanged(in StatusChangedEvent evt)
        {
            if ((evt.Dirty & StatusDirty.AtkSpd) != 0)
            {
                _animator.SetFloat(HashAttackSpeed, evt.AttackSpeed * _multiplier);
            }

            if ((evt.Dirty & StatusDirty.MovSpd) != 0)
            {
                _animator.SetFloat(HashMovementSpeed, evt.MovementSpeed * _multiplier);
            }
        }

        //  AnimationPhase 이벤트 처리
        public void OnAnimationPhaseChanged(in AnimationPhaseChangedEvent evt)
        {
            if (!_animator || !_animator.isActiveAndEnabled) return;

            float duration = Mathf.Max(0.001f, evt.PhaseDuration);

            switch (evt.Phase)
            {
                case ActionPhase.Windup:
                    _animator.SetBool(HashAttacking, true);
                    _animator.speed = (_lenWindup > 0.001f) ? _lenWindup / duration : 1f;
                    _animator.SetFloat(HashWindup, evt.PhaseProgress);
                    break;

                case ActionPhase.Recovery:
                    _animator.SetBool(HashWindupDone, true);
                    _animator.SetFloat(HashRecovery, evt.PhaseProgress);
                    _animator.speed = (_lenRecovery > 0.001f) ? _lenRecovery / duration : 1f;
                    break;

                case ActionPhase.Cooldown:
                    if (evt.WholeProgress >= 0.99f)
                    {
                        _animator.SetBool(HashAttacking, false);
                        _animator.SetBool(HashWindupDone, false);
                        _animator.speed = 1f;
                    }
                    break;

                case ActionPhase.None:
                    _animator.SetBool(HashAttacking, false);
                    _animator.SetBool(HashWindupDone, false);
                    _animator.speed = 1f;
                    break;
            }

            _animator.SetFloat(HashPhaseProgress, evt.WholeProgress);
        }

        // ============================================
        // State Handlers
        // ============================================



        void HandleMovement(MovementState movement)
        {
            bool isMoving = movement switch
            {
                MovementState.Move or MovementState.Walk or MovementState.Run => true,
                _ => false
            };

            _animator.SetBool(HashRun, isMoving);
        }

        void HandleFacing(Facing2D facing)
        {
            _sprite.flipX = facing == Facing2D.Left;
        }

        void HandleDeath()
        {
            _animator.SetTrigger(HashDeath);
        }

        // ============================================
        // Utilities
        // ============================================

        void CacheClipLengths()
        {
            if (_animator?.runtimeAnimatorController == null) return;

            var clips = _animator.runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
            {
                switch (clip.name)
                {
                    case "AttackWindup":
                        _lenWindup = clip.length;
                        break;
                    case "AttackRecovery":
                        _lenRecovery = clip.length;
                        break;
                }
            }
        }

        Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                1 => new Color(0.3f, 0.6f, 1f),
                2 => new Color(1f, 0.3f, 0.3f),
                _ => Color.white
            };
        }

    }
}