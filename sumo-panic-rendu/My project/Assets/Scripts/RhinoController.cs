using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class RhinoController : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 5f;

    // Force appliquée au sumo (en tant qu'impulsion)
    public float pushForce = 20f;
    public float verticalBoost = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        Collider col = GetComponent<Collider>();
        col.isTrigger = true; // on traverse les sumos
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        SumoController sumo = other.GetComponent<SumoController>();
        if (sumo == null) return;

        // Direction principale = direction du rhino
        Vector3 dir = transform.forward.normalized;

        // Impulsion : forte composante horizontale + un peu vers le haut
        Vector3 impulse = dir * pushForce + Vector3.up * verticalBoost;

        // On passe l'impulsion au sumo
        sumo.AddKnockback(impulse);
    }
}