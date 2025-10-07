using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Status
{
    public class StatusModificationQueue : IManagedComponent, INeedInit
    {
        private readonly List<StatusModification> _modifications = new(8);

        public List<StatusModification> Modifications => _modifications;

        public void Enqueue(StatusModification mod)
        {
            Modifications.Add(mod);
        }

        public void Clear()
        {
            Modifications?.Clear();
        }

        public void Initialize()
        {
            Modifications.Add(StatusModification.Init());
        }

        public int Count => _modifications.Count;


    }
}