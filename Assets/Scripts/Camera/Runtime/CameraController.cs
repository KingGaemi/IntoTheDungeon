using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Features.Battle;

/// <summary>
/// 2.5D 카메라: 살아 있는 파티원의 평균 바닥 위치(ECS)를 가로로 따라간다.
/// 세로는 방(WalkAreaAuthoring) 기준 높이에 고정해 던파처럼 바닥 띠가 화면 아래쪽에 오게 한다.
/// 그림이 아니라 로직 위치를 따라가므로 점프·피격 흔들림에 카메라가 흔들리지 않는다.
/// 전투 전 배치 중엔 배치 칸 격자 가운데에 고정한다(파티원을 끌 때마다 평균 위치가 바뀌어 화면이 흔들리지 않게).
/// </summary>
public class CameraController : MonoBehaviour, IWorldInjectable
{
    [Tooltip("클수록 빨리 따라간다(초당 남은 거리의 약 이 비율만큼 줄어듦)")]
    [SerializeField] float followSharpness = 8f;
    [Tooltip("방에 WalkAreaAuthoring이 없을 때 플레이어 바닥 위치에서 카메라 중심까지 높이")]
    [SerializeField] float fallbackHeight = 3f;

    IWorld _world;
    BattleProgress _progress;
    IWalkArea _walk;
    WalkAreaAuthoring _area;
    Camera _cam;
    bool _snapped;
    bool _hasGround;
    Vector2 _lastGround;

    public void Init(IWorld world)
    {
        _world = world;
        world.TryGet(out _progress);
        world.TryGet(out _walk);
        _area = FindFirstObjectByType<WalkAreaAuthoring>();
        _cam = GetComponent<Camera>();
        _snapped = false;
    }

    void LateUpdate()
    {
        if (_world == null) return;
        if (_progress != null && _progress.Phase == BattlePhase.Deploy && _walk != null)
        {
            var mid = Formation.MiddleOf(_walk);   // 배치 칸 격자 가운데(끄는 파티원과 상관없이 고정)
            _lastGround = new Vector2(mid.X, mid.Y);
        }
        else if (Party.TryGetCenter(_world.EntityManager, out var c, out _)) _lastGround = new Vector2(c.X, c.Y);   // 파티 전멸이면 마지막 위치에 머문다
        else if (!_hasGround) return;
        _hasGround = true;
        var ground = _lastGround;

        float targetX = ground.x;
        float targetY = _area ? _area.CameraY : ground.y + fallbackHeight;

        // 방 밖이 보이지 않게 가로 범위 제한(방이 화면보다 좁으면 가운데)
        if (_area && _cam && _cam.orthographic)
        {
            float halfW = _cam.orthographicSize * _cam.aspect;
            float min = _area.CameraMinX + halfW, max = _area.CameraMaxX - halfW;
            targetX = min <= max ? Mathf.Clamp(targetX, min, max) : (_area.CameraMinX + _area.CameraMaxX) * 0.5f;
        }

        var p = transform.position;
        if (!_snapped) { p.x = targetX; p.y = targetY; _snapped = true; }
        else
        {
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);   // 프레임레이트와 무관한 부드러운 추적
            p.x = Mathf.Lerp(p.x, targetX, k);
            p.y = Mathf.Lerp(p.y, targetY, k);
        }
        transform.position = p;
    }

}
