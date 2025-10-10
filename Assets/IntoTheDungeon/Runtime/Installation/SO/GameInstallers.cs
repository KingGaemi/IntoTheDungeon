using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Runtime.Abstractions;
using IntoTheDungeon.World.Implementaion;

namespace IntoTheDungeon.Core.World.Installation
{
    [CreateAssetMenu(menuName = "IntoTheDungeon/GameInstallers")]
    public class GameInstallers : ScriptableObject
    {
        [SerializeReference] public List<IGameInstaller> installers = new();
        public void InstallAll(GameWorld world)
        {
            // installers.Add(new PhysicsInstaller());
            // installers.Add(new CharacterCoreInstaller());
            // foreach (var i in installers) i?.Install(world);
            
        }

    }
}