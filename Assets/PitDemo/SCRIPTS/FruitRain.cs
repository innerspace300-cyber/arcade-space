using UnityEngine;

// ARcade: the fruit collection's reward (FruitTracker) - every fruit pours
// down the picture like rain for a few seconds, over everything (the HUD
// included: the Default sorting layer, above the canvas's TopLayer), to a
// win jingle, with a score bonus.
public class FruitRain : MonoBehaviour
{
    public Sprite[] fruit;
    public float duration = 4.5f;
    public float perSecond = 28f;
    [Tooltip("Fall speed, picture pixels per second.")]
    public Vector2 fallSpeed = new Vector2(110f, 200f);
    [Tooltip("Size, in fruit pixels to a picture pixel.")]
    public Vector2 size = new Vector2(1f, 2f);
    public int sortingOrder = 250;

    Camera _cam;
    float _start, _spawned;

    /// Rain these on the running demo, with `win` playing and `bonus` points.
    public static void Play(Sprite[] fruit, AudioClip win, int bonus)
    {
        var demo = SpatialEmulator.Demo.PitDemoGame.Running;
        if (!demo || fruit == null || fruit.Length == 0) return;
        var rain = new GameObject("FRUIT RAIN").AddComponent<FruitRain>();
        rain.transform.SetParent(demo.transform, false);
        rain.fruit = fruit;
        PitAudio.PlayClip(win);
        if (bonus > 0) { PitScoreManager.Instance?.AddScore(bonus); TreasureChest.BonusPoints += bonus; }
    }

    void Start()
    {
        _cam = SpatialEmulator.Demo.PitDemoGame.HudCamera;   // the front layer, over the HUD
        _start = Time.time;
    }

    void Update()
    {
        if (!_cam) { Destroy(gameObject); return; }
        float age = Time.time - _start;
        if (age < duration)
        {
            _spawned += perSecond * Time.deltaTime;
            while (_spawned >= 1f) { _spawned -= 1f; Drop(); }
        }
        else if (transform.childCount == 0) Destroy(gameObject);
    }

    void Drop()
    {
        float pixel = _cam.orthographicSize * 2f / _cam.pixelHeight;
        float halfWidth = _cam.orthographicSize * _cam.aspect;
        var sprite = fruit[Random.Range(0, fruit.Length)];
        var go = new GameObject("Fruit");
        go.layer = LayerMask.NameToLayer(SpatialEmulator.Demo.PitDemoGame.HudLayer);
        go.transform.SetParent(transform, false);
        var p = _cam.transform.position;
        float scale = Mathf.Round(Random.Range(size.x, size.y));
        go.transform.position = new Vector3(p.x + Random.Range(-halfWidth, halfWidth),
            p.y + _cam.orthographicSize + 16f * pixel * scale, p.z + 2f);
        go.transform.localScale = Vector3.one * (pixel * sprite.pixelsPerUnit * scale);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = sortingOrder;
        var drop = go.AddComponent<FruitDrop>();
        drop.speed = Random.Range(fallSpeed.x, fallSpeed.y) * pixel;
        drop.bottom = p.y - _cam.orthographicSize - 20f * pixel * scale;
        drop.sway = Random.Range(4f, 10f) * pixel;
        drop.spin = Random.Range(-120f, 120f);
    }
}

// One fruit falling: down, swaying a little, turning, gone past the bottom.
public class FruitDrop : MonoBehaviour
{
    public float speed, bottom, sway, spin;
    float _x, _phase;

    void Start() { _x = transform.position.x; _phase = Random.value * 10f; }

    void Update()
    {
        var p = transform.position;
        p.y -= speed * Time.deltaTime;
        p.x = _x + Mathf.Sin(Time.time * 3f + _phase) * sway;
        transform.position = p;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (p.y < bottom) Destroy(gameObject);
    }
}
