using UnityEngine;

namespace TimeCrystal
{
    // Hit: the player's ship and the pilot's portrait flash white (a white silhouette laid over
    // each, Resources/TcFlash.shader, fading fast), the portrait shakes a little and the ship very
    // slightly. On the ship (TcPlayerShip); RetroShmupPlayerController.TakeDamage calls Hit.
    public class TcDamageFlash : MonoBehaviour
    {
        const float FlashTime = 0.18f, ShakeTime = 0.3f;
        const float PortraitShake = 14f;     // (canvas units, of 1920 across)
        const float ShipShake = 0.035f;      // (world units: a few of the art's pixels)

        static Material s_material;
        SpriteRenderer _body, _bodyWhite, _portrait, _portraitWhite;
        RectTransform _portraitRect;
        Vector2 _portraitHome;
        Vector3 _shipOffset;
        float _flash = -1f, _shake = -1f;

        public static Material WhiteMaterial
        {
            get
            {
                if (!s_material)
                {
                    var shader = Resources.Load<Shader>("TcFlash");
                    if (shader) s_material = new Material(shader) { name = "TC White Flash" };
                }
                return s_material;
            }
        }

        void Awake() => _body = GetComponent<SpriteRenderer>();

        public void Hit()
        {
            if (!WhiteMaterial) return;
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            var portrait = canvas ? canvas.Find("PLAYER PORTRAIT") : null;
            _portrait = portrait ? portrait.GetComponent<SpriteRenderer>() : null;
            if (_shake < 0f && portrait is RectTransform rt) { _portraitRect = rt; _portraitHome = rt.anchoredPosition; }
            if (!_bodyWhite) _bodyWhite = Twin(_body);
            if (_portrait && (!_portraitWhite || _portraitWhite.transform.parent != _portrait.transform)) _portraitWhite = Twin(_portrait);
            _flash = FlashTime;
            _shake = ShakeTime;
        }

        static SpriteRenderer Twin(SpriteRenderer of)
        {
            if (!of) return null;
            var go = new GameObject("HIT FLASH") { layer = of.gameObject.layer };
            go.transform.SetParent(of.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = WhiteMaterial;
            r.sortingLayerID = of.sortingLayerID;
            r.sortingOrder = of.sortingOrder + 1;
            r.enabled = false;
            return r;
        }

        static void Copy(SpriteRenderer white, SpriteRenderer of, float alpha)
        {
            if (!white || !of) return;
            white.gameObject.layer = of.gameObject.layer;   // (the portrait's moves to TcStatus in full screen)
            white.sprite = of.sprite;
            // (Sprites flip in their own shader; the white one's doesn't, so its flips are a mirrored scale.)
            white.flipX = white.flipY = false;
            white.transform.localScale = new Vector3(of.flipX ? -1f : 1f, of.flipY ? -1f : 1f, 1f);
            white.color = new Color(1f, 1f, 1f, alpha);
            // Only while it flashes - set every frame, after the game's own: its death and CONTINUE turn the
            // ship's sprites off and back on, this one with them.
            white.enabled = alpha > 0f && of.enabled && of.sprite;
        }

        void OnDisable()
        {
            if (_bodyWhite) _bodyWhite.enabled = false;
            if (_portraitWhite) _portraitWhite.enabled = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float a = 0f;
            if (_flash > -1f)
            {
                _flash -= dt;
                a = Mathf.Clamp01(_flash / FlashTime);
                if (_flash <= 0f) _flash = -1f;
            }
            Copy(_bodyWhite, _body, a);
            Copy(_portraitWhite, _portrait, a);
            if (_shake > -1f)
            {
                _shake -= dt;
                float k = Mathf.Clamp01(_shake / ShakeTime);   // (dying away)
                var jitter = Random.insideUnitCircle * k;
                // The portrait about where it sits; the ship by what it moved last frame, so it never drifts.
                if (_portraitRect) _portraitRect.anchoredPosition = _portraitHome + jitter * PortraitShake;
                var offset = _shake > 0f ? (Vector3)(jitter * ShipShake) : Vector3.zero;
                transform.position += offset - _shipOffset;
                _shipOffset = offset;
                if (_shake <= 0f)
                {
                    if (_portraitRect) _portraitRect.anchoredPosition = _portraitHome;
                    _shake = -1f;
                }
            }
        }
    }
}
