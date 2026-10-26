using UnityEditor;
using UnityEngine;

namespace VK.EditorTools
{
    /// <summary>Kenney 16px tiles → point-filtered, uncompressed sprites at 16 PPU.</summary>
    public class ImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Tiles")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
