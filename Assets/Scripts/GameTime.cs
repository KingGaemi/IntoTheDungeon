using UnityEngine;

public class GameTime : MonoBehaviour
{

    public static float timeScale = 1.0f;


    public static float DeltaTime => Time.deltaTime * timeScale;

}
