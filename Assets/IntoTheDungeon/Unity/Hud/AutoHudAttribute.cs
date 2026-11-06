namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 붙이면 UnityBootstrapper가 BattleHud 오브젝트에 자동으로 AddComponent 한다(주입 전이라 IWorldInjectable도 받음).
    /// 새 HUD는 [AutoHud] public sealed class X : MonoBehaviour, IWorldInjectable 만으로 끝 — 부트스트래퍼 편집 금지.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class AutoHudAttribute : System.Attribute { }
}
