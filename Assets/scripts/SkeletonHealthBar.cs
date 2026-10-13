using UnityEngine;

/// <summary>
/// Barre de vie au-dessus du squelette : sa largeur suit les PV de EnemyStats.
/// </summary>
public class SkeletonHealthBar : MonoBehaviour
{
    public EnemyStats enemyStats; // vide = cherche dans les parents

    private float maxWidth;
    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        maxWidth = rectTransform.sizeDelta.x;

        if (enemyStats == null)
            enemyStats = GetComponentInParent<EnemyStats>();
    }

    void Update()
    {
        if (enemyStats == null || rectTransform == null) return;

        float healthPercent = Mathf.Clamp01((float)enemyStats.currentHealth / enemyStats.maxHealth);
        rectTransform.sizeDelta = new Vector2(maxWidth * healthPercent, rectTransform.sizeDelta.y);
    }
}
