using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaintableObject : MonoBehaviour
{
    [SerializeField] private Color unpaintedColor = Color.black;
    [SerializeField] private float interactionRange = 3.0f;
    [SerializeField] private Material outlineMaterial;  // outline material reference
    [SerializeField] private Material defaultMaterial;  // original material without outline

    [SerializeField] public float respawnPointX; // where the object respawns when it falls into the waterfall or out of bounds
    [SerializeField] public float respawnPointY;
    [SerializeField] public float respawnPointZ;

    [SerializeField] public string respawnLayer;
    // [SerializeField] private ParticleSystem drippingEffect; // shows up / disappears when the player paints / unpaints the object
    private ParticleSystem[] paintEffects;
    private Color originalColor;
    private SpriteRenderer[] spriteRenderers;
    private List<Collider2D> allColliders = new List<Collider2D>();
    private List<bool> originalTriggerStates = new List<bool>();
    private bool isPlayerNearby = false;
    private GameObject player;
    private Collider2D triggerCollider;
    private Rigidbody2D rb;
    private PaintableObjectManager manager;
    private SpriteRenderer myRenderer;
    private Shader shaderGUItext;
    private Shader shaderSpritesDefault;

    private Inventory inventory;
    private PlayerInput playerInput;
    public bool isPaintable = true;
    public bool canPickUp = true;
    public bool canBeUnpaintedByEnemy = false;

    public bool isAffectedbyWater = true;

    private void Start()
    {

        myRenderer = gameObject.GetComponent<SpriteRenderer>();
        shaderGUItext = Shader.Find("GUI/Text Shader");
        shaderSpritesDefault = myRenderer.material.shader;

        manager = PaintableObjectManager.instance;
        if (manager == null)
        {
            Debug.LogError("PaintableObjectManager instance not found.");
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = false; // Disables physics 
        }

        player = GameObject.Find("Player");
        inventory = player.GetComponent<Inventory>();
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();
        GetAllColliders(this.transform);
        SetAllCollidersAsTriggers(true);

        // Ensure defaultMaterial is set
        if (defaultMaterial == null)
        {
            Debug.LogWarning("Default material not set! Using the first sprite renderer's material.");
            if (spriteRenderers.Length > 0)
            {
                defaultMaterial = spriteRenderers[0].material;
            }
            else
            {
                Debug.LogError("No SpriteRenderers found! Please assign a default material.");
            }
        }

        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                originalColor = renderer.color;
                renderer.color = unpaintedColor;
            }
        }


        paintEffects = GetComponentsInChildren<ParticleSystem>();

        if (paintEffects.Length > 0)
        {
            foreach (var effect in paintEffects)
            {
                effect.Stop();
            }
        }
    }

    private void GetAllColliders(Transform obj)
    {
        Collider2D[] colliders = obj.GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            allColliders.Add(collider);
            originalTriggerStates.Add(collider.isTrigger);
        }

        foreach (Transform child in obj)
        {
            GetAllColliders(child);
        }
    }

    private void SetAllCollidersAsTriggers(bool isTrigger)
    {
        foreach (var collider in allColliders)
        {
            collider.isTrigger = isTrigger;
        }
    }

    private void RevertCollidersToOriginalStates()
    {
        for (int i = 0; i < allColliders.Count; i++)
        {
            allColliders[i].isTrigger = originalTriggerStates[i]; // Revert to original state
        }
    }

    public void respawnPaintableObject()
    {
        //set layer to respawn layer
        if (respawnLayer != "")
        {
            gameObject.layer = LayerMask.NameToLayer(respawnLayer);
        }


        PaintableObjectManager manager = PaintableObjectManager.instance;
        manager.UpdatePaintedObjects(this.name, new Vector3(respawnPointX, respawnPointY, respawnPointZ));




    }

    public float GetDistanceFromPlayer()
    {
        Vector2 closestPoint = triggerCollider.ClosestPoint(player.transform.position);
        return Vector2.Distance(player.transform.position, closestPoint);
    }

    public bool GetIsPlayerNearby()
    {
        return GetDistanceFromPlayer() <= interactionRange;
    }

    private void Update()
    {
        //check if a paintable object fell out of bounds, we need to respawn it
        if (transform.position.y < -25)
        {
            respawnPaintableObject();
            inventory.PlaceDownObject(true, this);
        }


        PaintableObjectManager manager = PaintableObjectManager.instance;
        if (manager.nearestObject == this)
        {
            if (this.CompareTag("paintable") && !canPickUp)
            {
                ApplyOutline(false);
            }
            else if (GetIsPlayerNearby() && isPaintable)
            {
                ApplyOutline(true);  // Apply outline when player is nearby
            }
            else
            {
                ApplyOutline(false);  // Remove outline when player is too far
            }
        }
        else
        {
            ApplyOutline(false);  // Remove outline when player is too far
        }
    }

    private void ApplyOutline(bool apply)
    {
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                if (apply)
                {
                    renderer.material = outlineMaterial;  // Apply the outline material
                }
                else
                {
                    renderer.material = defaultMaterial;  // Revert to default material
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Waterfall") || (other.CompareTag("Water") && isAffectedbyWater))
        {
            //call placedownobject from inventory
            respawnPaintableObject();
            //inventory.PickupObject(gameObject);
            inventory.PlaceDownObject(true, this);

            /*manager.RemovePaintedObject(this.name);
            manager.RemovePaintableObject(this.name);
            Destroy(gameObject);*/
        }
    }


    public void Paint()
    {
        if (playerInput != null)
        {
            playerInput.enabled = true;
        }
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = originalColor;
            }

        }
        
        RevertCollidersToOriginalStates();
        if (isPaintable)
        {
            gameObject.tag = "paintable";
            PaintableObjectManager manager = PaintableObjectManager.instance;
            manager.AddPaintableObject(this);

            foreach (var effect in paintEffects)
            {
                effect.Play();
            }
        }
        else
        {
            foreach (var effect in paintEffects)
            {
                effect.Stop();
            }
        }

        if (rb != null)
        {
            rb.simulated = true;
        }
    }

    public void UnPaint()
    {
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = unpaintedColor;
            }
        }
        Debug.Log("unpaint called");
        SetAllCollidersAsTriggers(true);
        gameObject.tag = "BrokenObject";
        PaintableObjectManager manager = PaintableObjectManager.instance;
        foreach (var effect in paintEffects)
        {
            effect.Stop();
        }

        if (rb != null)
        {
            rb.simulated = false;
        }
    }
}
