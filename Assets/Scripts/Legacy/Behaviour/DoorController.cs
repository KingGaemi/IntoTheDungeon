using UnityEngine;

public enum DoorState { Closed, Open }

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;


    private SpriteRenderer _sr;
    public DoorState State { get; private set; } = DoorState.Closed;

    void Awake() { _sr = GetComponent<SpriteRenderer>();}

    public void SetState(DoorState s) { State = s; Refresh(); }
    public void Toggle() => SetState(State == DoorState.Open ? DoorState.Closed : DoorState.Open);

    void Refresh()
    {
        _sr.sprite = (State == DoorState.Open) ? openSprite : closedSprite;
    }
}
