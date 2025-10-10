namespace IntoTheDungeon.Core.Runtime.ECS
{
    public class EntityRegistry
    {
        static int _nextId = 1;

        public int getId(){ return _nextId++; }
    }

}