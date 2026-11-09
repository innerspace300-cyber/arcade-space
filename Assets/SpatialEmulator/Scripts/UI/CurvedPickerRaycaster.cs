// CurvedPickerRaycaster.cs — the game picker's taps on the CRT cabinet's
// tube (GamePicker). There the picker is drawn onto the tube's bulging glass
// through the CRT filter (its picture bent out toward the corners), while
// its canvas - what taps land on - lies flat and unseen just behind it.
// Each tap is moved to where the picker it hit on the glass really is on
// the flat canvas, so buttons and rows answer where they're seen.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class CurvedPickerRaycaster : GraphicRaycaster
    {
        /// On the tube: taps go through the curve. Off: as any canvas.
        public bool curved;
        public float bulge = CrtEffect.Bulge;
        const float Bend = 0.11f;   // (LayerUnlit's CRT filter: the picture pushed out toward the corners)

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (!curved) { base.Raycast(eventData, resultAppendList); return; }
            var position = eventData.position;
            if (!Warp(position, out var onCanvas)) return;
            eventData.position = onCanvas;
            base.Raycast(eventData, resultAppendList);
            eventData.position = position;
        }

        bool Warp(Vector2 screen, out Vector2 result)
        {
            result = screen;
            var camera = eventCamera;
            if (!camera) return false;
            var t = (RectTransform)transform;
            float w = t.rect.width, h = t.rect.height;
            if (w <= 0f || h <= 0f) return false;
            var ray = camera.ScreenPointToRay(screen);
            Vector3 o = t.InverseTransformPoint(ray.origin), d = t.InverseTransformVector(ray.direction);
            if (Mathf.Abs(d.z) < 1e-6f) return false;
            // Where the ray meets the glass: its curve is flat at the edges,
            // bulging out (-z) to this canvas in the middle (GamePicker sets it so).
            float deep = bulge * w, z = 0f, dx = 0f, dy = 0f;
            for (int i = 0; i < 5; i++)
            {
                var p = o + d * ((z - o.z) / d.z);
                dx = p.x * 2f / w; dy = p.y * 2f / h;
                float cx = Mathf.Clamp(dx, -1f, 1f), cy = Mathf.Clamp(dy, -1f, 1f);
                z = deep * (1f - (1f - cx * cx) * (1f - cy * cy));
            }
            // The filter's bend: what shows there is from further out.
            float ux = dx * (1f + Bend * dy * dy), uy = dy * (1f + Bend * dx * dx);
            if (Mathf.Abs(ux) > 1f || Mathf.Abs(uy) > 1f) return false;
            var world = t.TransformPoint(new Vector3(ux * w * 0.5f, uy * h * 0.5f, 0f));
            result = camera.WorldToScreenPoint(world);
            return true;
        }
    }
}
