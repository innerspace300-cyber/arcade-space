using System.Collections.Generic;
using UnityEngine;

// ARcade: burning embers along a gate's black outline (the death gate: as if
// it had smouldered a thousand years) - its ember overlays (the gate's own
// size and pivot, a few spots each) drawn over it, each set flickering on its
// own, and a soft orange neon glow round them all, breathing. On the gate's
// renderer (LevelPortal's gate, ParallaxLevelBackground's standing gate).
public class GateEmbers : MonoBehaviour
{
    public Sprite[] sets;
    public Sprite glow;

    SpriteRenderer _gate, _glow;
    readonly List<SpriteRenderer> _sets = new List<SpriteRenderer>();
    float[] _phase;

    public static GateEmbers Add(SpriteRenderer gate, Sprite[] sets, Sprite glow)
    {
        if (!gate || sets == null || sets.Length == 0) return null;
        var embers = gate.gameObject.AddComponent<GateEmbers>();
        embers.sets = sets;
        embers.glow = glow;
        return embers;
    }

    SpriteRenderer Piece(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = _gate.sharedMaterial;
        sr.sortingLayerID = _gate.sortingLayerID;
        sr.sortingOrder = order;
        sr.maskInteraction = _gate.maskInteraction;
        return sr;
    }

    void Start()
    {
        _gate = GetComponent<SpriteRenderer>();
        if (!_gate) { enabled = false; return; }
        if (glow) _glow = Piece("EMBER GLOW", glow, _gate.sortingOrder - 1);   // (behind it: a halo round its edges, the gate itself still black)
        foreach (var s in sets) if (s) _sets.Add(Piece("EMBERS", s, _gate.sortingOrder + 2));
        _phase = new float[_sets.Count];
        for (int i = 0; i < _phase.Length; i++) _phase[i] = Random.value * 10f;
    }

    void LateUpdate()
    {
        if (!_gate) return;
        bool on = _gate.enabled;
        float t = Time.time, all = 0f;
        for (int i = 0; i < _sets.Count; i++)
        {
            // Each set glowing up and dying down on its own, with a quick flicker.
            float k = 0.45f + 0.35f * Mathf.Sin(t * (1.3f + i * 0.4f) + _phase[i]) + 0.2f * Mathf.Sin(t * 9f + _phase[i] * 3f);
            k = Mathf.Clamp01(k);
            all += k;
            _sets[i].color = new Color(1f, 1f, 1f, k * _gate.color.a);
            _sets[i].enabled = on;
        }
        if (_glow)
        {
            float k = _sets.Count > 0 ? all / _sets.Count : 1f;
            _glow.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.55f + 0.45f * k) * _gate.color.a);
            _glow.enabled = on;
        }
    }

    void OnDestroy()
    {
        foreach (var s in _sets) if (s) Destroy(s.gameObject);
        if (_glow) Destroy(_glow.gameObject);
    }
}
