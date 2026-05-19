using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.ParticleSystem;
using UnityEngine.Rendering.Universal;

public class Shifter : MonoBehaviour
{
    // Public fields
    public GameObject linkedRipPrefab; 
    public GameObject deathParticles;
    [Tooltip("True if you want the shifter to instantiate portal first")]
    public bool waitsForAnimations = false;

    public float animationLength = 2.0f;
    public bool _isAngry = true;
    public float teleportCooldown = 5.0f;
    public float destoryTime = 5.0f; // Time to destroy the rip shifter created
    public float minExitDistance = 5.0f;  // Minimum distance from the player for the exit rip
    public float maxExitDistance = 10.0f; // Maximum distance from the player for the exit rip
    public bool canTeleport = true;

    private bool hasGoneThroughOwnPortal = false;

    // Private fields
    private bool isActive = false;
    private bool isChasing = false;
    private GameObject player;
    private Camera mainCamera;
    private Renderer shifterRenderer;
    private Rigidbody2D rb; 

    const float MOVE_FORCE = 50.0f;
    [SerializeField] private float maxSpeed = 2.5f;
    [SerializeField] private float maxPlayerChaseSpeed = 2.5f;
    [SerializeField] private float maxPortalChaseSpeed = 8f;
    [SerializeField] private float ripOffset = 3.5f;
    private PortalPath portalPath;
    private bool isFollowingPath = false;
    public GameObject targetPortal;
    private float pathFollowThreshold = 0.5f; // Distance threshold to consider portal reached
    //Paramters below are what forces the shifter to only teleport if it's part of it's "plan"
    public bool isTrackingPortals = false;  // Only true once actively chasing player
    private bool isOwnPortal = false;        // True for portals created by this shifter
    private LinkedRip currentEntranceRip;    // Track the current entrance rip this shifter created
    private LinkedRip currentExitRip;
    [Header("Dynamic Speed Settings")]
    public bool useDynamicSpeed = false;
    [SerializeField] private float minSpeed = 1.5f;  // Speed when close to player
    [SerializeField] private float minSpeedDistance = 5f;  // Distance at which to use minimum speed
    [SerializeField] private float maxSpeedDistance = 15f; // Distance at which to use maximum speed
    [SerializeField] private float xPositionThreshold = 1.5f; // How close the x position needs to be to consider "aligned"
    private float unreachableYThreshold = 2f; // Minimum y-distance to consider portal unreachable
    private float stuckCheckDuration = 1f; // How long to wait before considering the shifter stuck
    private float stuckTimer = 0f;
    private Vector2 lastPosition;
    private bool potentiallyStuck = false;
    private SpriteRenderer _spriteRenderer;
    public Animator animator;
    private Light2D shifterLight;

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        mainCamera = Camera.main;
        shifterRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody2D>();
        portalPath = PortalPath.Instance;
        shifterLight = GetComponentInChildren<Light2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        UpdateSortingOrder();
        Debug.Log("Has Gone through portal: " + hasGoneThroughOwnPortal);
        bool playerVisible = true;

        if (!playerVisible)
        {
            _isAngry = false;
            isTrackingPortals = false;
        }
        else
        {
            _isAngry = true;
            if (!isTrackingPortals && isChasing)
            {
                isTrackingPortals = true;  // Start tracking once we're angry and chasing
                CheckForPortalPath();      // Immediately check for available portals
            }
        }

        // Check if the Shifter is visible on the player's screen
        if (!isActive && IsVisibleOnScreen() && playerVisible)
        {
            isActive = true;
            PortalPath.Instance.ClearAllPaths();
            StartCoroutine(TeleportLoop());
        }

        // If chasing, move toward the player
        if (isChasing && _isAngry)
        {
            // Check portal path if not already following one
            if (!isFollowingPath)
            {
                CheckForPortalPath();
            }

            // If found portal go there, Otherwise chase the player
            if (isFollowingPath && targetPortal != null)
            {
                if (!CheckPortalEdgeCases())
                {
                    FollowPortalPath();
                }
                else
                {
                    Debug.Log("Clearing path");
                    targetPortal = null;
                    isFollowingPath = false;
                    portalPath.ClearPathForShifter(this);
                    ChasePlayer();
                }
            }
            else
            {
                ChasePlayer();
            }
        }
        UpdateAnimation();
        animator.SetBool("isChasing", isChasing);
    }

    void UpdateSortingOrder()
    {
        if (rb.velocity.y < -0.1f)
        {
            _spriteRenderer.sortingOrder = -5;
            if (shifterLight != null)
                shifterLight.enabled = false;
        }
        else
        {
            _spriteRenderer.sortingOrder = 5;
            if (shifterLight != null)
                shifterLight.enabled = true;
        }
    }

    //Returns false if we should keep chasing the portal, not the player. For Edge cases where player is closer or can't reach the portal
    private bool CheckPortalEdgeCases()
    {
        if (!hasGoneThroughOwnPortal)
        {
            return false;
        }
        float xDistance = Mathf.Abs(targetPortal.transform.position.x - transform.position.x);
        float yDistance = Mathf.Abs(targetPortal.transform.position.y - transform.position.y);
        if (Vector2.Distance(player.transform.position, transform.position) <
            Vector2.Distance(targetPortal.transform.position, transform.position))
        {
            Transform endPoint = targetPortal.GetComponent<LinkedRip>().EndRip.transform;
            if (endPoint != null)
            {
                if (Mathf.Abs(endPoint.position.x - player.transform.position.x) >
                    Mathf.Abs(transform.position.x - player.transform.position.x))
                {
                    //This means the end point is worse than current pos
                    if (player.transform.position.z.Equals(transform.position.z))
                    {
                        //Also should check that we're currently not below the player, because if we are, we should keep going through portal
                        if (player.transform.position.y <= transform.position.y + 0.2f)
                        {
                            Debug.Log("Clearing queue");
                            return true;
                        }
                    }
                }
            }
        }
        if (xDistance <= xPositionThreshold)
        {
            // Enter checks here
            if (!potentiallyStuck)
            {
                Debug.Log("checking if stuck");
                potentiallyStuck = true;
                lastPosition = transform.position;
                stuckTimer = 0f;
            }
            else
            {
                // Check if we've moved significantly
                if (Vector2.Distance(lastPosition, transform.position) < 0.5f)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer >= stuckCheckDuration)
                    {
                        Debug.Log($"[Shifter: {name}] Portal unreachable - X aligned but Y distance too great: {yDistance}");
                        potentiallyStuck = false;
                        return true;
                    }
                }
                else
                {
                    // Reset if we're moving
                    potentiallyStuck = false;
                    stuckTimer = 0f;
                }
            }
        }
        else
        {
            // Reset stuck detection if we're not x-aligned anymore
            potentiallyStuck = false;
            stuckTimer = 0f;
        }

        return false;
    }
    private bool IsVisibleOnScreen()
    {
        if (shifterRenderer.isVisible)
        {
            Vector3 screenPoint = mainCamera.WorldToViewportPoint(transform.position);
            return screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;
        }
        return false;
    }

    private IEnumerator TeleportLoop()
    {
        while (isActive)
        {
            if (canTeleport && _isAngry)
            {
                yield return new WaitForSeconds(1.0f);
                CreateAndUseRips();
                yield return new WaitForSeconds(teleportCooldown);
            }
            yield return null;
        }
    }
    //Goes down the queue of portals the player has been through
    private void FollowPortalPath()
    {
        if (targetPortal == null)
        {
            var nextPortals = portalPath.GetNextPortalPair(this);
            if (nextPortals.HasValue)
            {
                targetPortal = nextPortals.Value.entrance;
                isFollowingPath = true;
            }
            else
            {
                isFollowingPath = false;
                return;
            }
        }

        // move towards target portal if we're tracking
        if (isTrackingPortals)
        {
            maxSpeed = maxPortalChaseSpeed;
            Vector2 direction = (targetPortal.transform.position - transform.position).normalized;
            rb.velocity = new Vector2(direction.x * maxSpeed, rb.velocity.y);

            // Check if shifter's reached target portal
            if (Vector2.Distance(transform.position, targetPortal.transform.position) < pathFollowThreshold)
            {
                // Don't clear target until we actually go through the portal
                // The LinkedRip will handle that when we teleport
            }
        }
    }
    //Cleanup
    private void OnDestroy()
    {
        if (portalPath != null)
        {
            portalPath.ClearPathForShifter(this);
        }
    }
    private void CreateAndUseRips()
    {
        // Create entrance rip at Shifter's position
        _isAngry = false;
        canTeleport = false;
        var colliders = GetComponents<Collider2D>();
        if (waitsForAnimations)
        {
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }
            GetComponent<Rigidbody2D>().isKinematic = true;
        }
        int layer = gameObject.layer - 5; //enemyX - 5 = groundX
        Vector3 entranceOffset = new Vector3(_spriteRenderer.flipX ? ripOffset : -ripOffset, 0f, 0f);
        Vector3 entrancePosition = transform.position + entranceOffset;
        GameObject entranceRip = Instantiate(linkedRipPrefab, entrancePosition, Quaternion.identity);
        entranceRip.layer = layer;

        // Calculate a position near the player for the exit rip on the same Y-axis as Shifter
        Vector3 exitPosition = CalculateExitPosition();
        GameObject exitRip = Instantiate(linkedRipPrefab, exitPosition, Quaternion.identity);
        exitRip.layer = layer;

        LinkedRip entranceRipScript = entranceRip.GetComponent<LinkedRip>();
        LinkedRip exitRipScript = exitRip.GetComponent<LinkedRip>();

        entranceRipScript.EndRip = exitRip;
        exitRipScript.EndRip = entranceRip;
        isOwnPortal = true;
        currentEntranceRip = entranceRipScript;
        currentExitRip = exitRipScript;

        // Don't record own portals in the path system
        StartCoroutine(PlayAttackSequenceThenContinue(exitRip.transform.position));
        StartCoroutine(TeleportThroughRip(exitRip.transform.position));
        StartCoroutine(DestroyAfterDelay(entranceRipScript, exitRipScript, entranceRip, exitRip));

    }

    private Vector3 CalculateExitPosition()
    {
        // Randomize distance within range
        float exitDistance = Random.Range(minExitDistance, maxExitDistance);

        // Determine the direction to place the exit rip (left or right of the player)
        float direction = Random.value > 0.5f ? 1.0f : -1.0f;
        Debug.Log("value is " + direction);
        // Keep the exit rip on the same Y-axis as the Shifter
        Vector3 exitPosition = new Vector3(
            player.transform.position.x + direction * 5f, // Adjust X position
            player.transform.position.y + 4.3f, // Same Y as player
            transform.position.z // Maintain Z position
        );

        return exitPosition;
    }

    private IEnumerator TeleportThroughRip(Vector3 targetPosition)
    {
        canTeleport = false;
        isChasing = false;

        if (waitsForAnimations)
        {
            yield return new WaitForSeconds(animationLength);
        }

        yield return new WaitForSeconds(1.0f);

        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = true;
        }

        GetComponent<Rigidbody2D>().isKinematic = false;
        isChasing = true;
        canTeleport = true;

        // Check for next portal after teleporting
        CheckForPortalPath();
    }

    private void ChasePlayer()
    {
        if (!hasGoneThroughOwnPortal)
        {
            return;
        }
        Vector2 direction = new Vector2((player.transform.position.x - transform.position.x), 0).normalized;
        maxSpeed = maxPlayerChaseSpeed;
        if (useDynamicSpeed)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

            // Clamp distance between min and max distances
            float clampedDistance = Mathf.Clamp(distanceToPlayer, minSpeedDistance, maxSpeedDistance);

            // Calculate speed based on distance (lerp between min and max speed)
            float speedPercent = (clampedDistance - minSpeedDistance) / (maxSpeedDistance - minSpeedDistance);
            float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, speedPercent);

            rb.velocity = new Vector2(direction.x * currentSpeed, rb.velocity.y);

            Debug.Log($"[Shifter: {name}] Distance: {distanceToPlayer:F2}, Speed: {currentSpeed:F2}");
        }
        else
        {
            // Original behavior
            rb.velocity = new Vector2(direction.x * maxSpeed, rb.velocity.y);
        }
    }
    public bool ShouldUsePortal(LinkedRip portal)
    {
        // Use portal if:
        // 1. It's our own portal we just created
        // 2. We're tracking portals AND this portal is our current target
        bool shouldUse = false;
        if (isOwnPortal && !hasGoneThroughOwnPortal)
        {
            shouldUse = true;
        }
        else
        {
            shouldUse = (isTrackingPortals && targetPortal != null && portal.gameObject == targetPortal);
        }


        Debug.Log($"[Shifter: {name}] ShouldUsePortal check for {portal.name}: {shouldUse}");
        return shouldUse;
    }
    public void OnPortalUsed()
    {
        // Clear current portal target since we've used it
        targetPortal = null;
        hasGoneThroughOwnPortal = true;
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
        shifterRenderer.enabled = false;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        canTeleport = false;
        isChasing = false;

        StartCoroutine(ReappearAfterDelay(1.5f));

        // Look for next portal immediately
        CheckForPortalPath();

        Debug.Log($"[Shifter: {name}] Used portal, checking for next target");
    }
    private IEnumerator DestroyAfterDelay(LinkedRip entrance, LinkedRip exit, GameObject entranceRip, GameObject exitRip)
    {
        float timer = 0.0f;

        while (timer < destoryTime)
        {
            if (entrance.GetTeleportTime() > 0.0f || exit.GetTeleportTime() > 0.0f)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            timer += Time.deltaTime;
            yield return null;
        }
        //Ensures it won't auto teleport in rips anymore
        isOwnPortal = false;
        currentEntranceRip = null;
        currentExitRip = null;
        Destroy(entranceRip);
        Destroy(exitRip);
    }
    private void CheckForPortalPath()
    {
        Debug.Log($"[Shifter: {name}] Checking for portal path. isTrackingPortals: {isTrackingPortals}, isFollowingPath: {isFollowingPath}");

        // Don't check for paths if we're not tracking
        if (!isTrackingPortals) return;

        var nextPortals = portalPath.GetNextPortalPair(this);
        if (nextPortals.HasValue)
        {
            targetPortal = nextPortals.Value.entrance;
            isFollowingPath = true;
            Debug.Log($"[Shifter: {name}] Found new portal to follow: {targetPortal.name}");
        }
        else
        {
            isFollowingPath = false;
            Debug.Log($"[Shifter: {name}] No new portal pair available in queue");
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        LinkedRip entrance = collider.GetComponent<LinkedRip>();
        if (entrance != null)
        {
            return;
        }
        if (collider.gameObject.CompareTag("paintable"))
        {
            Vector2 moveDirection = new Vector2((transform.position - player.transform.position).x, 0);
            // Normalize the move direction to ensure consistent distance calculations
            moveDirection.Normalize();
            Collider2D paintableCollider = collider;
            Collider2D shifterCollider = GetComponent<Collider2D>();

            // Project the collider sizes onto the movement direction
            float projectedShifterSize = shifterCollider.bounds.extents.x;
            float projectedPaintableSize = paintableCollider.bounds.extents.x;

            // Calculate the distance needed to clear the paintable object
            float moveDistance = projectedShifterSize + projectedPaintableSize + 0.1f; // Add small buffer
            Vector2 newPosition = new Vector2(collider.transform.position.x, transform.position.y) + moveDirection * moveDistance;
            // Move the shifter to the new position
            transform.position = newPosition;
        }

        if (collider.gameObject.CompareTag("Waterfall"))
        {
            Die();
        }
    }

    private void Die()
    {
        rb.isKinematic = true;
        foreach (Collider2D collider2D in GetComponents<Collider2D>())
        {
            collider2D.enabled = false;
        }
        GetComponent<SpriteRenderer>().enabled = false;
        GameObject deathparticles = Instantiate(deathParticles, transform.position, Quaternion.identity);
        deathparticles.layer = gameObject.layer;
        Destroy(deathparticles, 5f);
        Destroy(gameObject,5f);
        
    }
    private IEnumerator ReappearAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        isChasing = true;
        rb.isKinematic = false;
        shifterRenderer.enabled = true;

        yield return new WaitForSeconds(0.4f);

        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = true;
        }
        canTeleport = true;
    }
    void UpdateAnimation()
    {
        if (player.transform.position.x - transform.position.x < 0)
        {
            _spriteRenderer.flipX = false;

        }
        else if (player.transform.position.x - transform.position.x > 0)
        {
            _spriteRenderer.flipX = true;

        }
    }

    private IEnumerator PlayAttackSequenceThenContinue(Vector3 targetPosition)
    {
        isChasing = false;
        rb.velocity = Vector2.zero;

        animator.Play("PreAttack");
        yield return WaitForAnimation("PreAttack");

        animator.Play("FullAttack");
        yield return WaitForAnimation("FullAttack");

        StartCoroutine(TeleportThroughRip(targetPosition));
    }
    private IEnumerator WaitForAnimation(string stateName)
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            yield return null;
    }
}

