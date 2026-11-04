using UnityEditor;
using UnityEngine;

namespace VK.EditorTools
{
    /// <summary>
    /// Pixel-art sprites → point-filtered, uncompressed, sized to one world unit:
    /// Kenney 16px tiles at 16 PPU, PixelLab 48px characters/monsters at 48 PPU, 96px bosses at 96 PPU, 40px icons at 40 PPU.
    /// </summary>
    public class ImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 5;

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/VFX/") && assetPath.EndsWith(".png"))
            {   // spell-effect strips (tools/vfx_export.py): plain texture, sliced by Res.Vfx; pixels stay exact
                var vi = (TextureImporter)assetImporter;
                vi.textureType = TextureImporterType.Default;
                vi.filterMode = FilterMode.Point;
                vi.textureCompression = TextureImporterCompression.Uncompressed;
                vi.mipmapEnabled = false;
                vi.alphaIsTransparency = true;
                vi.wrapMode = TextureWrapMode.Clamp;
                vi.npotScale = TextureImporterNPOTScale.None;
                vi.maxTextureSize = 4096;
                return;
            }
            int ppu;
            if (assetPath.StartsWith("Assets/Resources/Tiles")) ppu = 16;
            else if (assetPath.StartsWith("Assets/Resources/PixelLab/Bosses")) ppu = 96;
            else if (assetPath.StartsWith("Assets/Resources/PixelLab/Icons")) ppu = 40;
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
