using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge;
using IntoTheDungeon.Unity.View;

namespace IntoTheDungeon.Unity.Factories
{
    public sealed class CharacterViewFactory : IBehaviourFactory
    {
        public Component Attach(GameObject go, Entity entity, byte[] payload, IEntityManager em)
        {
            var view = go.GetComponent<CharacterView>();
            if (view == null)
                view = go.AddComponent<CharacterView>();

            view.Initialize(entity, em, payload);
            return view;
        }
    }
}