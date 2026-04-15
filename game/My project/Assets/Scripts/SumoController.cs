using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SumoController : MonoBehaviour
{
    [Header("Déplacement")]
    public float moveSpeed = 5f;
    public float inputDeadZone = 0.2f;

    [Header("Poussée")]
    public float pushForce = 10f;
    public float pushCooldown = 0.5f;

    [Header("Knockback")]
    public float knockbackDamping = 5f; // plus grand = le knockback se dissipe plus vite

    private Rigidbody rb;
    private Vector3 moveInput;
    private float nextPushTime;

    // Inputs reçus du manager (RemotePlayersManager)
    private float inputH;
    private float inputV;
    private bool inputPushHeld;

    // Vitesse ajoutée par des chocs externes (rhino, explosion, etc.)
    private Vector3 knockbackVelocity = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Appelé par RemotePlayersManager pour mettre à jour les inputs de CE sumo
    public void SetInput(float h, float v, bool pushHeld)
    {
        inputH = h;
        inputV = v;
        inputPushHeld = pushHeld;
    }

    // Appelé par des événements externes (rhino, piège, etc.)
    // impulse = quantité de mouvement (comme un AddForce en mode Impulse)
    public void AddKnockback(Vector3 impulse)
    {
        // Convertir l'impulsion en une vitesse initiale
        knockbackVelocity += impulse / rb.mass;
    }

    void Update()
    {
        Vector3 rawInput = new Vector3(inputH, 0f, inputV);
        float magnitude = rawInput.magnitude;

        if (magnitude < inputDeadZone)
        {
            moveInput = Vector3.zero;
        }
        else
        {
            moveInput = rawInput.normalized;
        }

        if (moveInput.sqrMagnitude > 0.01f)
        {
            transform.forward = moveInput;
        }

        // Poussée avec cooldown
        if (inputPushHeld && Time.time >= nextPushTime)
        {
            rb.AddForce(transform.forward * pushForce, ForceMode.Impulse);
            nextPushTime = Time.time + pushCooldown;
        }
    }

    void FixedUpdate()
    {
        // Vitesse de déplacement contrôlée (input)
        Vector3 baseVelocity = moveInput * moveSpeed;

        // On ajoute la composante de knockback
        Vector3 totalVelocity = baseVelocity + knockbackVelocity;

        // Garder la composante verticale actuelle (gravité, sauts...)
        totalVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = totalVelocity;

        // Dissiper progressivement le knockback
        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            knockbackVelocity = Vector3.Lerp(
                knockbackVelocity,
                Vector3.zero,
                knockbackDamping * Time.fixedDeltaTime
            );
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }
    }
}