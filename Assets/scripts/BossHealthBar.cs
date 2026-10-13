using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grande barre de vie du boss en haut de l'ecran, avec son nom.
/// Construit son propre Canvas : rien a cabler dans la scene.
/// Apparait quand le boss repere le joueur, disparait a sa mort.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class BossHealthBar : MonoBehaviour
{
    public string bossName = "Dark Knight";
    public Color fillColor = new Color(0.65f, 0.05f, 0.12f, 1f);
    public Color lagColor = new Color(1f, 0.85f, 0.4f, 1f); // traine derriere le rouge a chaque coup
    public float width = 700f;
    public float height = 22f;

    private EnemyStats stats;
    private BossAI ai;
    private GameObject root;
    private RectTransform fill;
    private RectTransform lag;
    private float lagPercent = 1f;

    void Start()
    {
        stats = GetComponent<EnemyStats>();
        ai = GetComponent<BossAI>();
        BuildUI();
        root.SetActive(false);
    }

    void Update()
    {
        bool alive = stats.currentHealth > 0;
        bool show = alive && (ai == null || ai.IsEngaged);
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;

        float percent = Mathf.Clamp01((float)stats.currentHealth / stats.maxHealth);
        lagPercent = Mathf.MoveTowards(lagPercent, percent, Time.deltaTime * 0.5f);
        if (lagPercent < percent) lagPercent = percent;

        fill.sizeDelta = new Vector2(width * percent, height);
        lag.sizeDelta = new Vector2(width * lagPercent, height);
    }

    void OnDisable()
    {
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }

    void BuildUI()
    {
        root = new GameObject("BossHealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // cadre sombre, ancre en haut au centre
        RectTransform frame = NewImage("Frame", root.transform, new Color(0f, 0f, 0f, 0.75f));
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.anchoredPosition = new Vector2(0f, -40f);
        frame.sizeDelta = new Vector2(width + 8f, height + 8f);

        lag = NewBar("Lag", frame, lagColor);
        fill = NewBar("Fill", frame, fillColor);

        GameObject label = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(frame, false);
        RectTransform lr = (RectTransform)label.transform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -4f);
        lr.sizeDelta = new Vector2(width, 40f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = bossName;
        text.fontSize = 30;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.95f, 0.85f, 0.7f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    RectTransform NewBar(string name, RectTransform frame, Color color)
    {
        RectTransform bar = NewImage(name, frame, color);
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
        bar.pivot = new Vector2(0f, 0.5f); // retrecit vers la gauche
        bar.anchoredPosition = new Vector2(4f, 0f);
        bar.sizeDelta = new Vector2(width, height);
        return bar;
    }

    static RectTransform NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return (RectTransform)go.transform;
    }
}
