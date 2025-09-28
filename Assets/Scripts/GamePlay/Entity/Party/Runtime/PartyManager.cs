using System.Collections.Generic;
using MyGame.GamePlay.Entity.Abstractions;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    [SerializeField] private GameObject playerParty;
    [SerializeField] private List<GameObject> enemyPartys = new();

    [SerializeField] private Transform playerLocation;
    [SerializeField] private Transform enemyLocation;
    public void RegisterParty(GameObject party)
    {
        var core = party.GetComponent<PartyCore>();
        if (core.teamFlag == TeamFlag.Player)
        {
            if (playerParty) { Debug.LogError("playerParty is already exist"); return; }
            party.transform.position = playerLocation.position;

            playerParty = party;
        }
        else
        {
            party.transform.position = enemyLocation.position;
            
            enemyPartys.Add(party);
        }
    }
}

