using UnityEngine;

public class EntityRoot : MonoBehaviour
{
    [SerializeField] VisualContainer visualContainer;
    [SerializeField] ScriptContainer scriptContainer;

    public VisualContainer Visual => visualContainer;
    
    public ScriptContainer Script => scriptContainer;
}
