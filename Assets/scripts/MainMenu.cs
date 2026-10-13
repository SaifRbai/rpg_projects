using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boutons du menu principal (relies dans l'Inspector via OnClick).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Vitesse de tout le jeu : 1 = normal, 0.5 = ralenti, 1.5 = 50 % plus rapide")]
    [Range(0.25f, 3f)]
    public float gameSpeed = 1f;

    public void PlayGame()
    {
        // Time.timeScale accelere/ralentit tout : deplacements, animations, particules, delais.
        // La valeur reste valable dans toutes les scenes suivantes.
        Time.timeScale = gameSpeed;
        SceneManager.LoadScene(LevelTracker.FirstLevel);
    }

    public void QuitGame()
    {
        Application.Quit(); // sans effet dans l'editeur, ferme le jeu une fois compile
    }
}
