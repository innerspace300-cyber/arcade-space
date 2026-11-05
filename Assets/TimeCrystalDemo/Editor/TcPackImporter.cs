// TcPackImporter.cs — art dropped into Assets/TimeCrystalDemo/PACKS (the
// sprite packs for THE TIME CRYSTAL) imports as pixel art: a sprite, point
// filtered, no mipmaps, uncompressed - crisp at any size on the cabinet.
// Only on first import: a texture changed by hand afterwards keeps its settings.

using UnityEditor;
using UnityEngine;

namespace SpatialEmulator.EditorTools
{
    public class TcPackImporter : AssetPostprocessor
    {
        const string Packs = "Assets/TimeCrystalDemo/PACKS/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Packs) || !assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
