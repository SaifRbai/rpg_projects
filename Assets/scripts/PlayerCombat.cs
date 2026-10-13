using System.Collections;
using UnityEngine;

/// <summary>
/// Attaque du joueur (barre Espace) : touche tous les ennemis dans un cercle
/// autour de lui, joue l'animation d'attaque et l'immobilise pendant le coup.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat")]
    public int attackDamage = 1;
    public float attackRange = 1.5f;
    public LayerMask enemyLayer;
    public float attackCooldown = 0.5f; // temps minimum entre deux coups

    private Animator animator;
    private PlayerMovement playerMovement;
    private float nextAttackTime = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        animator.ResetTrigger("Attack");
    }

    void Update()
    {
        if (Time.time >= nextAttackTime && Input.GetKeyDown(KeyCode.Space))
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void Attack()
    {
        if (playerMovement != null) playerMovement.ForceStop();

        // tous les colliders dans le rayon d'attaque, sur le calque des ennemis
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            EnemyStats enemy = hit.GetComponent<EnemyStats>();
            if (enemy != null) enemy.TakeDamage(attackDamage);
        }

        animator.SetTrigger("Attack");
        StartCoroutine(ResumeAfterAttack());
    }

    IEnumerator ResumeAfterAttack()
    {
        yield return new WaitForSeconds(attackCooldown);
        if (playerMovement != null) playerMovement.ResumeMovement();
    }

    // affiche le rayon d'attaque dans la vue Scene quand le joueur est selectionne
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
