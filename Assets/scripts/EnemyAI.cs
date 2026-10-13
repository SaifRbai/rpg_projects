using UnityEngine;

/// <summary>
/// IA du squelette : poursuit le joueur quand il est assez proche, s'arrete
/// devant les murs et inflige des degats au contact.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Deplacement")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f; // au-dela, le squelette ne bouge pas
    public float stopDistance = 0.8f; // en deca, il s'arrete pour ne pas "rentrer" dans le joueur
    public LayerMask wallLayer;

    [Header("Combat")]
    public int touchDamage = 1;
    public float damageCooldown = 1f; // temps minimum entre deux degats de contact

    [Header("Visuel")]
    public bool spriteFacesLeft = true; // a inverser si le squelette regarde du mauvais cote

    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveDirection;
    private float nextDamageTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Kinematic : le squelette ne se fait pas pousser, on le deplace nous-memes
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    /// <summary>Vrai pendant l'attaque ou la mort : le squelette ne bouge ni ne se retourne.</summary>
    bool IsBusy()
    {
        if (animator == null) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Skeleton_Attack")
            || state.IsName("Skeleton_Attack_2")
            || state.IsName("Skeleton_Dying");
    }

    void Update()
    {
        if (player == null || rb == null) return;

        Vector2 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        moveDirection = (distance <= detectionRange && distance > stopDistance)
            ? toPlayer.normalized
            : Vector2.zero;

        if (!IsBusy()) FacePlayer();
    }

    // Le squelette est fait de plusieurs sprites : on le retourne via l'echelle X, pas via flipX.
    void FacePlayer()
    {
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.1f) return;

        // echelle positive quand le joueur est a droite (inverse si le sprite d'origine regarde a droite)
        bool positiveScale = dx > 0;
        if (!spriteFacesLeft) positiveScale = !positiveScale;

        Vector3 scale = transform.localScale;
        scale.x = positiveScale ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (IsBusy() || IsWallAhead())
        {
            SetSpeed(0f);
            return;
        }

        rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
        SetSpeed(moveDirection.magnitude);
    }

    // Un corps Kinematic traverse les colliders : on detecte les murs nous-memes par raycast.
    bool IsWallAhead()
    {
        if (moveDirection == Vector2.zero || wallLayer == 0) return false;
        return Physics2D.Raycast(rb.position, moveDirection, 0.5f, wallLayer).collider != null;
    }

    void SetSpeed(float speed)
    {
        if (animator != null) animator.SetFloat("Speed", speed);
    }

    // Appele tant que le joueur touche le squelette
    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player") || Time.time < nextDamageTime) return;

        PlayerStats stats = collision.gameObject.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.TakeDamage(touchDamage);
        nextDamageTime = Time.time + damageCooldown;

        if (animator != null) animator.SetTrigger("Attack");
    }
}
