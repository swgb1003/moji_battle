using UnityEditor;

namespace MojiBattle.Editor
{
    public sealed class MenuAssetImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = assetPath.EndsWith("Logo.png") || assetPath.Contains("/Achievements/") || assetPath.Contains("/WorldCup/");
            importer.maxTextureSize = 2048;
            // Keep large backgrounds at full resolution; small printed emblems are
            // displayed far below their generated source size. Resize on import
            // without block compression so their edges and transparency stay clean.
            if (assetPath.Contains("/WorldCup/"))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (name.StartsWith("design_cup_") || name == "design_vs")
                    importer.maxTextureSize = 512;
                else if (name == "design_trophy" || name == "design_laurel" ||
                    name == "design_laurel_badge" || name == "design_ink" ||
                    name == "world_cup_emblem" || name == "watanabe_cup_bracket" ||
                    name.StartsWith("world_cup_rule_"))
                    importer.maxTextureSize = 1024;
            }
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
