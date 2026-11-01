using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;
using IntoTheDungeon.Unity.Bridge.View;



namespace IntoTheDungeon.Features.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class CharacterAnimator : MonoBehaviour,
        IViewComponent,
        IStateEventListener,
        IStatusEventListener,
        IAnimationEventListener,
        IDamageEventListener,
        IRecipeBindable
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
        static readonly int HashHurt = Animator.StringToHash("Hurt");
        static readonly int HashPhaseProgress = Animator.StringToHash("PhaseProgress");
        static readonly int HashAttacking = Animator.StringToHash("Attacking");
        static readonly int HashWindup = Animator.StringToHash("Windup");
        static readonly int HashRecovery = Animator.StringToHash("Recovery");
        static readonly int HashWindupDone = Animator.StringToHash("WindupDone");

        // Animation Clip Lengths
        float _lenWindup;
        float _lenRecovery;

        // 필살기 모션: 컨트롤러의 대시 공격 상태를 그대로 쓴다(Attacking이 꺼지면 Idle로 빠짐)
        static readonly int HashSkillState = Animator.StringToHash("Dash_Attack");
        bool _inSkill;
        bool _stunned;   // 경직 중: 끊긴 평타의 단계 이벤트가 늦게 와도 공격 모션을 다시 켜지 않게

        // State Tracking
        MovementState _lastMovement;
        Facing2D _lastFacing;

        public IViewBridge ViewBridge { get; }

        void Reset()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!_sprite) _sprite = GetComponent<SpriteRenderer>();
        }

        void Awake()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!_sprite) _sprite = GetComponent<SpriteRenderer>();
        }


        // ============================================
        // Event Handlers 
        // ============================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void LogEvt(string msg)
        {
            if (IntoTheDungeon.Unity.DebugTools.EntityStatusViewer.LogAnimation)
                Debug.Log($"[AnimEvt] {name} {msg}");
        }

        public void OnStateChanged(in StateChangedEvent evt)
        {
            LogEvt($"State mask={evt.Mask} action={evt.ActionState} move={evt.MovementState} control={evt.ControlState} facing={evt.Facing}");
            // Movement 변경
            if (evt.HasMovement && _lastMovement != evt.MovementState)
            {
                _lastMovement = evt.MovementState;
                HandleMovement(evt.MovementState);
            }

            // Facing 변경
            if (evt.HasFacing)
            {
                _lastFacing = evt.Snapshot.Facing;
                HandleFacing(evt.Snapshot.Facing);
            }

            if (evt.HasAction)
            {
                if (evt.ActionState == ActionState.Skill) HandleSkillStart();
                else if (_inSkill) HandleSkillEnd();
            }

            if (evt.HasControl)
            {
                if (evt.ControlState == ControlState.Stunned) HandleStun();
                else if (_stunned) _stunned = false;
            }

            // Control 변경 (Death)
            if (evt.HasControl && evt.ControlState == ControlState.Dead)
            {
                HandleDeath();
            }

            // 공격 애니메이션 시작은 OnAnimationPhaseChanged(Windup)에서 한다.
            // 여기서 트리거를 켜면 전이 조건이 맞지 않을 때 트리거가 남아 다음 사이클에 유령 공격이 재생된다.
        }

        public void OnStatusChanged(in StatusChangedEvent evt)
        {
            LogEvt($"Status dirty={evt.Dirty} atkSpd={evt.AttackSpeed} movSpd={evt.MovementSpeed}");
            if (evt.Dirty == StatusDirty.All)
            {
                _animator.SetFloat(HashAttackSpeed, evt.AttackSpeed * _multiplier);
                _animator.SetFloat(HashMovementSpeed, evt.MovementSpeed * _multiplier);
                return;
            }
            if ((evt.Dirty & StatusDirty.AtkSpd) != 0)
            {
                _animator.SetFloat(HashAttackSpeed, evt.AttackSpeed * _multiplier);
            }

            if ((evt.Dirty & StatusDirty.MovSpd) != 0)
            {
                _animator.SetFloat(HashMovementSpeed, evt.MovementSpeed * _multiplier);
            }
        }

        public void OnDamaged(in DamagedEvent evt)
        {
            // 치명타면 Death 트리거(StateChanged)가 따로 오므로 Hurt는 건너뛴다
            LogEvt($"Damaged amount={evt.Amount} hp={evt.RemainingHp} animatorOk={(_animator && _animator.isActiveAndEnabled)} ctrl={(_animator && _animator.runtimeAnimatorController ? _animator.runtimeAnimatorController.name : "null")}");
            if (evt.RemainingHp <= 0) return;
            if (_inSkill) return;   // 필살기 모션은 맞아도 끊지 않는다
            // 공격 중에 경직 없는 공격을 맞으면 공격은 계속되므로 모션도 끊지 않는다(흔들림만). 경직이면 HandleStun이 피격 모션을 튼다
            if (_animator.GetBool(HashAttacking) && !_stunned) return;
            _animator.SetTrigger(HashHurt);
        }

        //  AnimationPhase 이벤트 처리
        public void OnAnimationPhaseChanged(in AnimationPhaseChangedEvent evt)
        {
            if (!_animator || !_animator.isActiveAndEnabled) return;
            if (_inSkill || _stunned) return;   // 필살기·경직이 평타를 끊은 같은 프레임의 평타 단계 이벤트는 무시

            float duration = Mathf.Max(0.001f, evt.PhaseDuration);

            switch (evt.Phase)
            {
                case ActionPhase.Windup:
                    _animator.SetBool(HashAttacking, true);
                    _animator.SetBool(HashWindupDone, false);
                    _animator.speed = (_lenWindup > 0.001f) ? _lenWindup / duration : 1f;
                    _animator.SetFloat(HashWindup, evt.PhaseProgress);
                    _animator.SetTrigger(HashAttack);   // 실제 Windup 시작 1회에만
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
                        _animator.ResetTrigger(HashAttack);
                        _animator.speed = 1f;
                    }
                    break;

                case ActionPhase.None:
                    _animator.SetBool(HashAttacking, false);
                    _animator.SetBool(HashWindupDone, false);
                    _animator.ResetTrigger(HashAttack);
                    _animator.speed = 1f;
                    break;
            }

            _animator.SetFloat(HashPhaseProgress, evt.WholeProgress);
            LogEvt($"Phase {evt.Phase} dur={evt.PhaseDuration:0.###} whole={evt.WholeProgress:0.00} lenW={_lenWindup:0.###} lenR={_lenRecovery:0.###} -> animator.speed={_animator.speed:0.###}");
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

        void HandleSkillStart()
        {
            _inSkill = true;
            // 평타를 끊고 들어올 수 있으므로 공격 트리거·속도를 정리하고 바로 재생
            _animator.ResetTrigger(HashAttack);
            _animator.ResetTrigger(HashHurt);
            _animator.SetBool(HashWindupDone, false);
            _animator.SetBool(HashAttacking, true);
            _animator.speed = 1f;
            _animator.Play(HashSkillState, 0, 0f);
        }

        void HandleStun()
        {
            _stunned = true;
            // 휘두르던 공격 모션을 접는다(피격 모션은 DamagedEvent의 Hurt 트리거가 튼다)
            _animator.ResetTrigger(HashAttack);
            _animator.SetBool(HashAttacking, false);
            _animator.SetBool(HashWindupDone, false);
            _animator.speed = 1f;
            _animator.SetTrigger(HashHurt);   // 피격 이벤트가 경직 전에 처리돼 건너뛰었어도 경직 모션은 보이게
        }

        void HandleSkillEnd()
        {
            _inSkill = false;
            _animator.SetBool(HashAttacking, false);
        }

        void HandleDeath()
        {
            _inSkill = false;
            _animator.SetTrigger(HashDeath);
        }

        // ============================================
        // Utilities
        // ============================================

        Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                1 => new Color(0.3f, 0.6f, 1f),
                2 => new Color(1f, 0.3f, 0.3f),
                _ => Color.white
            };
        }

        public void Bind(IViewRecipe recipe)
        {
            if (recipe is IAttackAnimatable atk)
            {
                _lenWindup = atk.AttackWindupClip ? atk.AttackWindupClip.length : 0f;
                _lenRecovery = atk.AttackRecoveryClip ? atk.AttackRecoveryClip.length : 0f;
            }
        }


    }
}