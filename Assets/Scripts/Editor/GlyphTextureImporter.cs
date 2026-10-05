using UnityEditor;

namespace MojiBattle.EditorTools
{
    /// <summary>字形マスクは解析で画素を読むため、Read/Write 有効・無圧縮・ミップなしで取り込む。</summary>
    public sealed class GlyphTextureImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Glyphs/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.sRGBTexture = false;
        }
    }
}
