using UnityEngine;

using MyGame.GamePlay.Entity.Characters.Abstractions;
using MyGame.GamePlay.Entity.Abstractions;

[RequireComponent(typeof(CharacterCore))]
public class CharacterIntentInput : MonoBehaviour
{
    // [SerializeField] float moveMagnitude = 1f;

    [Header("Wiring")]
    [SerializeField] CharacterCore core;



    void Awake()
    {
        if (!core) core = GetComponent<CharacterCore>();
    }

    void Update()
    {
        // 1) 이동 입력: WASD/화살표
        var x = Input.GetAxisRaw("Horizontal");

        if (x != 0)
        {
            var facing = ToFacing(x);
            var it = CharacterIntent.Move(facing);
            core.ApplyIntent(in it);

        }
        else
        {
            {
                var it = CharacterIntent.Stop();
                core.ApplyIntent(in it);
            }

        }



        // 3) 공격: J
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var it = CharacterIntent.Attack(/*target*/ null);
            core.ApplyIntent(in it);
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            var it = CharacterIntent.Stop();
            core.ApplyIntent(in it);
        }

    }

    // 입력을 사용 중인 Facing2D로 변환
    Facing2D ToFacing(float x)
    {
        return x < 0 ? Facing2D.Left : Facing2D.Right;
    }
}
