using UnityEngine;

public class ArenaSection : MonoBehaviour
{
    [Header("Shake Settings")]
    public float shakeIntensity = 0.05f;
    public float shakeDuration = 2f;

    [Header("Fall Settings")]
    public float fallDelay = 0.5f;

    private Vector3 originalPosition;
    private bool isShaking = false;
    private bool hasFallen = false;
    private float shakeTimer = 0f;
    private Rigidbody rb;

    void Start()
    {
        originalPosition = transform.localPosition;

        // Ajoute un Rigidbody s'il n'existe pas
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // Ne bouge pas au debut
        rb.mass = 10f;
    }

    void Update()
    {
        if (isShaking && !hasFallen)
        {
            shakeTimer += Time.deltaTime;

            // Effet de tremblement
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity * 0.5f, shakeIntensity * 0.5f);
            float offsetZ = Random.Range(-shakeIntensity, shakeIntensity);
            transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, offsetZ);

            // Apres la duree de tremblement, la dalle tombe
            if (shakeTimer >= shakeDuration)
            {
                StartFalling();
            }
        }
    }

    // Appelle cette fonction pour declencher l'effondrement
    public void TriggerCollapse()
    {
        if (!isShaking && !hasFallen)
        {
            isShaking = true;
            shakeTimer = 0f;
            Debug.Log("Section " + gameObject.name + " commence a trembler!");
        }
    }

    void StartFalling()
    {
        hasFallen = true;
        isShaking = false;
        transform.localPosition = originalPosition; // Reset position avant de tomber

        // Active la physique pour que la dalle tombe
        rb.isKinematic = false;
        rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);

        // Ajoute une petite rotation pour un effet plus dramatique
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

        Debug.Log("Section " + gameObject.name + " tombe!");

        // Detruit la dalle apres 5 secondes
        Destroy(gameObject, 5f);
    }

    // Pour tester dans l'editeur
    [ContextMenu("Test Collapse")]
    void TestCollapse()
    {
        TriggerCollapse();
    }
}
