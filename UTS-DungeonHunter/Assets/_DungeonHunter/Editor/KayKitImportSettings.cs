using UnityEditor;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Pengaturan import otomatis untuk aset KayKit (CC0) di Assets/_DungeonHunter/ThirdParty/KayKit/.
    /// - Characters  : rig Generic (Rig_Medium), avatar dari model sendiri, tanpa animasi
    /// - Animations  : rig Generic, animasi di-import, clip idle/jalan/lari di-loop
    /// - Weapons / Dungeon : mesh statis tanpa rig
    /// Material tidak di-import; material URP dibuat oleh DungeonSceneBuilder.
    /// </summary>
    public class KayKitImportSettings : AssetPostprocessor
    {
        public const string Root = "Assets/_DungeonHunter/ThirdParty/KayKit/";

        private static readonly string[] LoopKeywords =
        {
            "Idle", "Walking", "Running", "Spellcasting", "Aiming", "Blocking", "Inactive"
        };

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;

            bool isAnimation = assetPath.StartsWith(Root + "Animations/");
            bool isCharacter = assetPath.StartsWith(Root + "Characters/");

            if (isAnimation || isCharacter)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = isAnimation;
                importer.optimizeGameObjects = false;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Animations/"))
                return;

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = false;
                foreach (string keyword in LoopKeywords)
                    if (clip.name.Contains(keyword))
                        loop = true;

                clip.loopTime = loop;
                clip.loopPose = loop;
            }
            importer.clipAnimations = clips;
        }
    }
}
