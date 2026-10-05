using UnityEditor;

namespace MojiBattle.EditorTools
{
    /// <summary>同梱フォントは字形ベイク専用。動的フォントとして取り込み、OS フォントへの代替を使わない。</summary>
    public sealed class FontAssetImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessAsset()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Art/Fonts/")) return;
            if (assetImporter is TrueTypeFontImporter ti)
            {
                ti.fontTextureCase = FontTextureCase.Dynamic;
                ti.includeFontData = true;
                ti.fontNames = new string[0];
                ti.fontReferences = new UnityEngine.Font[0];
            }
        }
    }
}
