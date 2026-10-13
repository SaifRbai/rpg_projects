using UnityEngine.SceneManagement;

/// <summary>
/// Retient le dernier niveau joue, pour que l'ecran de Game Over relance CE niveau
/// au lieu de toujours repartir du premier.
/// Classe statique : elle survit aux changements de scene et repart a zero au lancement du jeu.
/// </summary>
public static class LevelTracker
{
    public const string FirstLevel = "Room1";

    private static string currentLevel = FirstLevel;

    public static string CurrentLevel
    {
        get { return string.IsNullOrEmpty(currentLevel) ? FirstLevel : currentLevel; }
    }

    /// <summary>Appele par PlayerStats au demarrage de chaque niveau jouable.</summary>
    public static void RecordCurrentLevel()
    {
        string name = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(name))
            currentLevel = name;
    }
}
