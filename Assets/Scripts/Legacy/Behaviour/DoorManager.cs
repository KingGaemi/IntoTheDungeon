#if false
using UnityEngine;
using System;

public class DoorManager : MonoBehaviour
{
    public GameObject stageGenerator;
    StageManager stageGeneration;
    void Start()
    {
        if(!stageGenerator)
            stageGeneration = FindFirstObjectByType<StageManager>();

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && stageGeneration)
        {
          
            stageGeneration.GenerateStage();
        }
    }
    void Update()
    {
        
    }
}
#endif