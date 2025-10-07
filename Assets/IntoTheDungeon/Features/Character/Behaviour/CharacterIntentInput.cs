using UnityEngine;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Core;
using IntoTheDungeon.Core.Runtime.World;

[RequireComponent(typeof(CharacterCore))]
public class CharacterIntentInput : EntityBehaviour
{
    // [SerializeField] float moveMagnitude = 1f;

    [Header("Wiring")]
    [SerializeField] CharacterCore core;

    CharacterIntent _currentIntent = CharacterIntent.None;



    void Awake()
    {
        if (!core) core = GetComponent<CharacterCore>();
    }

    void Update()
    {
        // 1) 이동 입력: WASD/화살표
        var x = Input.GetAxisRaw("Horizontal");

        CharacterIntent next = CharacterIntent.None;
        if (x != 0)
        {
            var facing = ToFacing(x);
            next = CharacterIntent.Move(facing);
        }
        



        // 3) 공격: J
        if (Input.GetKeyDown(KeyCode.Space))
        {
            next = CharacterIntent.Attack();
        }
       

        if (_currentIntent.Equals(next)) return;
        else
        {
            core.AddIntent(next);
            _currentIntent = next;
        } 

    }

    // 입력을 사용 중인 Facing2D로 변환
    Facing2D ToFacing(float x)
    {
        return x < 0 ? Facing2D.Left : Facing2D.Right;
    }

}
