# Path of the Knight

Petit RPG 2D en vue de dessus fait avec Unity.

## Prérequis

- [Unity Hub](https://unity.com/download)
- **Unity 2022.3.62f3** (installer cette version exacte depuis Unity Hub → *Installs* → *Install Editor* → onglet *Archive* si elle n'apparaît pas)
- Git

## Ouvrir le jeu dans Unity

1. Cloner le dépôt :
   ```bash
   git clone https://github.com/SaifRbai/rpg_projects.git
   ```
2. Ouvrir **Unity Hub** → **Add** → **Add project from disk** → choisir le dossier `rpg_projects`.
3. Cliquer sur le projet pour l'ouvrir avec Unity **2022.3.62f3**.
   La première ouverture prend quelques minutes (Unity recrée le dossier `Library/`).
4. Dans la fenêtre **Project**, ouvrir `Assets/Scenes/MainMenu.unity` (double-clic).
5. Appuyer sur **Play** ▶ en haut de l'éditeur.

> Si le jeu démarre dans une autre scène ou si l'écran est vide, vérifier que la scène ouverte est bien `MainMenu`.

## Contrôles

| Touche | Action |
|---|---|
| Flèches directionnelles / WASD | Se déplacer |
| Espace | Attaquer |

## Scènes

`MainMenu` → `Room1` → `Room2` → `Room3` → `Victory` (ou `GameOver` si le joueur meurt).

## Créer un exécutable (optionnel)

**File → Build Settings** → choisir la plateforme (Windows / Linux / Mac) → **Build**.
