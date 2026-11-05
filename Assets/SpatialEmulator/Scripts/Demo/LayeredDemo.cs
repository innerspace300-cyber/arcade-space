// LayeredDemo.cs — what the cabinet needs from a built-in demo game (a real
// Unity scene played in place of an emulated romset): its depth groups,
// each drawn by its own camera into a texture that goes on one of
// MobileRetroDepthLayerStack's layer quads, and the picture's shape.
// ENDLESS KNIGHT (PitDemoGame) and THE TIME CRYSTAL (TimeCrystalGame).
// Only one runs at a time.

using System.Collections.Generic;
using UnityEngine;

namespace SpatialEmulator.Demo
{
    public abstract class LayeredDemo : MonoBehaviour
    {
        public struct Layer
        {
            public string name;
            public uint zOrder;
            /// How deep it sits, in steps of the layer spacing (the emulator's
            /// layers are their z-order apart, one step each; a demo's, its own).
            public float depth;
            public RenderTexture texture;
            /// Shown above the cabinet frame (ENDLESS KNIGHT's health bar and
            /// score), on the frame's own layer, not on the screen.
            public bool aboveFrame;
            /// Grown with the cabinet frame as the layers spread apart, so it
            /// keeps filling the frame's opening (the far background).
            public bool fillFrame;
            /// Its texture holds soft pixels (glows, trails) drawn premultiplied: the layer quad adds them like light (LayerUnlit _SrcBlend One).
            public bool premultiplied;
            /// Drawn over every other layer (THE TIME CRYSTAL's player ship over its HUD), at its own depth.
            public bool onTop;
            /// Only in full screen, under the picture (THE TIME CRYSTAL's portrait and health bar):
            /// the picture's size, moved down BelowPictureDrop of its height. Never a layer quad in AR.
            public bool belowPicture;
        }

        /// The demo that's running, if any.
        public static LayeredDemo Active { get; protected set; }
        public static bool AnyRunning => Active != null;

        public readonly List<Layer> Layers = new List<Layer>();

        /// The picture's shape, width over height.
        public abstract float Aspect { get; }

        /// The Unity layer of its front (HUD) depth group.
        public abstract string HudLayerName { get; }

        /// Full screen: the picture up at the top, just under the menu bar (not centred).
        public virtual bool FullScreenAtTop => false;
        /// Full screen: how far the belowPicture layer's moved down, in picture heights.
        public virtual float BelowPictureDrop => 0f;
    }
}
