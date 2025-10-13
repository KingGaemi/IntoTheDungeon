using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Unity.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ProjectileView : MonoBehaviour, IViewComponent
    {
        SpriteRenderer _renderer;
        Transform _transform;
        Entity _entity;
        IEntityManager _em;

        public ViewBridge ViewBridge { get; set; }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _transform = transform;
        }

        public void Initialize(Entity entity, IEntityManager em, byte[] payload)
        {
            _entity = entity;
            _em = em;

            if (payload != null && payload.Length >= 16)
            {
                // payload 파싱 예시: [color(4), scale(4), rotation(4), lifetime(4)]
                var color = new Color32(payload[0], payload[1], payload[2], payload[3]);
                var scale = System.BitConverter.ToSingle(payload, 4);
                var rotation = System.BitConverter.ToSingle(payload, 8);
                
                if (_renderer != null)
                    _renderer.color = color;
                
                _transform.localScale = Vector3.one * scale;
                _transform.rotation = Quaternion.Euler(0, 0, rotation);
            }
        }

        void LateUpdate()
        {
            if (!IsEntityValid()) return;

            // Position 동기화
            if (_em.TryGetComponent(_entity, out TransformComponent transform))
            {
                _transform.SetPositionAndRotation(new Vector3(transform.Position.X, transform.Position.Y, 0), Quaternion.Euler(0, 0, transform.Rotation));
            }


        }

        bool IsEntityValid()
        {
            return ViewBridge != null 
                && ViewBridge.IsEntityValid(_entity);
        }

        void OnDestroy()
        {
            _entity = default;
            _em = null;
        }
    }
}