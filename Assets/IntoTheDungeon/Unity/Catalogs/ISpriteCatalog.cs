namespace IntoTheDungeon.Unity.Catalogs
{
    public interface ISpriteCatalog
    {
        bool TryGetId(UnityEngine.Sprite sprite, out int id);
    }
}