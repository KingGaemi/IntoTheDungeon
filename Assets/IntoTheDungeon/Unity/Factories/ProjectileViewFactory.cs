using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge;
using IntoTheDungeon.Unity.View;

namespace IntoTheDungeon.Unity.Factories
{
    public sealed class ProjectileViewFactory : IBehaviourFactory
    {
        public Component Attach(GameObject go, Entity entity, byte[] payload, IEntityManager em)
        {
            var view = go.GetComponent<ProjectileView>();
            if (view == null)
                view = go.AddComponent<ProjectileView>();

            view.Initialize(entity, em, payload);
            return view;
        }
    }

    
}