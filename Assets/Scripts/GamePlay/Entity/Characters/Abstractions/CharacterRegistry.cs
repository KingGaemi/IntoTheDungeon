using System.Collections.Generic;

namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public static class CharacterRegistry
    {
        static readonly List<CharacterCore> list = new();
        public static void Register(CharacterCore c) { if (!list.Contains(c)) list.Add(c); }
        public static void Unregister(CharacterCore c) { list.Remove(c); }
        public static IReadOnlyList<CharacterCore> All => list;
    }

}
