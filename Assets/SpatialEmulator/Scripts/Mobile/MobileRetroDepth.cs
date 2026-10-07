// MobileRetroDepth.cs — P/Invoke reader for the in-process retrodepth
// transport (spatial-emulator-core/libretro-mame's src/emu/retrodepth.h).
//
// Desktop's RetroDepthShm.cs reads a POSIX shared-memory segment because
// MAME ran as a separate process. On mobile the core is linked directly
// into this app (see LibretroCore.cs), so layer data is just read straight
// out of the same process's memory via these plain C getter functions -
// no shared memory, no polling a frame_id across a process boundary.

using System;
using System.Runtime.InteropServices;

namespace SpatialEmulator.Mobile
{
    public struct MobileRdLayer
    {
        public string Name;
        public uint ZOrder;
        public int Width;
        public int Height;
        public bool HasOwner;
    }

    public static class MobileRetroDepth
    {
#if UNITY_IOS && !UNITY_EDITOR
        const string Lib = "__Internal"; // resolved at link time against SpatialEmulatorCore.framework - see LibretroCore.cs's build note
#else
        const string Lib = "spatial_libretro";
#endif

        [StructLayout(LayoutKind.Sequential)]
        struct RDLayerDescNative
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] name;
            public uint z_order;
            public uint width;
            public uint height;
            [MarshalAs(UnmanagedType.I1)] public bool has_owner;
        }

        [DllImport(Lib)] static extern uint retrodepth_frame_id();
        [DllImport(Lib)] static extern uint retrodepth_layer_count();
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retrodepth_get_layer_desc(uint index, out RDLayerDescNative outDesc);
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retrodepth_get_layer_pixels(uint index, IntPtr dst, uint dstSizeBytes);
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retrodepth_get_layer_owners(uint index, IntPtr dst, uint dstSizeBytes);
        [DllImport(Lib)] static extern void retrodepth_set_palette_route(uint paletteIndex, byte group);

        public static uint FrameId => retrodepth_layer_count() > 0 ? retrodepth_frame_id() : 0;
        public static int LayerCount => (int)retrodepth_layer_count();

        public static bool TryGetLayer(int index, out MobileRdLayer layer)
        {
            layer = default;
            if (!retrodepth_get_layer_desc((uint)index, out var native)) return false;
            int nul = Array.IndexOf(native.name, (byte)0);
            layer = new MobileRdLayer
            {
                Name = System.Text.Encoding.ASCII.GetString(native.name, 0, nul < 0 ? native.name.Length : nul),
                ZOrder = native.z_order,
                Width = (int)native.width,
                Height = (int)native.height,
                HasOwner = native.has_owner,
            };
            return true;
        }

        /// Copies this layer's BGRA pixels straight into a pinned managed
        /// buffer (e.g. a NativeArray/byte[] the caller already sized from
        /// TryGetLayer's Width*Height*4). Returns false on any mismatch.
        public static bool TryGetLayerPixels(int index, IntPtr dst, int dstSizeBytes)
            => retrodepth_get_layer_pixels((uint)index, dst, (uint)dstSizeBytes);

        public static bool TryGetLayerOwners(int index, IntPtr dst, int dstSizeBytes)
            => retrodepth_get_layer_owners((uint)index, dst, (uint)dstSizeBytes);

        /// Neo Geo only: routes palette index -> depth group (0-3, or 0xFF
        /// to reset to the default group 0). Harmless no-op for boards that
        /// export fixed hardware layers instead of palette-routed groups.
        public static void SetPaletteRoute(int paletteIndex, byte group) => retrodepth_set_palette_route((uint)paletteIndex, group);
    }
}
