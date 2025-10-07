using System;
using System.Collections.Generic;

namespace IntoTheDungeon.Core.ECS.Components
{
    public class ComponentBundle : IComponentBundle
    {
        private readonly List<Type> _componentTypes = new();
        private readonly List<Type> _managedComponentTypes = new();
        public ComponentBundle Add<T>() where T : struct, IComponentData
        {
            _componentTypes.Add(typeof(T));
            return this;
        }

        /// <summary>
        /// IManagedComponent 타입 추가
        /// </summary>
        public ComponentBundle AddManaged<T>() where T : class, IManagedComponent
        {
            _managedComponentTypes.Add(typeof(T));
            return this;
        }

        public Type[] GetComponentTypes()
        {
            return _componentTypes.ToArray();
        }

        public Type[] GetManagedComponentTypes()
        {
            return _managedComponentTypes.ToArray();
        }

    }

}