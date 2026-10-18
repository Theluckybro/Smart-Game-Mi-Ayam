using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Membuat Volume Profile post-processing URP untuk nuansa dungeon:
    /// Bloom (obor, sihir, aura bersinar), Tonemapping ACES, color grading dingin-hangat,
    /// Vignette, Film Grain halus. Chromatic Aberration, Lens Distortion, dan Depth of Field
    /// disiapkan dengan nilai 0 / nonaktif — dianimasikan saat runtime oleh PostFXController.
    /// </summary>
    public static class PostFXAssets
    {
        private const string Dir = "Assets/_DungeonHunter/Settings";
        private const string ProfilePath = Dir + "/DungeonPostFX.asset";

        public static VolumeProfile Create()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/_DungeonHunter", "Settings");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // bangun ulang dari nol agar nilai selalu sesuai kode
            foreach (VolumeComponent c in profile.components.ToList())
            {
                profile.components.Remove(c);
                Object.DestroyImmediate(c, true);
            }

            var bloom = Add<Bloom>(profile);
            bloom.threshold.value = 0.95f;
            bloom.intensity.value = 1.2f;
            bloom.scatter.value = 0.68f;
            bloom.tint.value = new Color(1f, 0.93f, 0.85f);
            bloom.highQualityFiltering.value = true;

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.value = TonemappingMode.ACES;

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.value = 0.75f;
            color.contrast.value = 10f;
            color.saturation.value = 6f;
            color.colorFilter.value = new Color(1f, 0.97f, 0.94f);
            color.hueShift.value = 0f;

            // bayangan sedikit biru-ungu, highlight hangat (kontras api obor vs dinding batu)
            var smh = Add<ShadowsMidtonesHighlights>(profile);
            smh.shadows.value = new Vector4(0.94f, 0.95f, 1.08f, 0f);
            smh.midtones.value = new Vector4(1f, 1f, 1f, 0f);
            smh.highlights.value = new Vector4(1.06f, 1f, 0.92f, 0f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.value = 0.3f;
            vignette.smoothness.value = 0.45f;
            vignette.color.value = Color.black;

            var grain = Add<FilmGrain>(profile);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.12f;
            grain.response.value = 0.8f;

            var chromatic = Add<ChromaticAberration>(profile);
            chromatic.intensity.value = 0f;

            var lens = Add<LensDistortion>(profile);
            lens.intensity.value = 0f;

            var dof = Add<DepthOfField>(profile);
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 2f;
            dof.gaussianEnd.value = 8f;
            dof.gaussianMaxRadius.value = 1.2f;
            dof.highQualitySampling.value = true;
            dof.active = false;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
