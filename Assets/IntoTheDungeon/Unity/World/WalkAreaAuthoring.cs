using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Gameplay;

namespace IntoTheDungeon.Unity.World
{
    /// <summary>
    /// 방(맵)에 붙여 2.5D 걸을 수 있는 영역과 카메라가 보여줄 가로 범위를 지정한다.
    /// 값은 이 오브젝트 위치 기준(로컬)이라 방을 옮겨도 같이 따라간다.
    /// Y는 깊이: 화면 y = 깊이(+높이)로 그리므로 Scene 뷰의 사각형이 곧 바닥 띠다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkAreaAuthoring : MonoBehaviour
    {
        [Header("걸을 수 있는 영역 (이 오브젝트 기준)")]
        [SerializeField] Vector2 xRange = new(-10f, 10f);
        [Tooltip("x = 가장 앞(화면 아래), y = 가장 안쪽(화면 위)")]
        [SerializeField] Vector2 depthRange = new(-1.5f, 1.5f);

        [Header("카메라")]
        [Tooltip("카메라가 보여줄 가로 범위. 이 밖은 화면에 나오지 않는다")]
        [SerializeField] Vector2 cameraXRange = new(-14f, 14f);
        [Tooltip("바닥 띠의 가장 앞(화면 아래)에서 카메라 중심까지 높이. 클수록 바닥이 화면 아래로 간다")]
        [SerializeField] float cameraHeightAboveFront = 3f;

        Vector3 Origin => transform.position;

        public float MinX => Origin.x + Mathf.Min(xRange.x, xRange.y);
        public float MaxX => Origin.x + Mathf.Max(xRange.x, xRange.y);
        public float MinDepth => Origin.y + Mathf.Min(depthRange.x, depthRange.y);
        public float MaxDepth => Origin.y + Mathf.Max(depthRange.x, depthRange.y);
        public float CameraMinX => Origin.x + Mathf.Min(cameraXRange.x, cameraXRange.y);
        public float CameraMaxX => Origin.x + Mathf.Max(cameraXRange.x, cameraXRange.y);
        public float CameraY => MinDepth + cameraHeightAboveFront;

        public IWalkArea ToWalkArea() => new RectWalkArea(MinX, MaxX, MinDepth, MaxDepth);

        void OnDrawGizmos()
        {
            // 걸을 수 있는 바닥 띠(초록), 카메라 가로 범위(노랑)
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            DrawRect(MinX, MaxX, MinDepth, MaxDepth);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            DrawRect(CameraMinX, CameraMaxX, CameraY - 0.05f, CameraY + 0.05f);
        }

        static void DrawRect(float x0, float x1, float y0, float y1)
        {
            Gizmos.DrawLine(new Vector3(x0, y0), new Vector3(x1, y0));
            Gizmos.DrawLine(new Vector3(x1, y0), new Vector3(x1, y1));
            Gizmos.DrawLine(new Vector3(x1, y1), new Vector3(x0, y1));
            Gizmos.DrawLine(new Vector3(x0, y1), new Vector3(x0, y0));
        }
    }
}
