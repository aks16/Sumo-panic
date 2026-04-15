using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class RhinoSimpleController : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 10f;
    public float lifeTime = 5f;

    [Header("Poussée")]
    public float pushForce = 20f;
    public float verticalBoost = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // on le déplace à la main
        rb.useGravity = false;   // il reste à hauteur constante

        Collider col = GetComponent<Collider>();
        col.isTrigger = false;   // COLLISION NORMALE (pas Trigger)

        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        // Déplacement régulier pour de bonnes collisions
        rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("[RHINO] Collision avec : " + collision.gameObject.name);

        // Essayer de trouver le contrôleur de sumo sur l'objet touché ou ses parents
        SumoAnimatedController sumo = collision.gameObject.GetComponent<SumoAnimatedController>();
        if (sumo == null)
            sumo = collision.gameObject.GetComponentInParent<SumoAnimatedController>();

        if (sumo == null)
        {
            Debug.Log("[RHINO] Pas de SumoAnimatedController trouvé sur " + collision.gameObject.name);
            return;
        }

        // Direction de poussée = direction du rhino
        Vector3 dir = transform.forward.normalized;
        Vector3 impulse = dir * pushForce + Vector3.up * verticalBoost;

        Debug.Log("[RHINO] AddKnockback sur " + sumo.name + " impulse=" + impulse);
        sumo.AddKnockback(impulse);
    }
}