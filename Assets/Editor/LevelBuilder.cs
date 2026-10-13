using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Genere Room2, Room3 et Victory a partir de Room1 / GameOver.
///
/// Principe : on COPIE Room1 au lieu de reconstruire une scene de zero, parce que
/// Player, Main Camera, Canvas et la Healthbar ne sont pas des prefabs — leurs
/// references (hpFillRect, CameraFollow.player, onClick des boutons) sont cablees
/// a la main dans la scene et seraient perdues.
/// Ensuite on vide le decor de Room1, on repeint la salle dans le meme style que
/// Room1 (bordure de pierre + facade avec fenetres), on pose un decor dessine a la
/// main pour chaque theme, puis on clone les squelettes et on monte la difficulte.
///
/// Relancable : regenere les scenes a l'identique (placements deterministes par seed).
/// </summary>
public static class LevelBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string SourceLevel = ScenesDir + "/Room1.unity";
    const string SourceGameOver = ScenesDir + "/GameOver.unity";
    const string VictoryScene = ScenesDir + "/Victory.unity";
    const string VictoryBackground = "Assets/MainMenu/Misty Fantasy Ruins Menu Background.png";

    // Anciens noms : remplaces par Room2 / Room3, supprimes apres une generation reussie.
    static readonly string[] ObsoleteScenes = { ScenesDir + "/Level_2.unity", ScenesDir + "/Level_3.unity" };

    const string CainosDir = "Assets/Cainos/Pixel Art Top Down - Basic";
    const string GrassDir = CainosDir + "/Tile Palette/TP Grass";
    const string WallDir = CainosDir + "/Tile Palette/TP Wall";
    const string StoneDir = CainosDir + "/Tile Palette/TP Stone Ground";
    const string PrefabDir = CainosDir + "/Prefab";
    const string SolidTileDir = "Assets/Tiles/Solid";
    const string DecorRoot = "Decor_Theme";

    const int WallsLayer = 6; // "Walls" : bloque le joueur ET le raycast de EnemyAI

    /// <summary>Biome du niveau : chaque niveau doit se lire d'un coup d'oeil comme un lieu different.</summary>
    enum Theme { Garden, Crypt }

    class LevelSpec
    {
        public string SceneName;
        public string NextScene;
        public int Width;
        public int Height;
        public int EnemyCount;
        public int EnemyHealth;
        public int EnemyDamage;
        public float EnemySpeed;
        public float DetectionRange;
        public float DamageCooldown;
        public Theme Biome;
        public int Seed;
    }

    // Progression de difficulte. Niveau 1 (Room1) = 1 squelette, 5 PV, 1 degat, vitesse 2.
    static readonly LevelSpec[] Specs =
    {
        new LevelSpec
        {
            SceneName = "Room2", NextScene = "Room3",
            Width = 24, Height = 14, Seed = 1202,
            // Niveau 2 = jardin en ruines : herbe fleurie, allee pavee, muret central.
            Biome = Theme.Garden,
            EnemyCount = 3,
            // Le joueur fait 2 PV/s et reste IMMOBILE pendant qu'il frappe (ForceStop).
            // D'ou : PV bas (temps immobile court) et 1 degat, car les degats de contact
            // s'ADDITIONNENT quand plusieurs squelettes te touchent en meme temps.
            // detectionRange court = ils viennent par vagues, pas toute la salle d'un coup.
            EnemyHealth = 5, EnemyDamage = 1, EnemySpeed = 2.3f,
            DetectionRange = 6f, DamageCooldown = 1.4f,
        },
        new LevelSpec
        {
            SceneName = "Room3", NextScene = "Victory",
            Width = 30, Height = 20, Seed = 1303,
            // Niveau 3 = crypte : dalles froides, piliers, tombes et sarcophages.
            Biome = Theme.Crypt,
            EnemyCount = 5,
            // Plus dur que Room2 par le NOMBRE, la VITESSE et l'agro — pas par les
            // degats bruts : 5 squelettes a 2 degats tuent avant la fin du menage.
            EnemyHealth = 5, EnemyDamage = 1, EnemySpeed = 2.8f,
            DetectionRange = 7f, DamageCooldown = 1.4f,
        },
    };

    /// <summary>Etat de la salle en cours de construction (coordonnees en cases).</summary>
    class Room
    {
        public LevelSpec Spec;
        public Tilemap Ground;
        public Tilemap Walls;
        public System.Random Rng;
        public Transform Decor;

        public readonly HashSet<Vector3Int> Solid = new HashSet<Vector3Int>();     // murs + blocs
        public readonly HashSet<Vector3Int> Obstacles = new HashSet<Vector3Int>(); // blocs interieurs seulement
        public readonly HashSet<Vector3Int> Path = new HashSet<Vector3Int>();      // trajet depart -> autel, jamais encombre
        public readonly HashSet<Vector3Int> Taken = new HashSet<Vector3Int>();     // deco solide, ennemis, zones reservees

        public Vector3Int Start;
        public Vector3Int Altar;

        public int W { get { return Spec.Width; } }
        public int H { get { return Spec.Height; } }

        /// <summary>Interieur praticable : la facade du bas occupe les rangees 0 et 1.</summary>
        public bool InPlay(Vector3Int c)
        {
            return c.x >= 1 && c.x <= W - 2 && c.y >= 2 && c.y <= H - 2;
        }

        public bool IsFree(Vector3Int c)
        {
            return InPlay(c) && !Solid.Contains(c) && !Path.Contains(c) && !Taken.Contains(c);
        }
    }

    [MenuItem("Tools/RPG/Generer Room2, Room3 et Victory")]
    public static void BuildAll()
    {
        // EditorSceneManager.OpenScene est interdit en Play mode. On sort AVANT de toucher
        // au moindre asset : sinon la copie de Room1 a deja ecrase une salle et le projet
        // reste a moitie genere.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[LevelBuilder] Sors du mode Play (Ctrl+P) puis relance : "
                         + "les scenes ne peuvent pas etre ouvertes pendant le Play. "
                         + "Aucun asset n'a ete modifie.");
            return;
        }

        if (!System.IO.File.Exists(SourceLevel))
        {
            Debug.LogError("[LevelBuilder] Scene source introuvable : " + SourceLevel);
            return;
        }

        TileCache.Clear();
        CleanMissingScripts();
        LinkRoom1();

        foreach (LevelSpec spec in Specs)
        {
            if (!BuildLevel(spec))
            {
                Debug.LogError("[LevelBuilder] Echec sur " + spec.SceneName + " — on s'arrete.");
                return;
            }
        }

        BuildVictoryScene();
        RemoveObsoleteScenes();
        FixBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LevelBuilder] TERMINE : Room2, Room3, Victory generes et ajoutes aux Build Settings.");
    }

    /// <summary>L'autel de Room1 doit mener a Room2 (il pointait vers l'ancien Level_2).</summary>
    static void LinkRoom1()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceLevel, OpenSceneMode.Single);
        LevelAltar altar = Object.FindObjectOfType<LevelAltar>();
        if (altar == null || altar.nextSceneName == Specs[0].SceneName) return;

        altar.nextSceneName = Specs[0].SceneName;
        EditorUtility.SetDirty(altar);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LevelBuilder] Room1 : autel -> " + Specs[0].SceneName);
    }

    static bool BuildLevel(LevelSpec spec)
    {
        string dest = ScenesDir + "/" + spec.SceneName + ".unity";

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dest) != null)
            AssetDatabase.DeleteAsset(dest);

        if (!AssetDatabase.CopyAsset(SourceLevel, dest))
        {
            Debug.LogError("[LevelBuilder] CopyAsset a echoue vers " + dest);
            return false;
        }
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(dest, OpenSceneMode.Single);

        // --- reperer les acteurs ---
        LevelAltar altar = Object.FindObjectOfType<LevelAltar>();
        if (altar == null) { Debug.LogError("[LevelBuilder] Pas de LevelAltar dans " + dest); return false; }

        EnemyStats template = Object.FindObjectOfType<EnemyStats>();
        if (template == null) { Debug.LogError("[LevelBuilder] Pas d'ennemi (EnemyStats) dans " + dest); return false; }

        PlayerStats playerStats = Object.FindObjectOfType<PlayerStats>();
        if (playerStats == null) { Debug.LogError("[LevelBuilder] Pas de Player dans " + dest); return false; }
        GameObject player = playerStats.gameObject;

        // --- tilemaps : celle avec collider = murs, l'autre = sol ---
        Tilemap[] maps = Object.FindObjectsOfType<Tilemap>();
        Tilemap walls = maps.FirstOrDefault(m => m.GetComponent<TilemapCollider2D>() != null);
        Tilemap ground = maps.FirstOrDefault(m => m != walls);
        if (walls == null || ground == null)
        {
            Debug.LogError("[LevelBuilder] Il faut une tilemap de sol et une tilemap de murs (avec TilemapCollider2D) dans " + dest);
            return false;
        }

        // --- le decor de Room1 (arbres, portail, escalier...) n'a rien a faire ici ---
        Transform altarRoot = altar.transform.root;
        Transform enemyRoot = template.transform.root;
        RemoveSourceDecor(scene, player.transform.root, altarRoot, enemyRoot);

        Room room = new Room
        {
            Spec = spec,
            Ground = ground,
            Walls = walls,
            Rng = new System.Random(spec.Seed),
            // rangee 3 : en rangee 2 le sprite du joueur deborde sur la facade du bas
            Start = new Vector3Int(2, 3, 0),
            // l'autel (3 x 2 cases) tient dans le coin haut droit, sous la bordure
            Altar = new Vector3Int(spec.Width - 3, spec.Height - 4, 0),
        };

        ground.ClearAllTiles();
        walls.ClearAllTiles();

        // --- structure : murs, obstacles, trajet ---
        PaintOuterWalls(room);
        if (spec.Biome == Theme.Garden) BuildGardenWall(room);
        else BuildCryptPillars(room);
        LayPath(room);

        if (spec.Biome == Theme.Garden) PaintGardenGround(room);
        else PaintCryptGround(room);
        ApplyThemeTint(room);

        ground.CompressBounds();
        walls.CompressBounds();

        // --- depart / autel ---
        Vector3 startPos = CellCenter(room, room.Start);
        player.transform.position = new Vector3(startPos.x, startPos.y, player.transform.position.z);
        Reserve(room, room.Start, 2);

        Vector3 altarPos = CellFoot(room, room.Altar, 0f);
        altarRoot.position = new Vector3(altarPos.x, altarPos.y, altarRoot.position.z);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = 0; dy <= 2; dy++)
                room.Taken.Add(room.Altar + new Vector3Int(dx, dy, 0));

        // la camera demarre sur le joueur pour eviter un glissement au lancement
        Camera cam = Object.FindObjectOfType<Camera>();
        if (cam != null)
            cam.transform.position = new Vector3(startPos.x, startPos.y, cam.transform.position.z);

        altar.nextSceneName = spec.NextScene;
        altar.totalEnemies = spec.EnemyCount;
        EditorUtility.SetDirty(altar);

        // --- decor : d'abord les pieces maitresses, puis les ennemis, puis le petit decor ---
        GameObject decor = new GameObject(DecorRoot);
        room.Decor = decor.transform;

        if (spec.Biome == Theme.Garden) DecorateGarden(room);
        else DecorateCrypt(room);

        List<Vector3Int> spots = PickEnemyCells(room);
        if (spots.Count < spec.EnemyCount)
        {
            Debug.LogError("[LevelBuilder] Pas assez de place pour " + spec.EnemyCount + " squelettes dans " + dest);
            return false;
        }

        ApplyDifficulty(enemyRoot.gameObject, spec);
        PlaceEnemy(room, enemyRoot, spots[0]);
        for (int i = 1; i < spec.EnemyCount; i++)
        {
            GameObject clone = Object.Instantiate(enemyRoot.gameObject, enemyRoot.parent);
            clone.name = enemyRoot.name + "_" + (i + 1);
            ApplyDifficulty(clone, spec);
            PlaceEnemy(room, clone.transform, spots[i]);
        }

        if (spec.Biome == Theme.Garden) ScatterGarden(room);
        else ScatterCrypt(room);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, dest);
        if (!saved) { Debug.LogError("[LevelBuilder] SaveScene a echoue pour " + dest); return false; }

        Debug.Log(string.Format(
            "[LevelBuilder] {0} ({1}) : salle {2}x{3}, {4} squelettes ({5} PV, {6} degats, vitesse {7}), {8} elements de decor, autel -> {9}",
            spec.SceneName, spec.Biome, spec.Width, spec.Height, spec.EnemyCount,
            spec.EnemyHealth, spec.EnemyDamage, spec.EnemySpeed, room.Decor.childCount, spec.NextScene));
        return true;
    }

    /// <summary>Supprime tout ce qui n'est pas joueur, camera, UI, grille, autel ou ennemi.</summary>
    static void RemoveSourceDecor(Scene scene, Transform playerRoot, Transform altarRoot, Transform enemyRoot)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform t = root.transform;
            if (t == playerRoot || t == altarRoot || t == enemyRoot) continue;
            if (root.GetComponentInChildren<Camera>(true) != null) continue;
            if (root.GetComponentInChildren<Canvas>(true) != null) continue;
            if (root.GetComponentInChildren<Grid>(true) != null) continue;
            if (root.GetComponentInChildren<EnemyStats>(true) != null) continue;
            if (root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    //  TUILES
    //  Tileset "TP Wall" de Cainos (le meme que Room1) :
    //    0 / 1 / 2      bordure haute (coin gauche, milieu, coin droit)
    //    8 / 9          bordure gauche / droite        44-49 : variantes fissurees
    //    15-17 / 23-25  facade du bas, rangee haute / basse (gauche, milieu, droite)
    //    28-31 / 32-35  facade, variantes de briques    55 / 56 : facade avec fenetre
    //    53 54 / 57 58 / 59 60  bloc de pierre de 2 cases de large (dessus, facade, base)
    // =====================================================================

    static readonly Dictionary<string, TileBase> TileCache = new Dictionary<string, TileBase>();

    static TileBase LoadTile(string path)
    {
        TileBase tile;
        if (TileCache.TryGetValue(path, out tile)) return tile;
        tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (tile == null) Debug.LogWarning("[LevelBuilder] Tuile introuvable : " + path);
        TileCache[path] = tile;
        return tile;
    }

    static TileBase WallTile(int index) { return LoadTile(WallDir + "/TX Tileset Wall_" + index + ".asset"); }
    static TileBase GrassTile(string variant) { return LoadTile(GrassDir + "/TX Tileset Grass " + variant + ".asset"); }
    static TileBase StoneTile(int index) { return LoadTile(StoneDir + "/TX Tileset Stone Ground_" + index + ".asset"); }

    /// <summary>
    /// Copie d'une tuile de mur avec un collider plein (ColliderType.Grid).
    /// Les tuiles de bordure n'ont qu'un liseré comme forme physique : parfait pour le
    /// tour de la salle (comme Room1), mais un bloc au milieu de la salle doit bloquer
    /// toute sa surface, sinon on marche "dedans".
    /// </summary>
    static TileBase SolidWallTile(int index)
    {
        string path = SolidTileDir + "/TX Tileset Wall_" + index + " Solid.asset";
        TileBase cached;
        if (TileCache.TryGetValue(path, out cached)) return cached;

        Tile source = WallTile(index) as Tile;
        if (source == null) return WallTile(index);

        if (!AssetDatabase.IsValidFolder("Assets/Tiles")) AssetDatabase.CreateFolder("Assets", "Tiles");
        if (!AssetDatabase.IsValidFolder(SolidTileDir)) AssetDatabase.CreateFolder("Assets/Tiles", "Solid");

        Tile solid = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (solid == null)
        {
            solid = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(solid, path);
        }
        solid.sprite = source.sprite;
        solid.color = source.color;
        solid.transform = source.transform;
        solid.flags = source.flags;
        solid.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(solid);

        TileCache[path] = solid;
        return solid;
    }

    static int Pick(Room r, int[] options) { return options[r.Rng.Next(options.Length)]; }

    static void SetWall(Room r, int x, int y, TileBase tile)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);
        r.Walls.SetTile(cell, tile);
        r.Solid.Add(cell);
    }

    /// <summary>Tour de la salle dans le style de Room1 : liseré en haut et sur les cotes, facade a fenetres en bas.</summary>
    static void PaintOuterWalls(Room r)
    {
        int[] tops = { 1, 1, 1, 50, 51, 52 };
        int[] lefts = { 8, 8, 8, 44, 45, 46 };
        int[] rights = { 9, 9, 9, 47, 48, 49 };

        for (int x = 0; x < r.W; x++)
        {
            int top = x == 0 ? 0 : x == r.W - 1 ? 2 : Pick(r, tops);
            SetWall(r, x, r.H - 1, WallTile(top));
        }

        for (int y = 2; y < r.H - 1; y++)
        {
            SetWall(r, 0, y, WallTile(Pick(r, lefts)));
            SetWall(r, r.W - 1, y, WallTile(Pick(r, rights)));
        }

        // facade sur 2 rangees ; une fenetre toutes les 6 cases
        for (int x = 0; x < r.W; x++)
        {
            int upper, lower;
            if (x == 0) { upper = 15; lower = 23; }
            else if (x == r.W - 1) { upper = 17; lower = 25; }
            else if (x % 6 == 3 && x < r.W - 2) { upper = 55; lower = 56; }
            else { int v = r.Rng.Next(4); upper = 28 + v; lower = 32 + v; }

            SetWall(r, x, 1, WallTile(upper));
            SetWall(r, x, 0, WallTile(lower));
        }
    }

    /// <summary>Bloc de pierre sureleve de 2 cases de large, du rang y0 (base) au rang y1 (dessus). Hauteur mini : 3.</summary>
    static void StoneBlock(Room r, int x0, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            int left, right;
            if (y == y1) { left = 53; right = 54; }
            else if (y == y0) { left = 59; right = 60; }
            else if (y == y0 + 1) { left = 57; right = 58; }
            else { left = 8; right = 9; }

            SetWall(r, x0, y, SolidWallTile(left));
            SetWall(r, x0 + 1, y, SolidWallTile(right));
            r.Obstacles.Add(new Vector3Int(x0, y, 0));
            r.Obstacles.Add(new Vector3Int(x0 + 1, y, 0));
        }
    }

    /// <summary>Jardin : muret central de 2 cases avec un passage de 3 cases au milieu.</summary>
    static void BuildGardenWall(Room r)
    {
        int x0 = r.W / 2 - 1;
        int gapMid = r.H / 2;
        StoneBlock(r, x0, 2, gapMid - 2);
        StoneBlock(r, x0, gapMid + 2, r.H - 2);
    }

    /// <summary>Crypte : deux rangees de piliers (2 x 3 cases) qui cassent les lignes de vue.</summary>
    static void BuildCryptPillars(Room r)
    {
        foreach (int px in PillarColumns(r))
            foreach (int py in PillarRows(r))
                StoneBlock(r, px, py, py + 2);
    }

    static IEnumerable<int> PillarColumns(Room r) { for (int px = 5; px + 1 <= r.W - 6; px += 6) yield return px; }
    static IEnumerable<int> PillarRows(Room r) { for (int py = 5; py + 2 <= r.H - 6; py += 7) yield return py; }

    /// <summary>Trajet depart -> rangee du milieu -> colonne de l'autel. Rien de solide ne s'y pose.</summary>
    static void LayPath(Room r)
    {
        int midY = r.H / 2;
        Vector3Int a = r.Start;
        Vector3Int b = new Vector3Int(r.Start.x, midY, 0);
        Vector3Int c = new Vector3Int(r.Altar.x, midY, 0);
        Vector3Int d = new Vector3Int(r.Altar.x, r.Altar.y - 1, 0);
        AddPathLine(r, a, b);
        AddPathLine(r, b, c);
        AddPathLine(r, c, d);
    }

    static void AddPathLine(Room r, Vector3Int from, Vector3Int to)
    {
        Vector3Int step = new Vector3Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y), 0);
        Vector3Int cell = from;
        while (true)
        {
            if (!r.Solid.Contains(cell)) r.Path.Add(cell);
            if (cell == to) break;
            cell += step;
        }
    }

    /// <summary>Herbe de Room1 : surtout les variantes calmes, des massifs de fleurs, une allee pavee.</summary>
    static void PaintGardenGround(Room r)
    {
        float ox = r.Spec.Seed % 97;
        float oy = r.Spec.Seed % 89;

        for (int x = 0; x < r.W; x++)
        {
            for (int y = 0; y < r.H; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                TileBase tile;

                if (r.Path.Contains(cell))
                {
                    tile = GrassTile("Pavement " + r.Rng.Next(8));
                }
                else
                {
                    // bruit de Perlin -> les fleurs poussent en massifs, pas en confettis
                    float n = Mathf.PerlinNoise(x * 0.3f + ox, y * 0.3f + oy);
                    if (n > 0.62f && r.Rng.NextDouble() < 0.75)
                        tile = GrassTile("Flower " + r.Rng.Next(16));
                    else
                        tile = GrassTile(CalmGrass(r.Rng));
                }

                r.Ground.SetTile(cell, tile);
            }
        }
    }

    static string CalmGrass(System.Random rng)
    {
        int roll = rng.Next(100);
        if (roll < 45) return "0";
        if (roll < 60) return "1";
        if (roll < 72) return "5";
        if (roll < 82) return "6";
        return rng.Next(16).ToString();
    }

    /// <summary>
    /// Masque des BORDURES visibles d'une tuile (haut=1, droite=2, bas=4, gauche=8)
    /// -> indices utilisables dans "TX Tileset Stone Ground_N".
    /// Table obtenue en analysant les pixels de bord de chacune des 50 tuiles du
    /// tileset : une case a besoin d'une bordure du cote ou elle n'a pas de voisin.
    /// Le masque 0 (interieur plein) a 15 variantes -> c'est ce qui casse l'aplat.
    /// </summary>
    static readonly Dictionary<int, int[]> StoneByBorder = new Dictionary<int, int[]>
    {
        { 0,  new[] { 4, 9, 12, 14, 20, 28, 29, 30, 31, 32, 33, 35, 36, 43, 45 } },
        { 1,  new[] { 1, 6, 39, 40, 44 } },
        { 2,  new[] { 10, 15, 37, 42, 49 } },
        { 3,  new[] { 2, 7 } },
        { 4,  new[] { 17, 22, 34, 46, 47 } },
        { 5,  new[] { 25 } },
        { 6,  new[] { 18, 23 } },
        { 7,  new[] { 26 } },
        { 8,  new[] { 8, 13, 38, 41, 48 } },
        { 9,  new[] { 0, 5 } },
        { 10, new[] { 3, 11 } },
        { 11, new[] { 2, 7 } },   // pas de tuile exacte dans le tileset -> repli
        { 12, new[] { 16, 21 } },
        { 13, new[] { 24 } },
        { 14, new[] { 19, 27 } },
        { 15, new[] { 26 } },     // idem
    };

    /// <summary>
    /// Dallage de crypte en autotile. Les cases de bordure comptent comme sol : la dalle
    /// va jusqu'au liseré du mur. Seuls les piliers creent des bords de dalle.
    /// </summary>
    static void PaintCryptGround(Room r)
    {
        HashSet<Vector3Int> floor = new HashSet<Vector3Int>();
        for (int x = 0; x < r.W; x++)
            for (int y = 2; y < r.H; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!r.Obstacles.Contains(cell)) floor.Add(cell);
            }

        for (int x = 0; x < r.W; x++)
        {
            for (int y = 0; y < r.H; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                int mask = 0;
                if (floor.Contains(cell))
                {
                    if (!floor.Contains(cell + Vector3Int.up))    mask |= 1;
                    if (!floor.Contains(cell + Vector3Int.right)) mask |= 2;
                    if (!floor.Contains(cell + Vector3Int.down))  mask |= 4;
                    if (!floor.Contains(cell + Vector3Int.left))  mask |= 8;
                }
                // sinon : case sous un pilier ou sous la facade -> tuile pleine, jamais vue

                int[] choices;
                if (!StoneByBorder.TryGetValue(mask, out choices) || choices.Length == 0)
                    choices = StoneByBorder[0];

                r.Ground.SetTile(cell, StoneTile(choices[r.Rng.Next(choices.Length)]));
            }
        }
    }

    static readonly Color CryptGroundTint = new Color(0.70f, 0.74f, 0.92f, 1f);
    static readonly Color CryptWallTint = new Color(0.62f, 0.66f, 0.84f, 1f);
    static readonly Color CryptPropTint = new Color(0.80f, 0.84f, 1.00f, 1f);

    /// <summary>La crypte reprend la meme pierre mais refroidie : meme set, lecture tres differente.</summary>
    static void ApplyThemeTint(Room r)
    {
        bool crypt = r.Spec.Biome == Theme.Crypt;
        r.Ground.color = crypt ? CryptGroundTint : Color.white;
        r.Walls.color = crypt ? CryptWallTint : Color.white;
    }

    // =====================================================================
    //  DECOR
    //  Pieces maitresses posees a la main (coordonnees en cases), puis petit
    //  decor au sol disperse. Regles :
    //   - rien de solide sur le trajet, pres du depart, sur l'autel ou un ennemi
    //   - deco solide sur le calque Walls : le joueur ET les squelettes la contournent
    //   - petit decor (herbe, fleurs, cailloux) sous les personnages, sans collider
    // =====================================================================

    static string Prop(string name) { return PrefabDir + "/Props/PF Props - " + name + ".prefab"; }
    static string Plant(string name) { return PrefabDir + "/Plant/PF Plant - " + name + ".prefab"; }

    static Vector3 CellCenter(Room r, Vector3Int cell) { return r.Ground.GetCellCenterWorld(cell); }

    /// <summary>Les prefabs Cainos ont leur pivot en bas : on pose le pied dans la case.</summary>
    static Vector3 CellFoot(Room r, Vector3Int cell, float lift)
    {
        return r.Ground.CellToWorld(cell) + new Vector3(0.5f, lift, 0f);
    }

    static void Reserve(Room r, Vector3Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius)
                    r.Taken.Add(center + new Vector3Int(dx, dy, 0));
    }

    static IEnumerable<Vector3Int> Footprint(Vector3Int cell, int width, int height)
    {
        int x0 = cell.x - (width - 1) / 2;
        for (int x = x0; x < x0 + width; x++)
            for (int y = cell.y; y < cell.y + height; y++)
                yield return new Vector3Int(x, y, 0);
    }

    /// <summary>Piece de decor solide : refusee si son emprise n'est pas libre.</summary>
    static GameObject PlaceSolid(Room r, string prefabPath, int x, int y, int width = 1, int height = 1)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);
        List<Vector3Int> cells = Footprint(cell, width, height).ToList();
        if (cells.Any(c => !r.IsFree(c)))
        {
            Debug.LogWarning("[LevelBuilder] " + r.Spec.SceneName + " : place prise pour "
                             + System.IO.Path.GetFileNameWithoutExtension(prefabPath) + " en " + cell);
            return null;
        }

        // emprise paire -> on centre le prefab entre les deux cases
        Vector3 pos = CellFoot(r, cell, 0.1f);
        if (width % 2 == 0) pos.x += 0.5f;

        GameObject go = Spawn(r, prefabPath, pos);
        if (go == null) return null;

        foreach (Rigidbody2D rb in go.GetComponentsInChildren<Rigidbody2D>(true))
            rb.bodyType = RigidbodyType2D.Static; // pas de tonneau pousse dans le passage
        SetLayerRecursive(go.transform, WallsLayer);

        foreach (Vector3Int c in cells) r.Taken.Add(c);
        return go;
    }

    /// <summary>Petit decor au sol : sans collider, dessine sous les personnages.</summary>
    static GameObject PlaceFlat(Room r, string prefabPath, Vector3 position)
    {
        GameObject go = Spawn(r, prefabPath, position);
        if (go == null) return null;

        foreach (Collider2D col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (Rigidbody2D rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.bodyType = RigidbodyType2D.Static;
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -6; // au-dessus des tilemaps (-10/-9), sous l'autel (-5) et les persos
        }
        return go;
    }

    static GameObject Spawn(Room r, string prefabPath, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[LevelBuilder] Prefab introuvable : " + prefabPath);
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, r.Decor);
        go.transform.position = position;

        if (r.Spec.Biome == Theme.Crypt)
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color *= CryptPropTint;

        return go;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }

    /// <summary>
    /// Jardin (24 x 14). Muret en x 11-12, passage en y 6-8, allee : x=2 puis y=7 puis x=21.
    /// A gauche : puits, banc, arbres et un coin de reserve (caisses, jarres, tonneau).
    /// A droite : statue et pilier runique qui annoncent l'autel, arbres, coffre.
    /// </summary>
    static void DecorateGarden(Room r)
    {
        int gx = r.W / 2 - 1;            // muret : gx, gx + 1
        int gapMid = r.H / 2;

        // --- salle de gauche ---
        PlaceSolid(r, Plant("Tree 02"), 2, r.H - 4, 3, 3);
        PlaceSolid(r, Plant("Tree 01"), gx - 2, r.H - 3, 3, 2);
        PlaceSolid(r, Prop("Well 01"), 5, gapMid + 2, 2, 2);
        PlaceSolid(r, Prop("Stone Bench 01 S"), 5, 4, 2, 1);
        PlaceSolid(r, Prop("Road Sign 01 E"), 4, gapMid + 1);

        // coin de reserve contre le muret
        PlaceSolid(r, Prop("Crate 01"), gx - 2, 2, 1, 2);
        PlaceSolid(r, Prop("Crate 02"), gx - 1, 2);
        PlaceSolid(r, Prop("Barrel 01"), gx - 3, 3);
        PlaceSolid(r, Prop("Pot 01"), gx - 3, 2);
        PlaceSolid(r, Prop("Pot 03"), gx - 4, 2);

        // --- passage du muret : deux lanternes de chaque cote ---
        PlaceSolid(r, Prop("Stone Lantern 01"), gx - 1, gapMid + 2);
        PlaceSolid(r, Prop("Stone Lantern 01"), gx - 1, gapMid - 2);
        PlaceSolid(r, Prop("Stone Lantern 01"), gx + 2, gapMid + 2);
        PlaceSolid(r, Prop("Stone Lantern 01"), gx + 2, gapMid - 2);

        // --- salle de droite ---
        PlaceSolid(r, Prop("Statue 01"), r.Altar.x - 4, r.Altar.y, 1, 2);
        PlaceSolid(r, Prop("Rune Pillar X3"), r.Altar.x - 2, r.Altar.y, 1, 2);
        PlaceSolid(r, Plant("Tree 03"), r.W - 3, 3, 3, 3);
        PlaceSolid(r, Plant("Tree 01"), gx + 4, 3, 3, 2);
        PlaceSolid(r, Prop("Chest 01"), r.Altar.x - 3, gapMid - 2);
        PlaceSolid(r, Prop("Stone Bench 01 W"), gx + 6, gapMid + 1, 1, 2);
        PlaceSolid(r, Prop("Pot 02"), gx + 2, 2);

        // --- haies basses le long du mur du haut (pas de collider : on passe derriere) ---
        PlaceHedge(r, Plant("Bush 04"), 6, r.H - 2);
        PlaceHedge(r, Plant("Bush 02"), 7, r.H - 2);
        PlaceHedge(r, Plant("Bush 05"), gx + 3, r.H - 2);
        PlaceHedge(r, Plant("Bush 03"), gx + 5, r.H - 2);
        PlaceHedge(r, Plant("Bush 01"), 1, gapMid - 1);
        PlaceHedge(r, Plant("Bush 02"), r.W - 6, 2);
    }

    /// <summary>Buisson decoratif : pas d'obstacle, mais on ne pose rien d'autre dessus.</summary>
    static void PlaceHedge(Room r, string prefabPath, int x, int y)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);
        if (!r.IsFree(cell)) return;
        GameObject go = Spawn(r, prefabPath, CellFoot(r, cell, 0.05f));
        if (go == null) return;
        foreach (Collider2D col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        r.Taken.Add(cell);
    }

    /// <summary>
    /// Crypte (30 x 20). Piliers en x 5/11/17/23 (2 de large), y 5-7 et 12-14.
    /// Allee : x=2 puis y=10 puis x=27. En haut a gauche un petit cimetiere,
    /// des sarcophages le long des murs, des lanternes au pied de chaque pilier,
    /// et l'autel encadre d'une statue et de piliers runiques.
    /// </summary>
    static void DecorateCrypt(Room r)
    {
        string[] graves = { Prop("Gravestone 01"), Prop("Gravestone 02"), Prop("Gravestone 03") };
        int g = 0;
        for (int x = 3; x <= 9; x += 2)
        {
            PlaceSolid(r, graves[g++ % graves.Length], x, r.H - 3);
            PlaceSolid(r, graves[g++ % graves.Length], x, r.H - 5);
        }

        // lanternes : devant les piliers du bas, derriere ceux du haut
        int[] rows = PillarRows(r).ToArray();
        foreach (int px in PillarColumns(r))
        {
            PlaceSolid(r, Prop("Stone Lantern 01"), px - 1, rows[0]);
            if (rows.Length > 1) PlaceSolid(r, Prop("Stone Lantern 01"), px + 2, rows[1]);
        }

        // sarcophages
        PlaceSolid(r, Prop("Stone Coffin 01 H"), 14, r.H - 3, 2, 1);
        PlaceSolid(r, Prop("Stone Coffin 01 H"), 20, r.H - 3, 2, 1);
        PlaceSolid(r, Prop("Stone Coffin 01 V"), r.W - 2, 5, 1, 2);
        PlaceSolid(r, Prop("Stone Coffin 01 V"), 1, 12, 1, 2);

        // urnes au pied des sarcophages du haut
        PlaceSolid(r, Prop("Pot 01"), 16, r.H - 3);
        PlaceSolid(r, Prop("Pot 03"), 22, r.H - 3);

        // autel : statue + piliers runiques
        PlaceSolid(r, Prop("Statue 01"), r.Altar.x - 4, r.Altar.y, 1, 2);
        PlaceSolid(r, Prop("Rune Pillar X3"), r.Altar.x - 2, r.Altar.y + 1, 1, 2);
        PlaceSolid(r, Prop("Rune Pillar X2"), r.W - 2, r.Altar.y - 4, 1, 2);

        // ruines et tresor
        PlaceSolid(r, Prop("Rune Pillar Broken"), 1, 15);
        PlaceSolid(r, Prop("Rune Pillar Broken"), 14, 2);
        PlaceSolid(r, Prop("Stone 07"), 20, 2, 2, 1);
        PlaceSolid(r, Prop("Chest 01"), r.W - 3, 2);
        PlaceSolid(r, Prop("Stone Cube 01"), 8, 2);
    }

    static readonly string[] Tufts =
    {
        "Grass 01", "Grass 02", "Grass 03", "Grass 04", "Grass 05", "Grass 06", "Grass 07", "Grass 08",
        "Grass 09", "Grass 10", "Grass 11", "Grass 12", "Grass 13", "Grass 14", "Grass 15",
    };

    static void ScatterGarden(Room r)
    {
        Scatter(r, Tufts.Select(Plant).ToArray(), 45, false);
        Scatter(r, new[] { Plant("Flower 01") }, 18, true);
        Scatter(r, new[] { Prop("Stone 01"), Prop("Stone 02"), Prop("Stone 03") }, 8, true);
    }

    static void ScatterCrypt(Room r)
    {
        Scatter(r, new[] { Prop("Brick 01"), Prop("Brick 02"), Prop("Brick 03") }, 18, false);
        Scatter(r, new[] { Prop("Stone 01"), Prop("Stone 02"), Prop("Stone 03"), Prop("Stone 04") }, 16, false);
        Scatter(r, new[] { Plant("Grass 05"), Plant("Grass 07"), Plant("Grass 09"), Plant("Grass 13") }, 16, true);
    }

    /// <summary>Petit decor au sol, une piece max par case, decale aleatoirement dans la case.</summary>
    static void Scatter(Room r, string[] prefabs, int count, bool avoidPath)
    {
        HashSet<Vector3Int> used = new HashSet<Vector3Int>();
        int placed = 0;
        for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
        {
            Vector3Int cell = new Vector3Int(r.Rng.Next(1, r.W - 1), r.Rng.Next(2, r.H - 1), 0);
            if (r.Solid.Contains(cell) || used.Contains(cell)) continue;
            if (r.Taken.Contains(cell) && !r.Path.Contains(cell)) continue;
            if (avoidPath && r.Path.Contains(cell)) continue;

            float jx = (float)(r.Rng.NextDouble() - 0.5) * 0.6f;
            float jy = (float)r.Rng.NextDouble() * 0.6f;
            Vector3 pos = r.Ground.CellToWorld(cell) + new Vector3(0.5f + jx, 0.1f + jy, 0f);

            if (PlaceFlat(r, prefabs[r.Rng.Next(prefabs.Length)], pos) != null)
            {
                used.Add(cell);
                placed++;
            }
        }
    }

    // =====================================================================
    //  ENNEMIS
    // =====================================================================

    static List<Vector3Int> PickEnemyCells(Room r)
    {
        List<Vector3Int> free = new List<Vector3Int>();
        for (int x = 1; x < r.W - 1; x++)
        {
            for (int y = 2; y < r.H - 1; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!r.InPlay(cell) || r.Solid.Contains(cell) || r.Taken.Contains(cell)) continue;
                // pas d'ennemi colle au point de depart
                if (Mathf.Abs(x - r.Start.x) + Mathf.Abs(y - r.Start.y) < 8) continue;
                free.Add(cell);
            }
        }

        // melange deterministe : meme seed => meme niveau a chaque regeneration
        System.Random rng = new System.Random(r.Spec.Seed);
        List<Vector3Int> shuffled = free.OrderBy(c => rng.Next()).ToList();

        List<Vector3Int> picked = new List<Vector3Int>();
        foreach (Vector3Int cell in shuffled)
        {
            if (picked.Count >= r.Spec.EnemyCount) break;
            // espacer les ennemis pour eviter un paquet ingerable
            if (picked.Any(p => Mathf.Abs(p.x - cell.x) + Mathf.Abs(p.y - cell.y) < 7)) continue;
            picked.Add(cell);
        }
        // si l'espacement est trop strict pour la taille de salle, on complete
        foreach (Vector3Int cell in shuffled)
        {
            if (picked.Count >= r.Spec.EnemyCount) break;
            if (!picked.Contains(cell)) picked.Add(cell);
        }
        return picked;
    }

    static void PlaceEnemy(Room r, Transform enemy, Vector3Int cell)
    {
        Vector3 w = CellCenter(r, cell);
        enemy.position = new Vector3(w.x, w.y, enemy.position.z);
        Reserve(r, cell, 1);
    }

    static void ApplyDifficulty(GameObject enemy, LevelSpec spec)
    {
        EnemyStats stats = enemy.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.maxHealth = spec.EnemyHealth;
            EditorUtility.SetDirty(stats);
        }

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.moveSpeed = spec.EnemySpeed;
            ai.touchDamage = spec.EnemyDamage;
            ai.detectionRange = spec.DetectionRange;
            ai.damageCooldown = spec.DamageCooldown;
            EditorUtility.SetDirty(ai);
        }
    }

    // =====================================================================
    //  VICTOIRE
    // =====================================================================

    /// <summary>
    /// Victory = copie de GameOver, mais avec son propre fond et un vrai message de felicitations.
    /// Avant, le fond restait "gameover.jpg" (le texte GAME OVER est DANS l'image) :
    /// gagner les 3 niveaux affichait donc un Game Over.
    /// </summary>
    static void BuildVictoryScene()
    {
        if (!System.IO.File.Exists(SourceGameOver))
        {
            Debug.LogWarning("[LevelBuilder] GameOver.unity introuvable — Victory non generee. " +
                             "L'autel de Room3 pointera vers une scene absente.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VictoryScene) != null)
            AssetDatabase.DeleteAsset(VictoryScene);

        if (!AssetDatabase.CopyAsset(SourceGameOver, VictoryScene))
        {
            Debug.LogError("[LevelBuilder] Copie de GameOver vers Victory echouee.");
            return;
        }
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(VictoryScene, OpenSceneMode.Single);

        GameOverScreen screen = Object.FindObjectOfType<GameOverScreen>();
        if (screen != null)
        {
            screen.restartFromFirstLevel = true;
            EditorUtility.SetDirty(screen);
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LevelBuilder] Pas de Canvas dans Victory.");
            return;
        }

        // fond : ruines brumeuses au lieu de l'image "GAME OVER"
        Image background = canvas.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "BackgroundImage");
        Sprite victorySprite = AssetDatabase.LoadAssetAtPath<Sprite>(VictoryBackground);
        if (background != null && victorySprite != null)
        {
            background.sprite = victorySprite;
            background.color = Color.white;
            EditorUtility.SetDirty(background);
        }
        else
        {
            Debug.LogWarning("[LevelBuilder] Fond de victoire non applique (BackgroundImage ou " + VictoryBackground + " introuvable).");
        }

        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        int afterBackground = background != null ? background.transform.GetSiblingIndex() + 1 : 0;

        // bandeau sombre derriere le texte pour la lisibilite
        GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetSiblingIndex(afterBackground);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchoredPosition = new Vector2(0f, 120f);
        panelRect.sizeDelta = new Vector2(980f, 300f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        Color gold = new Color(1f, 0.82f, 0.25f, 1f);
        CreateText(canvas.transform, "TitleShadow", "FÉLICITATIONS !", font, 96, new Color(0.25f, 0.12f, 0f, 0.9f),
                   new Vector2(5f, 165f), new Vector2(1200f, 130f), FontStyles.Bold);
        CreateText(canvas.transform, "Title", "FÉLICITATIONS !", font, 96, gold,
                   new Vector2(0f, 170f), new Vector2(1200f, 130f), FontStyles.Bold);
        CreateText(canvas.transform, "Subtitle",
                   "Tu as terminé les 3 niveaux !\nLes squelettes sont vaincus, le royaume est sauvé.",
                   font, 34, Color.white, new Vector2(0f, 45f), new Vector2(1100f, 120f), FontStyles.Normal);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, VictoryScene);
        Debug.Log("[LevelBuilder] Victory generee : fond de victoire + message de felicitations.");
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, TMP_FontAsset font, float size,
                                      Color color, Vector2 position, Vector2 boxSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false; // ne doit pas bloquer les clics sur les boutons
        return tmp;
    }

    // =====================================================================
    //  PROJET
    // =====================================================================

    /// <summary>
    /// Les prefabs Cainos referencent des scripts du pack qui ont ete supprimes
    /// (escaliers, autel, piliers runiques...) -> "The referenced script is missing"
    /// en boucle dans la console. On retire ces composants vides des prefabs.
    /// </summary>
    [MenuItem("Tools/RPG/Nettoyer les scripts manquants (Cainos)")]
    public static void CleanMissingScripts()
    {
        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                removed += count;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        if (removed > 0)
            Debug.Log("[LevelBuilder] " + removed + " script(s) manquant(s) retire(s) des prefabs Cainos.");
    }

    static void RemoveObsoleteScenes()
    {
        foreach (string path in ObsoleteScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            AssetDatabase.DeleteAsset(path);
            Debug.Log("[LevelBuilder] Ancienne scene supprimee : " + path);
        }
    }

    /// <summary>Ordonne les scenes et supprime les doublons.</summary>
    static void FixBuildSettings()
    {
        List<string> wanted = new List<string> { ScenesDir + "/MainMenu.unity", SourceLevel };
        wanted.AddRange(Specs.Select(s => ScenesDir + "/" + s.SceneName + ".unity"));
        wanted.Add(VictoryScene);
        wanted.Add(ScenesDir + "/GameOver.unity");

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        HashSet<string> seen = new HashSet<string>();

        foreach (string path in wanted)
        {
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("[LevelBuilder] Scene absente, ignoree : " + path); continue; }
            if (!seen.Add(path)) continue;
            list.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[LevelBuilder] Build Settings : " + string.Join(", ",
            list.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToArray()));
    }
}
