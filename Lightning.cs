using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lightning : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private GameObject Player;

    //consts
    private const float ENEMY_ALPHA = 0.5f;
    private const float STUN_DURA = 3.0f;
    private const float SHAKE_DURA = 1.0f;

    private void Start()
    {
        Player = GameObject.Find("Player");
        playerHealth = Player.GetComponent<PlayerHealth>();
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            PlayerCol playerCol = col.GetComponent<PlayerCol>();
            playerCol.GetHitByLightning(this.gameObject);
            
        }
        
    }
}