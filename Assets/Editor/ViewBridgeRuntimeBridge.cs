#if UNITY_EDITOR
using IntoTheDungeon.Unity.Bridge;
using UnityEditor;

[InitializeOnLoad]
static class ViewBridgeRuntimeBridge
{
    public static ViewBridge Current { get; private set; }
    public static IntoTheDungeon.Core.Abstractions.World.IWorld World => Current?.World;

    static ViewBridgeRuntimeBridge()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        ViewBridge.AvailabilityChanged += OnAvail;
    }

    static void OnAvail(ViewBridge vb) => Current = vb;

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode)
            Current = null;
    }
}
#endif
