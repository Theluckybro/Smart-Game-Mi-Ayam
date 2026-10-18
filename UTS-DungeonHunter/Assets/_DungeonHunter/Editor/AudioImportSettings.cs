using UnityEditor;
using UnityEngine;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Pengaturan import audio otomatis:
    /// - Musik (Audio/Resources/Music) : Vorbis, Compressed In Memory (hemat memori, tetap bisa PlayScheduled sinkron)
    /// - SFX Kenney & ambient           : Decompress On Load (latensi nol), mono (lebih tepat untuk suara 3D)
    /// - Jingle tetap stereo
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace("\\", "/");
            bool music = path.Contains("/Audio/Resources/Music/");
            bool sfx = path.Contains("/ThirdParty/Kenney/") || path.Contains("/Audio/Resources/Ambient/");
            if (!music && !sfx)
                return;

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            importer.loadInBackground = false;

            if (music)
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.quality = 0.6f;
                importer.forceToMono = false;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.quality = 0.7f;
                importer.forceToMono = !path.Contains("jingles_");
            }
            importer.defaultSampleSettings = s;
        }
    }
}
