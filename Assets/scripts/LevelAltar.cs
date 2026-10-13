using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Autel de fin de niveau. Il reste gris tant que tous les squelettes ne sont pas morts,
/// puis il brille ; le joueur marche dessus pour passer au niveau suivant.
/// </summary>
public class LevelAltar : MonoBehaviour
{
    [Header("Niveau")]
    public string nextSceneName = "Room2";
    public int totalEnemies = 1; // nombre de squelettes a tuer dans cette salle

    [Header("Visuel")]
    public SpriteRenderer altarSprite;
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f); // gris fonce
    public Color activeColor = new Color(1f, 0.8f, 0f, 1f);       // lueur doree

    private bool isActive = false;
    private bool isLoading = false;
    private int enemiesDefeated = 0;

    void Start()
    {
        if (altarSprite != null)
            altarSprite.color = inactiveColor;
    }

    /// <summary>Appele par EnemyStats a chaque squelette tue.</summary>
    public void RegisterEnemyDefeat()
    {
        enemiesDefeated++;
        if (enemiesDefeated >= totalEnemies && !isActive)
            ActivateAltar();
    }

    void ActivateAltar()
    {
        isActive = true;
        if (altarSprite != null)
            altarSprite.color = activeColor;
        StartCoroutine(PulseGlow());
    }

    // fait pulser l'autel entre dore et blanc pour attirer l'oeil
    IEnumerator PulseGlow()
    {
        while (true)
        {
            yield return Fade(activeColor, Color.white);
            yield return Fade(Color.white, activeColor);
        }
    }

    IEnumerator Fade(Color from, Color to)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime * 2f)
        {
            if (altarSprite != null)
                altarSprite.color = Color.Lerp(from, to, t);
            yield return null;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive || isLoading || !other.CompareTag("Player")) return;

        // isLoading : evite de lancer deux chargements si le trigger se declenche deux fois
        isLoading = true;
        SceneManager.LoadScene(nextSceneName);
    }
}
