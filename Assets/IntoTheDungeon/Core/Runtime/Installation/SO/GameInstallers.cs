using UnityEngine;
using System.Collections.Generic;

namespace IntoTheDungeon.Core.Runtime.Installation
{
    [CreateAssetMenu(menuName = "MyGame/GameInstallers")]
    public class GameInstallers : ScriptableObject
    {
        [SerializeReference] public List<IGameInstaller> installers = new();
        public void Run(World.GameWorld world, Runner.SystemRunner runner)
        {
            foreach (var i in installers) i?.Install(world, runner);
        }
    }
}