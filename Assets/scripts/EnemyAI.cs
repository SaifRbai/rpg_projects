using UnityEngine;

/// <summary>
/// IA des ennemis (squelettes, gobelins) : poursuit le joueur quand il est assez
/// proche, s'arrete devant les murs et frappe quand le joueur est a portee.
///
/// Tout se mesure aux PIEDS : le collider du joueur ne couvre que ses pieds alors que
/// son pivot est au milieu du sprite. En visant le pivot, l'ennemi s'arretait a cote
/// du torse du joueur, sans jamais toucher son collider -> il ne frappait jamais.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Deplacement")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f; // au-dela, le squelette ne bouge pas
    public float stopDistance = 0.8f; // repli si l'ennemi ou le joueur n'a pas de collider
    public LayerMask wallLayer;

    [Header("Combat")]
    public int touchDamage = 1;
    [Tooltip("Ecart max entre les colliders (pieds) pour frapper ; l'ennemi s'arrete a cette distance")]
    public float attackReach = 0.3f;
    public float damageCooldown = 1f; // temps minimum entre deux degats de contact

    [Header("Visuel")]
    public bool spriteFacesLeft = true; // a inverser si le squelette regarde du mauvais cote

    [Tooltip("Etats de l'Animator pendant lesquels l'ennemi ne bouge ni ne se retourne (attaque, mort)")]
    public string[] busyStates = { "Skeleton_Attack", "Skeleton_Attack_2", "Skeleton_Dying" };

    private Transform player;
    private Collider2D playerFeet;
    private PlayerStats playerStats;
    private Collider2D feet;
    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveDirection;
    private float nextDamageTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        feet = GetComponent<Collider2D>();

        // Kinematic : le squelette ne se fait pas pousser, on le deplace nous-memes
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerFeet = playerObj.GetComponent<Collider2D>();
            playerStats = playerObj.GetComponent<PlayerStats>();
        }
    }

    /// <summary>Vrai pendant l'attaque ou la mort : le squelette ne bouge ni ne se retourne.</summary>
    bool IsBusy()
    {
        if (animator == null) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string busy in busyStates)
            if (state.IsName(busy)) return true;
        return false;
    }

    void Update()
    {
        if (player == null || rb == null) return;

        Vector2 toPlayer = PlayerFeetPos() - FeetPos();
        float distance = toPlayer.magnitude;

        bool inReach = InReach(distance);
        moveDirection = (distance <= detectionRange && !inReach) ? toPlayer.normalized : Vector2.zero;

        if (!IsBusy()) FacePlayer();
        if (inReach) TryAttack();
    }

    Vector2 FeetPos() { return feet != null ? (Vector2)feet.bounds.center : (Vector2)transform.position; }
    Vector2 PlayerFeetPos() { return playerFeet != null ? (Vector2)playerFeet.bounds.center : (Vector2)player.position; }

    // ecart reel entre les deux colliders (negatif s'ils se chevauchent)
    bool InReach(float feetDistance)
    {
        if (feet == null || playerFeet == null || !feet.enabled) return feetDistance <= stopDistance;
        return feet.Distance(playerFeet).distance <= attackReach;
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

    void TryAttack()
    {
        if (playerStats == null || Time.time < nextDamageTime) return;

        playerStats.TakeDamage(touchDamage);
        nextDamageTime = Time.time + damageCooldown;

        if (animator != null) animator.SetTrigger("Attack");
    }

    // Le joueur peut aussi foncer dans l'ennemi : meme regle que la portee
    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) TryAttack();
    }
}
