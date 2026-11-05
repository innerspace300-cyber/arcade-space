using UnityEngine;

// ARcade: little things in the air round a torii gate, in its art's pixels -
// Sparks (the bamboo gate's neon green magic: specks rising out of it,
// twinkling and fading) or Bugs (the graveyard gate's flies: dark specks
// buzzing about it, never still) or Smoke (the death gate's, and its
// skeletons': dark grey smoke curling up off it, swelling and thinning, as if
// it had smouldered a thousand years) or Fog (level 1's return torii's: faint
// puffs sitting low in its doorway, from foot to foot, drifting a little in
// place) or Haze (smoke drifting out of a doorway, swelling as it thins) or
// Bursts (its neon sparks: every few seconds a handful shooting out of the
// doorway, arcing down, twinkling out). On the gate's renderer (LevelPortal's
// gate, ParallaxLevelBackground's standing gate props), drawn just over it.
public class GateMotes : MonoBehaviour
{
    public enum Kind { None, Sparks, Bugs, Smoke, Fog, Haze, Bursts }

    public Kind kind;
    public Color color = Color.white;
    public int count = 14;
    [Tooltip("Round its drawn body (a boss's), not its whole canvas.")]
    public bool fromBody;

    SpriteRenderer _gate;
    SpriteRenderer[] _motes;
    Vector2[] _from, _anchor, _vel;
    float _nextBurst;
    const float SparkFall = 30f, SparkRest = 0.6f;   // (its fall, gate pixels a second squared halved; how long it lies on the ground)
    float[] _born, _life, _phase, _speed;
    float _pixel;
    static Sprite s_dot;

    public static GateMotes Add(SpriteRenderer gate, Kind kind, Color color)
    {
        if (!gate || kind == Kind.None) return null;
        var motes = gate.gameObject.AddComponent<GateMotes>();
        motes.kind = kind;
        motes.color = color;
        motes.count = kind == Kind.Bugs ? 9 : kind == Kind.Smoke ? 20 : kind == Kind.Fog ? 12 : kind == Kind.Haze ? 12 : kind == Kind.Bursts ? 16 : 16;
        return motes;
    }

    void Start()
    {
        _gate = GetComponent<SpriteRenderer>();
        if (!_gate || !_gate.sprite) { enabled = false; return; }
        if (!s_dot) s_dot = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _pixel = Mathf.Abs(transform.lossyScale.x) / _gate.sprite.pixelsPerUnit;   // one of the gate art's pixels, world units
        _motes = new SpriteRenderer[count];
        _from = new Vector2[count]; _anchor = new Vector2[count]; _vel = new Vector2[count];
        _born = new float[count]; _life = new float[count]; _phase = new float[count]; _speed = new float[count];
        for (int i = 0; i < count; i++)
        {
            bool puff = kind == Kind.Smoke || kind == Kind.Fog || kind == Kind.Haze;
            var go = new GameObject(kind == Kind.Bugs ? "Bug" : puff ? kind.ToString() : "Spark");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform.parent, true);   // (not scaled with the gate)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = puff ? FoeFx.GlowSprite : s_dot;
            sr.sortingLayerID = _gate.sortingLayerID;
            sr.sortingOrder = kind == Kind.Smoke ? _gate.sortingOrder - 1 : _gate.sortingOrder + 1;   // (smoke behind it: its outline stays crisp; fog and haze over it)
            go.transform.localScale = Vector3.one;
            SetWorldSize(go.transform, _pixel);
            _motes[i] = sr;
            _phase[i] = Random.value * 10f;
            _speed[i] = Random.Range(0.7f, 1.3f);
            Spawn(i, Time.time - Random.value * 2f);
            _anchor[i] = Anchor() - (Vector2)transform.position;
            if (kind == Kind.Bursts) { _born[i] = -100f; sr.color = Color.clear; }   // (unseen till the first burst)
        }
        if (kind == Kind.Bursts) _nextBurst = Time.time + Random.Range(0.6f, 1.4f);
    }

    void OnDestroy()
    {
        if (_motes == null) return;
        foreach (var m in _motes) if (m) Destroy(m.gameObject);
    }

    Bounds Area => fromBody ? RoadFoe.Drawn(_gate) : _gate.bounds;

    void SetWorldSize(Transform t, float size)
    {
        var parent = t.parent ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size / parent.x, size / parent.y, 1f);
    }

    // Somewhere in the gate's opening and round its posts, above the ground.
    Vector2 InGate()
    {
        var b = Area;
        return new Vector2(Random.Range(b.min.x + b.size.x * 0.15f, b.max.x - b.size.x * 0.15f),
                           Random.Range(b.min.y + b.size.y * 0.05f, b.min.y + b.size.y * 0.7f));
    }

    Vector2 Anchor()
    {
        var b = Area;
        return new Vector2(Random.Range(b.min.x + b.size.x * 0.1f, b.max.x - b.size.x * 0.1f),
                           Random.Range(b.min.y + b.size.y * 0.35f, b.max.y));
    }

    // Fog: low in its doorway, from foot to foot (its middle 40%, the bottom 14%); haze: inside its doorway (its middle, low).
    Vector2 AtFeet()
    {
        var b = Area;
        return new Vector2(Random.Range(b.center.x - b.size.x * 0.17f, b.center.x + b.size.x * 0.17f), b.min.y + b.size.y * Random.Range(0.02f, 0.12f));
    }
    Vector2 InDoorway()
    {
        var b = Area;
        return new Vector2(b.center.x + b.size.x * Random.Range(-0.07f, 0.07f), b.min.y + b.size.y * Random.Range(0.12f, 0.4f));
    }

    void Spawn(int i, float now)
    {
        _from[i] = (kind == Kind.Fog ? AtFeet() : kind == Kind.Haze ? InDoorway() : InGate()) - (Vector2)transform.position;   // (from it, wherever it goes)
        _born[i] = now;
        _life[i] = Random.Range(1.2f, 2.6f);
    }

    void Update()
    {
        if (_motes == null) return;
        var b = Area;
        float z = _gate.transform.position.z - 0.003f;
        var here = (Vector2)transform.position;   // (they move with it)
        for (int i = 0; i < _motes.Length; i++)
        {
            var m = _motes[i];
            if (!m) continue;
            if (kind == Kind.Smoke)
            {
                float age = Time.time - _born[i];
                if (age > _life[i] * 1.6f) { Spawn(i, Time.time); age = 0f; }
                float k = age / (_life[i] * 1.6f);
                // Billowing up off it: rising, curling side to side (a slow sway and a quicker eddy),
                // drifting with the breeze, swelling big and thinning as it goes, each puff turning.
                float curl = Mathf.Sin(age * 1.3f + _phase[i]) * 6f + Mathf.Sin(age * 3.7f + _phase[i] * 2f) * 2f;
                var at = here + _from[i] + new Vector2((curl + age * 3f) * _pixel, age * b.size.y * 0.16f * _speed[i]);
                m.transform.position = new Vector3(at.x, at.y + b.size.y * 0.2f, z + 0.006f);
                SetWorldSize(m.transform, _pixel * Mathf.Lerp(2f, 6.5f, Mathf.Sqrt(k)) * _speed[i]);
                m.transform.rotation = Quaternion.Euler(0f, 0f, (age * 40f + _phase[i] * 30f) * (_speed[i] > 1f ? 1f : -1f));
                m.color = new Color(color.r, color.g, color.b, color.a * Mathf.Sin(k * Mathf.PI) * (0.65f + 0.15f * Mathf.Sin(age * 5f + _phase[i])));
            }
            else if (kind == Kind.Fog)
            {
                float age = Time.time - _born[i];
                if (age > _life[i] * 2.5f) { Spawn(i, Time.time); age = 0f; }
                float k = age / (_life[i] * 2.5f);
                // Hanging low in the doorway, drifting a little to and fro in place, hardly rising, swelling a little.
                float drift = Mathf.Sin(age * 0.9f + _phase[i]) * 3f + Mathf.Sin(age * 2.3f + _phase[i] * 2f);
                var at = here + _from[i] + new Vector2(drift * _pixel, age * _pixel * 0.5f);
                m.transform.position = new Vector3(at.x, at.y, z - 0.002f);
                SetWorldSize(m.transform, _pixel * Mathf.Lerp(7f, 11f, k) * _speed[i]);
                m.color = new Color(color.r, color.g, color.b, color.a * Mathf.Sin(k * Mathf.PI));
            }
            else if (kind == Kind.Haze)
            {
                float age = Time.time - _born[i];
                if (age > _life[i] * 1.8f) { Spawn(i, Time.time); age = 0f; }
                float k = age / (_life[i] * 1.8f);
                // Drifting out of the doorway toward her and up, spreading to the sides, swelling, thinning.
                float side = Mathf.Sin(_phase[i] * 3f) * age * 7f + Mathf.Sin(age * 2.1f + _phase[i]) * 2f;
                var at = here + _from[i] + new Vector2(side * _pixel, age * b.size.y * 0.09f * _speed[i]);
                m.transform.position = new Vector3(at.x, at.y, z - 0.002f);
                SetWorldSize(m.transform, _pixel * Mathf.Lerp(3f, 9f, Mathf.Sqrt(k)) * _speed[i]);
                m.transform.rotation = Quaternion.Euler(0f, 0f, age * 25f + _phase[i] * 30f);
                m.color = new Color(color.r, color.g, color.b, color.a * Mathf.Sin(k * Mathf.PI) * (0.7f + 0.2f * Mathf.Sin(age * 4f + _phase[i])));
            }
            else if (kind == Kind.Bursts)
            {
                // Every second or two, all of them at once out of the doorway: up and out to either side, falling, fine
                // specks (a gate pixel as they twinkle), fading as they go.
                if (i == 0 && Time.time >= _nextBurst)
                {
                    _nextBurst = Time.time + Random.Range(1.1f, 2f);
                    for (int j = 0; j < _motes.Length; j++)
                    {
                        _from[j] = InDoorway() - here;
                        _born[j] = Time.time;
                        _vel[j] = new Vector2(Random.Range(-1f, 1f) * 22f, Random.Range(18f, 34f));   // (gate pixels a second)
                        // (when it comes down on the ground - its gate's feet - where it lies a moment, fading)
                        float above = (here.y + _from[j].y - b.min.y) / _pixel;
                        _life[j] = (_vel[j].y + Mathf.Sqrt(_vel[j].y * _vel[j].y + 4f * SparkFall * above)) / (2f * SparkFall);
                    }
                }
                float age = Time.time - _born[i];
                float landed = age - _life[i];
                if (landed > SparkRest) { m.color = Color.clear; continue; }
                float flight = Mathf.Min(age, _life[i]);
                var at = here + _from[i] + (_vel[i] * flight + new Vector2(0f, -SparkFall) * flight * flight) * _pixel;
                at.y = Mathf.Max(at.y, b.min.y);
                m.transform.position = new Vector3(at.x, at.y, z);
                float twinkle = landed > 0f ? 0.5f : 0.6f + 0.4f * Mathf.Sin(Time.time * 22f + _phase[i] * 7f);
                float fade = landed > 0f ? 1f - landed / SparkRest : 1f;
                m.color = new Color(Mathf.Lerp(color.r, 1f, 0.3f * twinkle), Mathf.Lerp(color.g, 1f, 0.3f * twinkle), Mathf.Lerp(color.b, 1f, 0.3f * twinkle), color.a * fade);
                SetWorldSize(m.transform, _pixel * (twinkle > 0.85f ? 1f : 0.6f));   // (fine specks, a little bigger as they twinkle)
            }
            else if (kind == Kind.Sparks)
            {
                float age = Time.time - _born[i];
                if (age > _life[i]) { Spawn(i, Time.time); age = 0f; }
                float k = age / _life[i];
                // Rising, drifting side to side, twinkling, fading out as it goes.
                var at = here + _from[i] + new Vector2(Mathf.Sin(age * 3f + _phase[i]) * _pixel * 2f, age * b.size.y * 0.22f * _speed[i]);
                m.transform.position = new Vector3(at.x, at.y, z);
                float twinkle = 0.6f + 0.4f * Mathf.Sin(Time.time * 18f + _phase[i] * 7f);
                float a = Mathf.Sin(k * Mathf.PI) * twinkle * color.a;   // (its colour's own opacity too)
                m.color = new Color(Mathf.Lerp(color.r, 1f, 0.35f * twinkle), Mathf.Lerp(color.g, 1f, 0.35f * twinkle), Mathf.Lerp(color.b, 1f, 0.35f * twinkle), a);
                SetWorldSize(m.transform, _pixel * (twinkle > 0.9f ? 1.6f : 1f));
            }
            else
            {
                // Buzzing about a spot, darting to a new one now and then.
                if (Random.value < Time.deltaTime * 0.4f) _anchor[i] = Anchor() - here;
                float t = Time.time * 6f * _speed[i] + _phase[i];
                var at = here + _anchor[i] + new Vector2(Mathf.Sin(t) + Mathf.Sin(t * 2.7f) * 0.5f, Mathf.Sin(t * 1.9f) + Mathf.Cos(t * 3.3f) * 0.4f) * _pixel * 4f;
                at += Random.insideUnitCircle * _pixel * 0.6f;   // (a little jitter)
                var p = m.transform.position;
                var next = Vector2.Lerp(new Vector2(p.x, p.y), at, Mathf.Clamp01(Time.deltaTime * 8f));
                m.transform.position = new Vector3(next.x, next.y, z);
                m.color = color;
            }
        }
    }
}
