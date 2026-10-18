using System.Collections.Generic;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Daftar semua efek suara di game.</summary>
    public enum Sfx
    {
        // Player
        SwordSwing, SwordWhoosh, SwordHitBone, SwordHitBody, SwordHitArmor,
        PlayerHurt, PlayerHurtArmor, PlayerStep, BodyFall,

        // Enemy
        SkeletonStep, GuardianStep, BoneBreak, LinkLand,
        AlertSpotted, AlertSuspicious, AlertFlee,
        MeleeWindup, MeleeSwing,
        CrossbowDraw, CrossbowShoot, BoltImpact,
        MageCharge, MageCast, MageImpact,
        SlamWindup, SlamBoom, SlamRocks,
        ChargeWindup, ChargeRumble, ChargeHit,
        EnrageBoom, Gong,

        // Level & objective
        KeyPickup, DoorUnlock, DoorOpen, DoorCreak, GateSlam, Chains,
        SpikeWarn, SpikeStrike, SealBreak, ArtifactPickup, Drip, PotionPickup, Heal,
        JingleKey, JingleVictory, JingleDefeat,

        // UI
        UIHover, UIClick, UIStart, UIPauseOpen, UIPauseClose, UIToast, UIEndHit
    }

    /// <summary>Definisi satu efek suara: variasi klip, volume, rentang pitch acak, dan pengaturan 3D.</summary>
    public class SfxDef
    {
        public string[] Clips;
        public float Volume = 1f;
        public float PitchMin = 1f, PitchMax = 1f;
        public float Spatial = 0.85f;     // 0 = 2D, 1 = full 3D
        public float MaxDistance = 30f;   // jarak suara hilang (rolloff linear)
        public float Cooldown = 0.03f;    // cegah suara sama menumpuk di frame yang sama
        public bool IsUI;

        public SfxDef TwoD() { Spatial = 0f; return this; }
        public SfxDef UI() { Spatial = 0f; IsUI = true; return this; }
        public SfxDef Range(float max) { MaxDistance = max; return this; }
        public SfxDef Blend(float spatial) { Spatial = spatial; return this; }
        public SfxDef Cool(float seconds) { Cooldown = seconds; return this; }
    }

    /// <summary>
    /// Bank suara. Klip SFX berasal dari paket CC0 Kenney (Resources/Kenney), tetesan air dari generator
    /// prosedural (Resources/Ambient). Volume tiap definisi sudah diseimbangkan berdasarkan analisis
    /// loudness klip, jadi semua efek terdengar setara.
    /// </summary>
    public static class SoundLibrary
    {
        private static readonly Dictionary<Sfx, SfxDef> defs = new Dictionary<Sfx, SfxDef>
        {
            // ---------- Player ----------
            [Sfx.SwordSwing] = K(0.45f, 0.95f, 1.1f, "knifeSlice", "knifeSlice2"),
            [Sfx.SwordWhoosh] = K(1f, 1.35f, 1.6f, "cloth1", "cloth2", "cloth3"),
            [Sfx.SwordHitBone] = K(0.9f, 1.15f, 1.35f, Seq("impactWood_light_", 5)),
            [Sfx.SwordHitBody] = K(0.45f, 0.9f, 1.1f, Seq("impactPunch_medium_", 5)),
            [Sfx.SwordHitArmor] = K(0.6f, 0.75f, 0.9f, Seq("impactPlate_heavy_", 5)),
            [Sfx.PlayerHurt] = K(0.6f, 0.9f, 1.05f, Seq("impactPunch_heavy_", 5)).Blend(0.3f),
            [Sfx.PlayerHurtArmor] = K(0.45f, 0.95f, 1.1f, Seq("impactPlate_light_", 5)).Blend(0.3f),
            [Sfx.PlayerStep] = K(0.4f, 0.9f, 1.1f, Seq("footstep_concrete_", 5)).Blend(0.3f).Cool(0.1f),
            [Sfx.BodyFall] = K(0.6f, 0.8f, 0.95f, Seq("impactSoft_heavy_", 5)),

            // ---------- Enemy ----------
            [Sfx.SkeletonStep] = K(0.28f, 1.15f, 1.35f, Seq("footstep_concrete_", 5)).Range(18f).Cool(0f),
            [Sfx.GuardianStep] = K(0.45f, 0.6f, 0.75f, Seq("impactSoft_heavy_", 5)).Range(32f),
            [Sfx.BoneBreak] = K(0.45f, 1.3f, 1.5f, Seq("impactPlank_medium_", 5)),
            [Sfx.LinkLand] = K(0.5f, 1f, 1.2f, Seq("impactSoft_medium_", 5)),
            [Sfx.AlertSpotted] = K(0.55f, 1.6f, 1.7f, "impactBell_heavy_004").Range(40f).Cool(0.15f),
            [Sfx.AlertSuspicious] = K(0.3f, 1.2f, 1.3f, "impactBell_heavy_002").Range(35f).Cool(0.15f),
            [Sfx.AlertFlee] = K(0.8f, 1.6f, 1.9f, Seq("impactWood_light_", 5)),
            [Sfx.MeleeWindup] = K(0.55f, 0.65f, 0.75f, "creak3"),
            [Sfx.MeleeSwing] = K(0.5f, 0.75f, 0.9f, "chop"),
            [Sfx.CrossbowDraw] = K(0.6f, 1.3f, 1.45f, "creak1", "creak2"),
            [Sfx.CrossbowShoot] = K(0.6f, 0.7f, 0.8f, "metalLatch"),
            [Sfx.BoltImpact] = K(0.8f, 1.1f, 1.3f, Seq("impactWood_medium_", 5)),
            [Sfx.MageCharge] = K(0.18f, 1.25f, 1.4f, Seq("forceField_", 5)),
            [Sfx.MageCast] = K(0.3f, 0.5f, 0.6f, Seq("laserLarge_", 5)),
            [Sfx.MageImpact] = K(0.35f, 1.3f, 1.5f, "explosionCrunch_000"),
            [Sfx.SlamWindup] = K(0.4f, 0.42f, 0.48f, Seq("forceField_", 5)).Range(40f),
            [Sfx.SlamBoom] = K(0.9f, 0.9f, 1f, "lowFrequency_explosion_001").Blend(0.4f).Range(45f),
            [Sfx.SlamRocks] = K(0.7f, 0.65f, 0.75f, Seq("impactMining_", 5)).Range(40f),
            [Sfx.ChargeWindup] = K(0.9f, 0.45f, 0.5f, "creak1", "creak2").Range(40f),
            [Sfx.ChargeRumble] = K(0.35f, 0.7f, 0.8f, Seq("spaceEngineLow_", 5)).Range(40f),
            [Sfx.ChargeHit] = K(0.9f, 0.7f, 0.8f, Seq("impactPunch_heavy_", 5)),
            [Sfx.EnrageBoom] = K(0.9f, 0.85f, 0.85f, "lowFrequency_explosion_000").Blend(0.4f).Range(50f),
            [Sfx.Gong] = K(0.7f, 0.5f, 0.55f, "impactBell_heavy_000", "impactBell_heavy_001").Blend(0.5f).Range(50f),

            // ---------- Level & objective ----------
            [Sfx.KeyPickup] = K(0.8f, 1f, 1.1f, "handleCoins"),
            [Sfx.DoorUnlock] = K(0.6f, 0.9f, 0.9f, "metalLatch"),
            [Sfx.DoorOpen] = K(0.9f, 0.65f, 0.7f, "doorOpen_2").Range(40f),
            [Sfx.DoorCreak] = K(0.8f, 0.55f, 0.55f, "creak2").Range(40f),
            [Sfx.GateSlam] = K(1f, 0.55f, 0.65f, Seq("impactMetal_heavy_", 5)).Range(45f),
            [Sfx.Chains] = K(0.8f, 0.5f, 0.55f, "handleCoins").Range(40f),
            [Sfx.SpikeWarn] = K(1f, 0.75f, 0.85f, "metalClick").Range(16f).Cool(0.1f),
            [Sfx.SpikeStrike] = K(0.35f, 1.15f, 1.3f, Seq("impactMetal_light_", 5)).Range(18f).Cool(0.1f),
            [Sfx.SealBreak] = K(0.7f, 0.75f, 0.85f, Seq("impactGlass_heavy_", 5)).Range(45f),
            [Sfx.ArtifactPickup] = K(0.6f, 1f, 1f, "impactBell_heavy_001"),
            [Sfx.PotionPickup] = K(0.8f, 1.1f, 1.3f, Seq("impactGlass_light_", 5)),
            [Sfx.Heal] = K(0.35f, 1.9f, 2.1f, Seq("forceField_", 5)).Blend(0.3f),
            [Sfx.Drip] = new SfxDef { Clips = new[] { "Ambient/drip_0", "Ambient/drip_1", "Ambient/drip_2", "Ambient/drip_3" },
                Volume = 0.25f, PitchMin = 0.85f, PitchMax = 1.2f, Spatial = 1f, MaxDistance = 26f },
            [Sfx.JingleKey] = K(0.55f, 1f, 1f, "jingles_PIZZI02").TwoD(),
            [Sfx.JingleVictory] = K(0.6f, 1f, 1f, "jingles_PIZZI10").UI(),
            [Sfx.JingleDefeat] = K(0.6f, 1f, 1f, "jingles_PIZZI07").UI(),

            // ---------- UI ----------
            [Sfx.UIHover] = K(0.25f, 0.95f, 1.05f, "select_001", "select_002").UI(),
            [Sfx.UIClick] = K(0.8f, 0.95f, 1.05f, "click_002", "click_003", "click_005").UI(),
            [Sfx.UIStart] = K(0.4f, 0.85f, 0.85f, "knifeSlice2").UI(),
            [Sfx.UIPauseOpen] = K(0.7f, 1f, 1.05f, "bookFlip1").UI(),
            [Sfx.UIPauseClose] = K(0.45f, 1f, 1.05f, "bookClose").UI(),
            [Sfx.UIToast] = K(0.6f, 0.95f, 1.1f, "bookFlip3").UI(),
            [Sfx.UIEndHit] = K(0.8f, 0.6f, 0.6f, Seq("impactPunch_heavy_", 5)).UI(),
        };

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<Sfx, int> lastIndex = new Dictionary<Sfx, int>();

        public static SfxDef Get(Sfx sfx) => defs[sfx];

        /// <summary>Pilih klip acak dari variasi yang ada, hindari klip yang sama dua kali berturut-turut.</summary>
        public static AudioClip PickClip(Sfx sfx)
        {
            SfxDef def = defs[sfx];
            int index = Random.Range(0, def.Clips.Length);
            if (def.Clips.Length > 1 && lastIndex.TryGetValue(sfx, out int last) && last == index)
                index = (index + 1) % def.Clips.Length;
            lastIndex[sfx] = index;
            return Load(def.Clips[index]);
        }

        public static AudioClip Load(string path)
        {
            if (cache.TryGetValue(path, out AudioClip clip))
                return clip;
            clip = Resources.Load<AudioClip>(path.Contains("/") ? path : "Kenney/" + path);
            if (clip == null)
                Debug.LogWarning($"[Audio] Klip tidak ditemukan: {path}");
            cache[path] = clip;
            return clip;
        }

        private static SfxDef K(float volume, float pitchMin, float pitchMax, params string[] clips) =>
            new SfxDef { Clips = clips, Volume = volume, PitchMin = pitchMin, PitchMax = pitchMax };

        /// <summary>Seq("impactWood_light_", 5) → impactWood_light_000 .. impactWood_light_004</summary>
        private static string[] Seq(string prefix, int count)
        {
            var list = new string[count];
            for (int i = 0; i < count; i++)
                list[i] = prefix + i.ToString("000");
            return list;
        }
    }
}
