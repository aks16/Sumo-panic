using UnityEngine;

public class SumoAnimatedController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1f;
    public float rotationSpeed = 720f;
    public float pushForce = 15f;
    public float pushCooldown = 0.5f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayer;

    [Header("Test Mode (Clavier)")]
    public bool useKeyboardControls = true;

    // Components
    private Rigidbody rb;
    private Animator animator;
    private CapsuleCollider capsule;

    // State
    private Vector3 moveDirection;
    private bool canPush = true;
    private bool isFalling = false;
    private bool isGrounded = false;
    private float lastPushTime;

    // Input from server (for multiplayer)
    [HideInInspector] public float inputH = 0f;
    [HideInInspector] public float inputV = 0f;
    [HideInInspector] public bool inputPush = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        capsule = GetComponent<CapsuleCollider>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Réglages du Rigidbody pour bien gérer la gravité et le comportement
        rb.mass = 5f;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
        rb.useGravity = true;                                      // >>> GRAVITÉ ACTIVÉE <<<
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0, 1f, 0);
        }

        // Si aucun layer défini, on prend "Default"
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
        }
    }

    void Update()
    {
        // Récupération des inputs
        if (useKeyboardControls)
        {
            inputH = Input.GetAxis("Horizontal");
            inputV = Input.GetAxis("Vertical");
            inputPush = Input.GetKeyDown(KeyCode.Space);
        }

        // Gestion de la poussée
        if (inputPush && canPush && !isFalling)
        {
            StartPush();
        }

        // Check sol / chute
        CheckGrounded();
        CheckFallingByHeight();

        // Mise à jour de l'animator
        UpdateAnimator();

        // Test : effondrement aléatoire d'une tuile
        if (useKeyboardControls && Input.GetKeyDown(KeyCode.E))
        {
            TestCollapseTile();
        }
    }

    void FixedUpdate()
    {
        if (isFalling) return; // quand il tombe en dehors de l'arène, on laisse juste la gravité agir

        // Direction de déplacement
        moveDirection = new Vector3(inputH, 0, inputV).normalized;

        // Mouvement
        if (moveDirection.magnitude > 0.1f)
        {
            // Vitesse horizontale contrôlée
            Vector3 targetVelocity = moveDirection * moveSpeed;
            Vector3 currentVelocity = rb.linearVelocity;

            // On garde la composante verticale pour la gravité
            rb.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);

            // Rotation vers la direction du mouvement
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );
        }
        else
        {
            // Pas d'input → vitesse horizontale nulle, mais on garde la gravité
            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
        }
    }

    void CheckGrounded()
    {
        // On part du centre du capsule collider, raycast vers le bas
        Vector3 origin = transform.position + Vector3.up * (capsule.height * 0.5f);
        float rayDistance = (capsule.height * 0.5f) + groundCheckDistance;

        Ray ray = new Ray(origin, Vector3.down);
        if (Physics.SphereCast(ray, capsule.radius * 0.9f, rayDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    void CheckFallingByHeight()
    {
        // "Chute" en dehors de l'arène : en dessous d'un seuil
        if (transform.position.y < -5f && !isFalling)
        {
            isFalling = true;

            // Option : désactiver le collider pour qu'il tombe librement
            if (capsule != null) capsule.enabled = false;

            Debug.Log("[v0] Player eliminated: " + gameObject.name);
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = new Vector2(inputH, inputV).magnitude;
        animator.SetFloat("Speed", speed);

        animator.SetBool("IsFalling", isFalling || !isGrounded);
    }

    void StartPush()
    {
        canPush = false;
        lastPushTime = Time.time;

        if (animator != null)
        {
            animator.SetBool("IsPushing", true);
        }

        // Impulsion de poussée vers l'avant
        rb.AddForce(transform.forward * pushForce, ForceMode.Impulse);

        // Pousser les autres joueurs devant soi
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position + Vector3.up,
            1f,
            transform.forward,
            2f
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject != gameObject)
            {
                Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                if (otherRb != null)
                {
                    Vector3 pushDir = (hit.point - transform.position).normalized;
                    pushDir.y = 0.2f;
                    otherRb.AddForce(pushDir * pushForce * 2f, ForceMode.Impulse);
                }
            }
        }

        Invoke(nameof(ResetPush), 0.4f);
    }

    void ResetPush()
    {
        if (animator != null)
        {
            animator.SetBool("IsPushing", false);
        }

        Invoke(nameof(EnablePush), pushCooldown);
    }

    void EnablePush()
    {
        canPush = true;
    }

    void TestCollapseTile()
    {
        ArenaSection[] sections = FindObjectsByType<ArenaSection>(FindObjectsSortMode.None);
        if (sections.Length > 0)
        {
            int random = Random.Range(0, sections.Length);
            sections[random].TriggerCollapse();
        }
    }

    // Appelé par le serveur / RemotePlayersManager pour définir les inputs
    public void SetInput(float h, float v, bool push)
    {
        inputH = h;
        inputV = v;
        if (push && !inputPush) inputPush = true;
    }

    void LateUpdate()
    {
        inputPush = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 bounceDir = (transform.position - collision.transform.position).normalized;
            bounceDir.y = 0.1f;
            rb.AddForce(bounceDir * 5f, ForceMode.Impulse);
        }
    }
//<<<<<<< HEAD
//=======

    public void AddKnockback(Vector3 impulse)
    {
    if (rb == null)
        rb = GetComponent<Rigidbody>();

    // Applique l'impulsion directement au Rigidbody
    rb.AddForce(impulse, ForceMode.Impulse);
    }
//>>>>>>> 705b7c8 (bon)
}
