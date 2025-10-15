using UnityEngine;

public class VisualContainer : MonoBehaviour
{
    readonly string _visualTag = "Visual_";

    public void SetName(in string Name)
    {
        name = _visualTag + Name;
    }
}
