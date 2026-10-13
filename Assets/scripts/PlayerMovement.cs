using UnityEngine;

/// <summary>
/// Deplacement du joueur en vue de dessus (ZQSD / fleches).
/// Le mouvement passe par le Rigidbody2D pour que les murs bloquent le joueur.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;

    // Vrai pendant une attaque : PlayerCombat immobilise le joueur le temps du coup.
    private bool isForcedStopped = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (isForcedStopped)
        {
            moveInput = Vector2.zero;
            return;
        }

        // normalise : en diagonale le joueur ne va pas plus vite
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;

        // le sprite regarde dans la direction du deplacement horizontal
        if (moveInput.x < -0.1f) spriteRenderer.flipX = true;
        else if (moveInput.x > 0.1f) spriteRenderer.flipX = false;
    }

    void FixedUpdate()
    {
        // physique dans FixedUpdate : deplacement regulier quel que soit le nombre d'images/s
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);

        // Speed pilote la transition Idle <-> Course dans l'Animator
        animator.SetFloat("Speed", moveInput.magnitude < 0.1f ? 0f : 1f);
    }

    /// <summary>Immobilise le joueur (appele au debut d'une attaque).</summary>
    public void ForceStop()
    {
        isForcedStopped = true;
        moveInput = Vector2.zero;
        animator.SetFloat("Speed", 0f);
    }

    /// <summary>Rend la main au joueur (appele a la fin de l'attaque).</summary>
    public void ResumeMovement()
    {
        isForcedStopped = false;
    }
}
