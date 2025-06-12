using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaultyBoat : MonoBehaviour
{
    const float SHAKE_TIME = 2.0f;
    const float SHAKE_INTENSITY = 0.15f;
    const float REQUIRED_TIME_IN_TRIGGER = 1.0f;

    private GameObject Player;
    private Animator boatAnimator;
    private float timeInTrigger = 0.0f;

    private void Start()
    {
        Player = GameObject.Find("Player");
        boatAnimator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player") || col.CompareTag("Enemy"))
        {
            StartCoroutine(Shake(SHAKE_TIME));
        }
    }

    private void OnTriggerStay2D(Collider2D col)
    {
        if (col.CompareTag("Player") || col.CompareTag("Enemy"))
        {
            timeInTrigger += Time.deltaTime;

            if (timeInTrigger >= REQUIRED_TIME_IN_TRIGGER)
            {
                boatAnimator.SetTrigger("BoatDrop");
                timeInTrigger = 0.0f;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Player") || col.CompareTag("Enemy"))
        {
            timeInTrigger = 0.0f;
        }
    }


    public IEnumerator Shake(float time)
    {
        while (time > 0.0f)
        {
            this.transform.position = new Vector3(this.transform.position.x + Mathf.Sin(Time.time * 100.0f) * SHAKE_INTENSITY, this.transform.position.y, this.transform.position.z);
            time -= Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
    }
}
