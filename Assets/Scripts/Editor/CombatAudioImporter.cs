using UnityEditor;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>Preload short cues; stream the supplied stereo music.</summary>
    public sealed class CombatAudioImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            bool cue = path.StartsWith("Assets/Resources/Audio/Combat/") || path.StartsWith("Assets/Audio/UI/");
            bool music = path.StartsWith("Assets/Audio/") && path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase);
            if (!cue && !music) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = cue;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = cue ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
            settings.compressionFormat = cue ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
