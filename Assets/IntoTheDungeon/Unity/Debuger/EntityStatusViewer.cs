using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Abstractions; // InformationComponent, TransformComponent 등이 있는 네임스페이스
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Unity.DebugTools
{
    public class EntityStatusViewer : MonoBehaviour
    {
        [Header("Entity Info")]
        public int EntityIndex = -1;
        public string EntityName;
        public string RecipeName;
        public int RecipeIdValue;

        [Header("Transform (Logic)")]
        public Vector2 LogicPosition;
        public Vector2 LogicDirection;

        [Header("Status")]
        public int CurrentHp;
        public int MaxHp;
        public bool IsAlive;
        public TeamFlag Team;

        [Header("State (Logic)")]
        public ActionState Action;
        public MovementState Movement;
        public ControlState Control;
        public Facing2D Facing;

        [Header("Animator (View)")]
        public string ControllerName;
        public string CurrentClip;
        public float NormalizedTime;
        public float AnimatorSpeed;
        public float StateSpeed;       // 상태 속도 × 속도 파라미터
        public bool InTransition;
        public string CurrentSprite;
        public bool FlipX;
        public float ParamAttackSpeed;
        public float ParamMovementSpeed;
        public bool ParamRun;
        public bool ParamAttacking;
        public bool ParamWindupDone;

        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private IWorld _world;
        private Entity _entity;
        private INameTable _nameTable;
        private IRecipeTable _recipeTable;
        private bool _isBound = false;

        // 뷰 브릿지가 뷰 마커를 바인딩할 때 이 함수를 호출해 엔진 레퍼런스를 넘겨주도록 합니다.
        public void BindEntity(IWorld world, Entity entity)
        {
            _world = world;
            _entity = entity;
            EntityIndex = entity.Index;
            world.TryGet(out _nameTable);
            world.TryGet(out _recipeTable);
            _animator = GetComponentInChildren<Animator>(true);
            _spriteRenderer = _animator ? _animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>(true);
            _isBound = true;
        }

        // 애니메이션 평가가 끝난 뒤 실제로 그려지는 값을 읽는다
        private void LateUpdate()
        {
            if (!_isBound) return;

            if (_spriteRenderer)
            {
                CurrentSprite = _spriteRenderer.sprite ? _spriteRenderer.sprite.name : "(none)";
                FlipX = _spriteRenderer.flipX;
            }

            if (!_animator) { ControllerName = "(Animator 없음)"; return; }
            ControllerName = _animator.runtimeAnimatorController ? _animator.runtimeAnimatorController.name : "(없음)";
            if (!_animator.runtimeAnimatorController || !_animator.isActiveAndEnabled) return;

            var st = _animator.GetCurrentAnimatorStateInfo(0);
            var clips = _animator.GetCurrentAnimatorClipInfo(0);
            CurrentClip = clips.Length > 0 && clips[0].clip ? clips[0].clip.name : "(없음)";
            NormalizedTime = st.normalizedTime;
            AnimatorSpeed = _animator.speed;
            StateSpeed = st.speed * st.speedMultiplier;
            InTransition = _animator.IsInTransition(0);

            ParamAttackSpeed = ReadFloat("AttackSpeed");
            ParamMovementSpeed = ReadFloat("MovementSpeed");
            ParamRun = ReadBool("Run");
            ParamAttacking = ReadBool("Attacking");
            ParamWindupDone = ReadBool("WindupDone");

            LogAnimChange(st.shortNameHash);
        }

        // 클립/상태/속도가 바뀌거나 재생이 멈추면 콘솔(=Editor.log)에 남긴다
        public static bool LogAnimation = true;
        string _lastClip;
        int _lastStateHash;
        float _lastSpeedProduct = -1f;
        float _lastT;
        float _stuckTime;
        bool _stuckLogged;

        void LogAnimChange(int stateHash)
        {
            if (!LogAnimation) return;
            float spd = AnimatorSpeed * StateSpeed;
            bool changed = CurrentClip != _lastClip || stateHash != _lastStateHash || !Mathf.Approximately(spd, _lastSpeedProduct);
            if (changed)
            {
                Debug.Log($"[Anim] {EntityName}_{EntityIndex} clip={CurrentClip} state#{stateHash} t={NormalizedTime:0.00} " +
                          $"spd={AnimatorSpeed:0.###}x{StateSpeed:0.###} sprite={CurrentSprite} flip={FlipX} " +
                          $"Run={ParamRun} Attacking={ParamAttacking} WindupDone={ParamWindupDone} " +
                          $"ECS={Action}/{Movement}/{Control}/{Facing}");
                _lastClip = CurrentClip; _lastStateHash = stateHash; _lastSpeedProduct = spd;
                _stuckTime = 0f; _stuckLogged = false;
            }

            // 반복하지 않는 클립이 끝난 경우(t>=1)는 정상 정지로 보고 제외
            if (Mathf.Approximately(NormalizedTime, _lastT) && NormalizedTime < 1f && !InTransition)
            {
                _stuckTime += Time.deltaTime;
                if (_stuckTime > 0.5f && !_stuckLogged)
                {
                    Debug.Log($"[Anim:멈춤] {EntityName}_{EntityIndex} clip={CurrentClip} t={NormalizedTime:0.00} " +
                              $"spd={AnimatorSpeed:0.###}x{StateSpeed:0.###} AttackSpeed={ParamAttackSpeed} MovementSpeed={ParamMovementSpeed}");
                    _stuckLogged = true;
                }
            }
            else _stuckTime = 0f;
            _lastT = NormalizedTime;
        }

        // 머리 위 디버그 표시: 인스펙터 없이 HP·상태·재생 중인 클립을 바로 확인
        public static bool ShowOverlay = true;
        static GUIStyle _style;
        int _lastHp = int.MinValue;

        private void OnGUI()
        {
            if (!ShowOverlay || !_isBound) return;
            // 지도·정비 화면이 덮고 있을 땐 머리 위 디버그 글자를 숨긴다(OnGUI는 UI보다 위에 그려짐)
            if (_world.TryGet(out IntoTheDungeon.Features.Battle.BattleProgress bp) && bp.Phase == IntoTheDungeon.Features.Battle.BattlePhase.Map) return;
            var cam = Camera.main;
            if (!cam) return;

            var anchor = _spriteRenderer ? _spriteRenderer.bounds : new Bounds(transform.position, Vector3.zero);
            var sp = cam.WorldToScreenPoint(new Vector3(anchor.center.x, anchor.max.y, 0f));
            if (sp.z < 0f) return;

            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, wordWrap = false };
            var text = $"{EntityName} [{Team}]  HP {CurrentHp}/{MaxHp}{(IsAlive ? "" : " 사망")}\n" +
                       $"{Action}/{Movement}  {Facing}\n" +
                       $"{CurrentClip}  t={NormalizedTime:0.00}  spd={AnimatorSpeed:0.##}×{StateSpeed:0.##}";
            var size = _style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y - 4f, size.x, size.y), text, _style);
        }

        void LogHpChange()
        {
            if (_lastHp != int.MinValue && CurrentHp != _lastHp)
                Debug.Log($"[피격] {EntityName}_{EntityIndex} HP {_lastHp} → {CurrentHp}/{MaxHp}  clip={CurrentClip}");
            _lastHp = CurrentHp;
        }

        float ReadFloat(string name)
        {
            foreach (var p in _animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Float) return _animator.GetFloat(name);
            return float.NaN;
        }

        bool ReadBool(string name)
        {
            foreach (var p in _animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Bool) return _animator.GetBool(name);
            return false;
        }

        private void FixedUpdate()
        {
            if (!_isBound || _world == null) return;

            var em = _world.EntityManager;

            // 1. 기본 정보 읽기
            if (em.TryGetComponent<InformationComponent>(_entity, out var info))
            {
                RecipeIdValue = info.RecipeId.Value;
                if (_nameTable != null) EntityName = _nameTable.GetName(info.NameId);
                if (_recipeTable != null) RecipeName = _recipeTable.GetName(info.RecipeId);
            }

            // 2. 로직 상의 트랜스폼 읽기
            if (em.TryGetComponent<TransformComponent>(_entity, out var trans))
            {
                LogicPosition = new Vector2(trans.Position.X, trans.Position.Y);
                LogicDirection = new Vector2(trans.Direction.X, trans.Direction.Y);
            }

            // 3. 체력/팀
            if (em.TryGetComponent<StatusComponent>(_entity, out var stats))
            {
                CurrentHp = stats.CurrentHp;
                MaxHp = stats.MaxHp;
                IsAlive = stats.IsAlive;
                LogHpChange();
            }
            if (em.TryGetComponent<TeamComponent>(_entity, out var team))
                Team = team.TeamFlag;

            // 4. 상태
            if (em.TryGetComponent<StateComponent>(_entity, out var state))
            {
                Action = state.Current.Action;
                Movement = state.Current.Movement;
                Control = state.Current.Control;
                Facing = state.Current.Facing;
            }
        }
    }
}