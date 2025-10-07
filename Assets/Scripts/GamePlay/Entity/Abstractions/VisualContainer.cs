using UnityEngine;

public class VisualContainer : MonoBehaviour
{
    string _visualTag = "Visual_";

    void Awake()
    {
        UpdateName();
    }
    void Reset()
    {
        UpdateName();
    }

    private void UpdateName()
    {

        name = _visualTag + transform.root.name;

    }
}
