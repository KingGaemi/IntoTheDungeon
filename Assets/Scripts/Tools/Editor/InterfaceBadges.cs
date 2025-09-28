using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

[InitializeOnLoad]
public static class InterfaceBadges {
    static readonly Regex isIface = new(@"(^|/)Abstractions/.*?/I[A-Z].*\.cs$", RegexOptions.Compiled);
    static InterfaceBadges() => EditorApplication.projectWindowItemOnGUI += OnGUI;

    static void OnGUI(string guid, Rect rect) {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (!path.EndsWith(".cs")) return;
        if (!isIface.IsMatch(path.Replace("\\","/"))) return;

        var r = new Rect(rect.xMax - 28, rect.y, 26, rect.height);
        var c = GUI.color; GUI.color = new Color(0, .7f, .9f);
        GUI.Label(r, "[I]", EditorStyles.miniBoldLabel);
        GUI.color = c;
    }
}
