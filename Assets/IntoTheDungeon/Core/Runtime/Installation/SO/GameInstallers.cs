using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Core.Runtime.Installation.Installers;

namespace IntoTheDungeon.Core.Runtime.Installation
{
    [CreateAssetMenu(menuName = "IntoTheDungeon/GameInstallers")]
    public class GameInstallers : ScriptableObject
    {
        [SerializeReference] public List<IGameInstaller> installers = new();
        public void InstallAll(World.GameWorld world)
        {
            // installers.Add(new PhysicsInstaller());
            // installers.Add(new CharacterCoreInstaller());
            // foreach (var i in installers) i?.Install(world);
            
        }

    }
}