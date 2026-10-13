using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Points de vie du joueur, barre de vie a l'ecran et Game Over a la mort.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public int maxHealth = 10;
    public int currentHealth;
    public int shield = 0; // absorbe les degats avant les PV

    [Tooltip("Remplissage rouge de la barre de vie (UI)")]
    public RectTransform hpFillRect;

    private float maxWidth;
    private bool isDead = false;

    void Start()
    {
        // retient ce niveau pour que le Game Over relance le bon
        LevelTracker.RecordCurrentLevel();

        currentHealth = maxHealth;

        // largeur de la barre pleine = reference pour 100 % de vie
        if (hpFillRect != null)
            maxWidth = hpFillRect.sizeDelta.x;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        if (shield > 0)
        {
            shield -= damageAmount;
            if (shield < 0)
            {
                // le surplus de degats passe a travers le bouclier
                currentHealth += shield;
                shield = 0;
            }
        }
        else
        {
            currentHealth -= damageAmount;
        }

        UpdateHealthBar();

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>La barre retrecit en largeur proportionnellement aux PV restants.</summary>
    public void UpdateHealthBar()
    {
        if (hpFillRect == null) return;

        // Clamp01 : sans ca, des PV negatifs donnaient une largeur negative
        float healthPercent = Mathf.Clamp01((float)currentHealth / maxHealth);
        hpFillRect.sizeDelta = new Vector2(maxWidth * healthPercent, hpFillRect.sizeDelta.y);
    }

    void Die()
    {
        // plusieurs squelettes peuvent frapper la meme image : on ne charge le Game Over qu'une fois
        isDead = true;
        SceneManager.LoadScene("GameOver");
    }
}
