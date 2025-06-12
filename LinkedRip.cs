using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinkedRip : MonoBehaviour
{
    public GameObject EndRip;
    //public PlayerMove playermove;
    const float TELEPORT_COOLDOWN = 0.5f;

    private bool canTeleport = true;
    private int currentLayer = 0;
    private LinkedRip EndRipLink;
    private LayerSwitching layerSwitching;
    private float TeleportTime = 0.0f;

    public bool playerInTrigger = false;
    public bool lowerTeleport = false;
    private bool enemyInTrigger = false;
    //This is for the shifters
    private PortalPath portalPath;

    private void Awake()
    {
        // duplicate the material. This is because the rip shader graph uses Object node, which doesn't work with batching. And it cannot be turned off for shader graphs. 
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer)
        {
            spriteRenderer.material = new Material(spriteRenderer.sharedMaterial);
        }
    }

    void Update()
    {
        if (TeleportTime != 0.0f)
        {
            TeleportTime -= Time.deltaTime;
        }
        /*
        if (playerInTrigger)
        {
            
            Debug.Log("1. counter is " + player.GetComponent<PlayerMove>().counter);
           
        }
        else
        {
            player.GetComponent<PlayerMove>().counter = false;

            Debug.Log("2. counter is " + player.GetComponent<PlayerMove>().counter);
        }
        */
    }


    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        
        layerSwitching = player.GetComponent<LayerSwitching>();
        EndRipLink = EndRip.GetComponent<LinkedRip>();
        SetCurrentLayerBasedOnLayer();
        portalPath = PortalPath.Instance;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy") && EndRip != null)
        {
            if (col.GetComponent<NoTeleport>() != null)
            {
                Debug.Log($"{col.name} has NoTeleport, skipping teleport.");
                return;
            }
            Shifter shifter = col.GetComponent<Shifter>();
            if (shifter != null)
            {
                if (shifter.ShouldUsePortal(this))
                {
                    enemyInTrigger = true;
                    TeleportEnemy(col.gameObject);
                    // Don't clear the target portal here - let the teleport handle it
                    Debug.Log($"[LinkedRip] Shifter {shifter.name} using portal");
                }
                else
                {
                    Debug.Log($"[LinkedRip] Shifter {shifter.name} ignored portal - not targeted or tracking");
                }
            }
            else
            {
                // Non-shifter enemy, just teleport
                enemyInTrigger = true;
                //Debug.Log("teleporting enemySSSSSS");
                TeleportEnemy(col.gameObject);
            }
        }

        if (col.CompareTag("Player") && EndRip != null)
        {
            playerInTrigger = true;
            GameObject player = GameObject.FindWithTag("Player");
            player.GetComponent<PlayerMove>().counter = true;
            player.GetComponent<PlayerMove>().OnCounterTriggered();
            Debug.Log("1. counter is " + player.GetComponent<PlayerMove>().counter);
            // Only record portal use for player teleports
        }
    }
    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Enemy") && EndRip != null)
        {
            enemyInTrigger = false;
            Debug.Log("Enemy exited trigger area.");
        }
        if (col.CompareTag("Player"))
        {
            playerInTrigger = false;
            GameObject player = GameObject.FindWithTag("Player");
            player.GetComponent<PlayerMove>().counter = false;
            Debug.Log("2. counter is " + player.GetComponent<PlayerMove>().counter);
            Debug.Log("Player exited trigger area.");
        }
    }


    //FOR PLAYER
    private void OnTeleportObject()
    {
        if (EndRip != null && playerInTrigger)
        {
            GameObject obj = GameObject.FindWithTag("Player");
            Rigidbody2D objRb = obj.GetComponent<Rigidbody2D>();
            Vector2 currentVelocity = objRb.velocity;

            // Record portal use for tracking shifters BEFORE teleporting
            Shifter[] shifters = FindObjectsOfType<Shifter>();
            foreach (Shifter shifter in shifters)
            {
                if (shifter.isTrackingPortals && shifter._isAngry)
                {
                    portalPath.RecordPortalUse(gameObject, EndRip);
                    Debug.Log($"[LinkedRip] Recorded portal use for tracking shifter {shifter.name}");
                    break; // Only need to record once
                }
            }

            // Handle actual teleport
            int layerDifference = EndRipLink.currentLayer - currentLayer;
            if (layerSwitching._switchCooldown <= 0.0f)
            {
                Vector3 targetPosition = EndRip.transform.position;
                if (lowerTeleport)
                {
                    targetPosition += Vector3.down * 0.5f; // make the teleport posisiton lower.
                    Debug.Log("lowered!");
                }

                StartCoroutine(layerSwitching.Switch(layerDifference, targetPosition));
                //StartCoroutine(layerSwitching.Switch(layerDifference, EndRip.transform.position));
                TeleportTime += 2.0f;
            }

            if (objRb != null)
            {
                objRb.velocity = currentVelocity;
            }
        }
    }

    public bool getPlayerInTrigger()
    {
        return playerInTrigger;
    }

    //FOR ENEMY
    private void TeleportEnemy(GameObject obj)
    {
        //Debug.Log("SSSSSSSSSSSSSSSSSSSSSSteleporting " + obj);
        if (EndRip != null && canTeleport && enemyInTrigger)
        {
            Shifter shifter = obj.GetComponent<Shifter>();
            Rigidbody2D objRb = obj.GetComponent<Rigidbody2D>();
            Vector2 currentVelocity = objRb.velocity;
            int layerDifference = EndRipLink.currentLayer - currentLayer;

            if (layerDifference != 0)
            {
                SetEnemyLayer(obj, layerDifference);
            }

            // If it's a shifter, handle portal path updates
            if (shifter != null)
            {
                shifter.OnPortalUsed(); // New method to handle post-teleport logic
            }

            StartCoroutine(layerSwitching.SwitchEnemy(obj, EndRip.transform.position, currentLayer, layerDifference));
            TeleportTime += 2.0f;

            if (objRb != null && shifter == null)
            {
                objRb.velocity = currentVelocity;
            }

            StartCoroutine(EndRipLink.TeleportCooldown());
        }
    }

    private void SetEnemyLayer(GameObject enemy, int layerDifference)
    {
        int newLayer = currentLayer + layerDifference;
        int newGroundLayer = LayerMask.NameToLayer("Enemy" + newLayer);
        enemy.layer = newGroundLayer;
        SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
        if ("Ground" + newLayer == "Ground0")
        {
            enemyRenderer.sortingOrder = 5;
        }
        else
        {
            enemyRenderer.sortingOrder = 0;
        }
    }

    public float GetTeleportTime()
    {
        return TeleportTime;
    }

    public IEnumerator TeleportCooldown()
    {
        canTeleport = false;
        yield return new WaitForSeconds(TELEPORT_COOLDOWN);
        canTeleport = true;
    }

    private void SetCurrentLayerBasedOnLayer()
    {
        switch (gameObject.layer)
        {
            case 6:
                currentLayer = 0;
                break;

            case 7:
                currentLayer = 1;
                break;

            case 8:
                currentLayer = 2;
                break;

            default:
                currentLayer = 0;
                break;
        }
    }
}
