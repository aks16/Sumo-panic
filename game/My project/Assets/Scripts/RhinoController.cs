/*using UnityEngine;

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

    /* void OnTriggerEnter(Collider other)
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

    void OnTriggerEnter(Collider other)
    {
        SumoAnimatedController sumo = other.GetComponent<SumoAnimatedController>();
        if (sumo == null) return;

        Rigidbody targetRb = other.attachedRigidbody;
        if (targetRb == null) return;

        Vector3 dir = transform.forward.normalized;
        Vector3 impulse = dir * pushForce + Vector3.up * verticalBoost;

        targetRb.AddForce(impulse, ForceMode.Impulse);
    }


}*/

using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class RhinoController : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 5f;

    public float pushForce = 20f;
    public float verticalBoost = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
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
        // on regarde sur le collider ou sur son parent (utile si le collider est un enfant du modèle 3D)
        SumoAnimatedController sumo = other.GetComponent<SumoAnimatedController>();
        if (sumo == null)
            sumo = other.GetComponentInParent<SumoAnimatedController>();

        if (sumo == null) return;

        Vector3 dir = transform.forward.normalized;
        Vector3 impulse = dir * pushForce + Vector3.up * verticalBoost;

        sumo.AddKnockback(impulse);
    }
}
