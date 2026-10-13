using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boutons des ecrans de fin (Game Over et Victoire).
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Reglage")]
    [Tooltip("Coche sur l'ecran de Victoire : Rejouer repart du premier niveau. " +
             "Decoche sur Game Over : Rejouer relance le niveau ou tu es mort.")]
    public bool restartFromFirstLevel = false;

    public void RestartGame()
    {
        string target = restartFromFirstLevel ? LevelTracker.FirstLevel : LevelTracker.CurrentLevel;
        SceneManager.LoadScene(target);
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
