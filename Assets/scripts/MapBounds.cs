using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A poser sur la tilemap des murs. Empeche le joueur de "sortir" de la carte :
///  - la bordure du haut n'a qu'un fin liseré comme collider : les pieds s'arretent
///    dessus, mais la tete et le corps depassent dans le vide noir au-dessus.
///    On ajoute donc un mur invisible sous la bordure, a la hauteur du personnage.
///  - filet de securite : si le joueur se retrouve quand meme hors de la carte
///    (teleport, collision ratee...), on le ramene a l'interieur.
/// Calcule a partir des tuiles au lancement : marche aussi apres une regeneration des salles.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class MapBounds : MonoBehaviour
{
    [Tooltip("Hauteur visible du personnage au-dessus de ses pieds (en unites)")]
    public float headroom = 1.3f;

    [Tooltip("Marge gardee entre les pieds du joueur et le bord de la carte (filet de securite)")]
    public float clampMargin = 0.3f;

    private Rigidbody2D player;
    private Collider2D playerFeet;
    private Bounds mapBounds;

    void Awake()
    {
        Tilemap tilemap = GetComponent<Tilemap>();
        tilemap.CompressBounds();
        mapBounds = tilemap.localBounds;
        mapBounds.center = transform.TransformPoint(mapBounds.center);

        CreateTopBlocker();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<Rigidbody2D>();
            playerFeet = playerObj.GetComponent<Collider2D>();
        }
    }

    // Enfant sur le meme calque que les murs : bloque le joueur ET le raycast des ennemis.
    void CreateTopBlocker()
    {
        GameObject blocker = new GameObject("TopBoundary");
        blocker.layer = gameObject.layer;
        blocker.transform.SetParent(transform, false);
        blocker.transform.position = Vector3.zero;

        float top = mapBounds.max.y;
        BoxCollider2D box = blocker.AddComponent<BoxCollider2D>();
        box.size = new Vector2(mapBounds.size.x, headroom + 2f);
        box.offset = new Vector2(mapBounds.center.x, top - headroom + box.size.y / 2f);
    }

    void FixedUpdate()
    {
        if (player == null || playerFeet == null) return;

        // on teste les pieds (le collider), pas le pivot qui est au milieu du sprite
        Vector2 feet = playerFeet.bounds.center;
        float minX = mapBounds.min.x + clampMargin, maxX = mapBounds.max.x - clampMargin;
        float minY = mapBounds.min.y + clampMargin, maxY = mapBounds.max.y - clampMargin;
        if (feet.x >= minX && feet.x <= maxX && feet.y >= minY && feet.y <= maxY) return;

        Vector2 inside = new Vector2(Mathf.Clamp(feet.x, minX, maxX), Mathf.Clamp(feet.y, minY, maxY));
        player.position += inside - feet;
        player.velocity = Vector2.zero;
    }
}
