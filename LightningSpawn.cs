using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public class LightningSpawn : MonoBehaviour
{
    [SerializeField] private float timeToSpawn = 4.5f; //4.5
    [SerializeField] private float timeToDestroy = 1.5f;
    [SerializeField] private float warningDuration = 1.0f;
    [SerializeField] private float blinkInterval = 0.2f;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private SpriteRenderer cloudSprite;

    public float startTime = 4.5f; //4.5
    public GameObject lightning;
    public Transform cloudTransform;
    private Color originalColor;
    private bool isWarningActive = false;
    private bool lightningActive = false;
    private Animator animator;

    [SerializeField] public const float SHAKE_INTENSITY = 0f;

    // SFX controller
    [SerializeField] private SFXController sfxController;

    private void Start()
    {
        animator = GetComponent<Animator>();
        originalColor = cloudSprite.color;
    }

    private void Update()
    {
        if (timeToSpawn <= warningDuration && !isWarningActive && !lightningActive)
        {
            TriggerWarningEffects();
            isWarningActive = true;
        }
        else if (timeToSpawn > warningDuration && !lightningActive)
        {
            StopWarningEffects();
            isWarningActive = false;
        }

        //Destroys the lightning bolt 1.5 seconds after spawning (or other if changed)
        //*Attach to lightning gameObject in cloud prefab
        if (timeToSpawn <= 0 && !lightningActive)
        {
            StartCoroutine(SpawnLightning());
            timeToSpawn = startTime;            
        }
        else
        {
            timeToSpawn -= Time.deltaTime;
        }

        if (lightningActive)
        {
            cloudSprite.color = warningColor;
        }
    }

    private void TriggerWarningEffects()
    {
        //StartCoroutine(Shake(warningDuration));
        StartCoroutine(BlinkWarning());
    }

    private void StopWarningEffects()
    {
        StopAllCoroutines();
        cloudSprite.color = originalColor;
    }

    private IEnumerator BlinkWarning()
    {
        bool sfxPlayed = false;
        
        while (timeToSpawn <= warningDuration && timeToSpawn > 0)
        {            
            // Play lightning sfx
            if (!sfxPlayed)
            {
                int index = GetLastNumberFromName(gameObject.name);
                sfxController.PlayLightningSFX(cloudTransform, index);
                sfxPlayed = true;
            }
                        
            cloudSprite.color = warningColor;
            yield return new WaitForSeconds(blinkInterval);
            cloudSprite.color = originalColor;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    private IEnumerator SpawnLightning()
    {
        lightningActive = true;

        GameObject spawnedLightning = Instantiate(lightning, cloudTransform.position, Quaternion.identity);
        spawnedLightning.layer = this.gameObject.layer;
        Destroy(spawnedLightning, timeToDestroy);

        yield return new WaitForSeconds(timeToDestroy);

        lightningActive = false;
    }

    /*private IEnumerator Shake(float time)
    {
        animator.enabled = false;
        while (time > 0.0f)
        {
            this.transform.position = new Vector3(this.transform.position.x + Mathf.Sin(Time.time * 100.0f) * SHAKE_INTENSITY, this.transform.position.y, this.transform.position.z);
            time -= Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
        animator.enabled = true;
    }
    */

    private int GetLastNumberFromName(string objectName)
    {
        Match match = Regex.Match(objectName, @"\d+$");
        if (match.Success)
        {
            return int.Parse(match.Value);
        }
        else
        {
            Debug.LogWarning("No number found at the end of the name.");
            return -1;
        }
    }
}