using UnityEditor;
using UnityEngine;

namespace VK.EditorTools
{
    /// <summary>
    /// Pixel-art sprites → point-filtered, uncompressed, sized to one world unit:
    /// Kenney 16px tiles at 16 PPU, PixelLab 48px characters/monsters at 48 PPU, 96px bosses at 96 PPU.
    /// </summary>
    public class ImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            int ppu;
            if (assetPath.StartsWith("Assets/Resources/Tiles")) ppu = 16;
            else if (assetPath.StartsWith("Assets/Resources/PixelLab/Bosses")) ppu = 96;
            else if (assetPath.StartsWith("Assets/Resources/PixelLab")) ppu = 48;
            else return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
