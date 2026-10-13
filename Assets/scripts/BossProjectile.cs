using UnityEngine;

/// <summary>
/// Boule d'energie tiree par le boss : avance en ligne droite, blesse le joueur
/// au contact et disparait contre un mur ou au bout de sa duree de vie.
/// Le collider du joueur ne couvre que ses pieds alors que la boule vole a hauteur
/// du corps : on teste donc le contact avec une boite autour du corps du joueur.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossProjectile : MonoBehaviour
{
    public float lifetime = 4f;
    public GameObject impactEffect; // optionnel

    [Tooltip("Corps du joueur (autour de son pivot) touche par la boule")]
    public Vector2 bodySize = new Vector2(0.7f, 1.3f);
    public Vector2 bodyOffset = new Vector2(0f, -0.25f);
    public float radius = 0.25f;

    [Tooltip("La boule vole a hauteur du torse : les murs sont testes a son ombre au sol")]
    public float flightHeight = 1f;

    private Rigidbody2D rb;
    private Vector2 velocity;
    private int damage;
    private LayerMask wallLayer;
    private bool launched;
    private PlayerStats player;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.GetComponent<PlayerStats>();
    }

    public void Launch(Vector2 direction, float speed, int damageAmount, LayerMask walls)
    {
        velocity = direction.normalized * speed;
        damage = damageAmount;
        wallLayer = walls;
        launched = true;
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (!launched) return;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);

        if (player != null && HitsPlayerBody())
        {
            player.TakeDamage(damage);
            Explode();
            return;
        }

        if (wallLayer != 0 && Physics2D.OverlapPoint(rb.position + Vector2.down * flightHeight, wallLayer) != null)
            Explode();
    }

    bool HitsPlayerBody()
    {
        Vector2 center = (Vector2)player.transform.position + bodyOffset;
        Vector2 d = rb.position - center;
        return Mathf.Abs(d.x) <= bodySize.x / 2f + radius && Mathf.Abs(d.y) <= bodySize.y / 2f + radius;
    }

    void Explode()
    {
        if (impactEffect != null) Destroy(Instantiate(impactEffect, transform.position, Quaternion.identity), 2f);
        Destroy(gameObject);
    }
}
