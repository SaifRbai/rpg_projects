using System.Collections;
using UnityEngine;

/// <summary>
/// IA du Chevalier Noir (boss de Room3).
///  - poursuit le joueur quand il entre dans la zone de detection
///  - au corps a corps : coup d'epee annonce (le joueur a le temps de reculer)
///  - a distance : charge une boule d'energie puis la tire vers le joueur
///  - phase 2 sous la moitie des PV : plus rapide, attaques plus frequentes, 3 boules
/// Les PV, le flash rouge et la mort sont geres par EnemyStats, comme pour les squelettes.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class BossAI : MonoBehaviour
{
    [Header("Deplacement")]
    public float moveSpeed = 1.6f;
    public float detectionRange = 9f;
    public LayerMask wallLayer;

    [Header("Coup d'epee")]
    public float meleeRange = 1.6f;      // distance pieds-a-pieds pour lancer le coup
    public float meleeHitRange = 2.1f;   // le joueur doit encore etre la a l'impact
    public int meleeDamage = 2;
    public float meleeWindup = 0.55f;    // delai avant l'impact : le temps d'esquiver
    public float meleeRecovery = 0.8f;   // le boss reste plante apres le coup
    public float meleeCooldown = 1.8f;

    [Header("Boule d'energie")]
    public GameObject projectilePrefab;
    public float beamMinDistance = 3.5f; // pas de tir si le joueur est colle au boss
    public float beamChargeTime = 1.2f;
    public float beamCooldown = 5f;
    public int projectileDamage = 2;
    public float projectileSpeed = 5f;

    [Header("Phase 2 (sous la moitie des PV)")]
    public float phase2SpeedMultiplier = 1.5f;
    public float phase2CooldownMultiplier = 0.65f;
    public int phase2Projectiles = 3;
    public float phase2SpreadAngle = 20f;

    [Header("Visuel")]
    [Tooltip("Enfant retourne pour regarder le joueur (le sprite d'origine regarde a droite)")]
    public Transform visual;
    public GameObject beamChargeEffect;  // particules de charge, actives pendant la charge
    public Transform projectileSpawn;

    [Header("Sons")]
    [Tooltip("Vide = une source 2D est ajoutee sur la camera (pas d'icone haut-parleur sur le boss)")]
    public AudioSource audioSource;
    public AudioClip swordSound, powerLoadSound, beamSound, painSound, deathSound;

    /// <summary>Vrai des que le boss a repere le joueur (affiche la barre de vie).</summary>
    public bool IsEngaged { get; private set; }

    private EnemyStats stats;
    private Rigidbody2D rb;
    private Collider2D feet;
    private Animator animator;
    private Transform player;
    private Collider2D playerFeet;
    private PlayerStats playerStats;

    private Vector2 moveDirection;
    private bool busy;            // en train d'attaquer : ne bouge pas, ne se retourne pas
    private bool phase2;
    private bool dead;
    private float nextMeleeTime;
    private float nextBeamTime;

    void Start()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody2D>();
        feet = GetComponent<Collider2D>();
        animator = GetComponentInChildren<Animator>();

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

        if (beamChargeEffect != null) beamChargeEffect.SetActive(false);
        nextBeamTime = Time.time + 2f; // pas de tir des la premiere seconde

        // Sons joues depuis la camera : le boss n'a pas d'AudioSource, donc pas d'icone
        // haut-parleur dans la vue Game, et le son ne s'arrete pas quand le boss disparait.
        if (audioSource == null && Camera.main != null)
        {
            audioSource = Camera.main.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    // Distances mesurees aux pieds : le pivot du joueur est au milieu de son sprite.
    Vector2 FeetPos() { return feet != null ? (Vector2)feet.bounds.center : (Vector2)transform.position; }
    Vector2 PlayerFeetPos() { return playerFeet != null ? (Vector2)playerFeet.bounds.center : (Vector2)player.position; }

    void Update()
    {
        if (dead || player == null) return;

        if (stats.currentHealth <= 0)
        {
            OnDeath();
            return;
        }

        if (!phase2 && stats.currentHealth <= stats.maxHealth / 2) EnterPhase2();

        Vector2 toPlayer = PlayerFeetPos() - FeetPos();
        float distance = toPlayer.magnitude;

        if (!IsEngaged && distance <= detectionRange) IsEngaged = true;

        moveDirection = Vector2.zero;
        if (!IsEngaged || busy) return;

        FacePlayer(toPlayer.x);

        if (distance <= meleeRange)
        {
            if (Time.time >= nextMeleeTime) StartCoroutine(MeleeAttack());
        }
        else if (distance >= beamMinDistance && Time.time >= nextBeamTime && projectilePrefab != null)
        {
            StartCoroutine(BeamAttack());
        }
        else
        {
            moveDirection = toPlayer.normalized;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (dead || busy || moveDirection == Vector2.zero || IsWallAhead())
        {
            SetSpeed(0f);
            return;
        }

        float speed = phase2 ? moveSpeed * phase2SpeedMultiplier : moveSpeed;
        rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);
        SetSpeed(phase2 ? 3.5f : 1f); // > 3 = animation de course
    }

    // Kinematic : traverse les colliders, on detecte les murs nous-memes (comme EnemyAI)
    bool IsWallAhead()
    {
        if (wallLayer == 0) return false;
        Vector2 size = feet != null ? (Vector2)feet.bounds.size : Vector2.one * 0.5f;
        return Physics2D.BoxCast(FeetPos(), size * 0.9f, 0f, moveDirection, 0.3f, wallLayer).collider != null;
    }

    void FacePlayer(float dx)
    {
        if (visual == null || Mathf.Abs(dx) <= 0.2f) return;
        Vector3 s = visual.localScale;
        s.x = dx > 0 ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        visual.localScale = s;
    }

    void SetSpeed(float speed)
    {
        if (animator != null) animator.SetFloat("Speed", speed);
    }

    float Cooldown(float baseCooldown)
    {
        return phase2 ? baseCooldown * phase2CooldownMultiplier : baseCooldown;
    }

    IEnumerator MeleeAttack()
    {
        busy = true;
        SetSpeed(0f);
        if (animator != null) animator.SetTrigger("Attack");
        PlaySound(swordSound);

        yield return new WaitForSeconds(meleeWindup);

        if (!dead && player != null && Vector2.Distance(PlayerFeetPos(), FeetPos()) <= meleeHitRange && playerStats != null)
            playerStats.TakeDamage(meleeDamage);

        yield return new WaitForSeconds(meleeRecovery);

        nextMeleeTime = Time.time + Cooldown(meleeCooldown);
        busy = false;
    }

    IEnumerator BeamAttack()
    {
        busy = true;
        SetSpeed(0f);
        if (animator != null)
        {
            animator.SetBool("Busy", true);
            animator.SetTrigger("BeamAttack");
        }
        if (beamChargeEffect != null) beamChargeEffect.SetActive(true);
        PlaySound(powerLoadSound);

        yield return new WaitForSeconds(phase2 ? beamChargeTime * 0.75f : beamChargeTime);

        if (beamChargeEffect != null) beamChargeEffect.SetActive(false);

        if (!dead && player != null)
        {
            PlaySound(beamSound);
            Vector2 origin = projectileSpawn != null ? (Vector2)projectileSpawn.position : FeetPos() + Vector2.up;
            // vise le centre du joueur, pas ses pieds
            Vector2 aim = ((Vector2)player.position - origin).normalized;

            int count = phase2 ? phase2Projectiles : 1;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-phase2SpreadAngle, phase2SpreadAngle, i / (float)(count - 1));
                Fire(origin, Quaternion.Euler(0f, 0f, angle) * aim);
            }
        }

        if (animator != null) animator.SetBool("Busy", false);
        yield return new WaitForSeconds(0.4f);

        nextBeamTime = Time.time + Cooldown(beamCooldown);
        busy = false;
    }

    void Fire(Vector2 origin, Vector2 direction)
    {
        GameObject ball = Instantiate(projectilePrefab, origin, Quaternion.identity);
        BossProjectile projectile = ball.GetComponent<BossProjectile>();
        if (projectile != null) projectile.Launch(direction, projectileSpeed, projectileDamage, wallLayer);
    }

    void EnterPhase2()
    {
        phase2 = true;
        PlaySound(painSound);
        if (animator != null && !busy) animator.SetTrigger("Hurt"); // il titube, puis enrage
    }

    void OnDeath()
    {
        dead = true;
        IsEngaged = false;
        StopAllCoroutines();
        if (beamChargeEffect != null) beamChargeEffect.SetActive(false);
        if (animator != null) animator.SetBool("Busy", false);

        PlaySound(deathSound); // la source est sur la camera : elle survit a l'effacement du boss
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}
