using System.Collections;
using UnityEngine;

/// <summary>
/// Points de vie du squelette, flash rouge quand il est touche, et sequence de mort :
/// particules, animation, puis le corps s'efface et disparait.
/// </summary>
public class EnemyStats : MonoBehaviour
{
    public int maxHealth = 5;
    public int currentHealth;

    [Header("Effet de mort")]
    [Tooltip("Particules a la mort. Vide = Resources/Effects/SkeletonDeathFX")]
    public GameObject deathEffectPrefab;
    public float deathAnimDuration = 1.1f; // duree de l'animation Skeleton_Dying
    public float fadeDuration = 0.8f;      // puis le corps s'efface pendant ce temps

    private bool isDead = false;

    // Le squelette est compose de plusieurs sprites (tete, bras, jambes...), pas d'un seul.
    private SpriteRenderer[] bodyParts;
    private Color[] baseColors;
    private Coroutine flashRoutine;

    void Start()
    {
        currentHealth = maxHealth;

        bodyParts = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
            baseColors[i] = bodyParts[i].color;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;

        // on relance le flash au lieu d'en empiler plusieurs (le corps restait rouge)
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRed());

        if (currentHealth <= 0)
        {
            isDead = true;
            Die();
        }
    }

    IEnumerator FlashRed()
    {
        SetBodyColor(Color.red, 1f);
        yield return new WaitForSecondsRealtime(0.15f);
        SetBodyColor(Color.white, 1f);
        flashRoutine = null;
    }

    // tint = multiplie la couleur d'origine de chaque partie, alpha = opacite (1 = opaque)
    void SetBodyColor(Color tint, float alpha)
    {
        if (bodyParts == null) return;
        for (int i = 0; i < bodyParts.Length; i++)
        {
            if (bodyParts[i] == null) continue;
            Color c = baseColors[i] * tint;
            c.a = baseColors[i].a * alpha;
            bodyParts[i].color = c;
        }
    }

    void Die()
    {
        // 1. particules (AVANT de couper le collider : on s'en sert pour trouver le centre du corps)
        SpawnDeathEffect();

        // 2. cacher la barre de vie et prevenir l'autel
        Transform hpBar = transform.Find("SkeletonHealthCanvas");
        if (hpBar != null) hpBar.gameObject.SetActive(false);

        LevelAltar altar = FindObjectOfType<LevelAltar>();
        if (altar != null) altar.RegisterEnemyDefeat();

        // 3. animation de mort ; Speed = 0 pour ne pas rester bloque dans l'etat Marche
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Die");
        }

        // 4. couper l'IA et le collider : le joueur peut marcher sur le corps
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 5. arreter le mouvement (sans changer le type de Rigidbody)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;

        // 6. le corps reste au sol le temps de l'animation, puis s'efface
        StartCoroutine(DeathSequence(anim));
    }

    void SpawnDeathEffect()
    {
        GameObject prefab = deathEffectPrefab != null
            ? deathEffectPrefab
            : Resources.Load<GameObject>("Effects/SkeletonDeathFX");
        if (prefab == null) return;

        Collider2D col = GetComponent<Collider2D>();
        Vector3 pos = col != null ? col.bounds.center : transform.position;

        // pas enfant du squelette : il est desactive avant que les ames aient fini de monter
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
        Destroy(fx, 3f);
    }

    IEnumerator DeathSequence(Animator anim)
    {
        yield return new WaitForSeconds(deathAnimDuration);
        if (anim != null) anim.enabled = false; // fige la derniere image (allonge au sol)

        if (flashRoutine != null) StopCoroutine(flashRoutine);

        Vector3 start = transform.position;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            SetBodyColor(Color.Lerp(Color.white, new Color(0.6f, 0.6f, 0.7f), k), 1f - k); // grisaille + transparence
            transform.position = start + Vector3.down * 0.15f * k;                            // s'enfonce un peu
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
