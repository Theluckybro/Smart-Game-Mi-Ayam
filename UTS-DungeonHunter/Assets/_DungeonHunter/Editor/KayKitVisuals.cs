using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Menyiapkan visual KayKit (CC0) untuk scene builder:
    /// material URP, Animator Controller per tipe karakter, pemasangan model + senjata,
    /// dan helper untuk menaruh model dungeon dengan ukuran yang disesuaikan ke grid game.
    /// </summary>
    public static class KayKitVisuals
    {
        private const string Root = KayKitImportSettings.Root;
        private const string MaterialDir = "Assets/_DungeonHunter/Materials/KayKit";
        private const string ControllerDir = "Assets/_DungeonHunter/Animation";

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static readonly Dictionary<string, AnimatorController> Controllers = new Dictionary<string, AnimatorController>();

        // =====================================================
        // SPESIFIKASI KARAKTER
        // =====================================================

        public class CharacterSpec
        {
            public string Model;
            public string Material;
            public string Controller;
            public float Height;
            public string RightWeapon;
            public float RightWeaponSize;
            public string LeftWeapon;
            public float LeftWeaponSize;
            public bool ReactToHits = true;
            public float Yaw;
        }

        private class ControllerSpec
        {
            public string Idle, Walk, Run, Attack, Attack2, Hit, Death, Taunt;
            public float AttackSpeed = 1f, Attack2Speed = 1f;
            public float WalkSpeed = 1.8f, RunSpeed = 4.5f;
        }

        private static readonly Dictionary<string, ControllerSpec> ControllerSpecs = new Dictionary<string, ControllerSpec>
        {
            ["Player"] = new ControllerSpec
            {
                Idle = "Idle_A", Walk = "Walking_A", Run = "Running_A",
                Attack = "Melee_1H_Attack_Slice_Horizontal", AttackSpeed = 1.8f,
                Hit = "Hit_A", Death = "Death_A", WalkSpeed = 2.5f, RunSpeed = 6f
            },
            ["SkeletonMelee"] = new ControllerSpec
            {
                Idle = "Skeletons_Idle", Walk = "Skeletons_Walking", Run = "Running_A",
                Attack = "Melee_1H_Attack_Chop", AttackSpeed = 1.3f,
                Hit = "Hit_B", Death = "Skeletons_Death"
            },
            ["SkeletonMage"] = new ControllerSpec
            {
                Idle = "Skeletons_Idle", Walk = "Skeletons_Walking", Run = "Running_A",
                Attack = "Ranged_Magic_Shoot", AttackSpeed = 1.2f,
                Hit = "Hit_A", Death = "Skeletons_Death"
            },
            ["SkeletonCrossbow"] = new ControllerSpec
            {
                Idle = "Skeletons_Idle", Walk = "Skeletons_Walking", Run = "Running_A",
                Attack = "Ranged_1H_Shoot", AttackSpeed = 1.2f,
                Hit = "Hit_A", Death = "Skeletons_Death"
            },
            ["Guardian"] = new ControllerSpec
            {
                Idle = "Melee_2H_Idle", Walk = "Skeletons_Walking", Run = "Running_B",
                Attack = "Melee_2H_Attack_Chop", AttackSpeed = 0.9f,
                Attack2 = "Melee_2H_Attack_Stab", Attack2Speed = 1f,
                Taunt = "Skeletons_Taunt", Death = "Skeletons_Death",
                WalkSpeed = 1.8f, RunSpeed = 4.5f
            },
        };

        // =====================================================
        // INIT
        // =====================================================

        public static void Init()
        {
            EnsureImported();
            CreateMaterials();
            LoadClips();
            Controllers.Clear();
            foreach (var kv in ControllerSpecs)
                Controllers[kv.Key] = BuildController(kv.Key, kv.Value);
        }

        /// <summary>Pastikan semua FBX KayKit sudah di-import dengan KayKitImportSettings.</summary>
        private static void EnsureImported()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                    continue;

                bool isAnimation = path.StartsWith(Root + "Animations/");
                bool wrongMaterial = importer.materialImportMode != ModelImporterMaterialImportMode.None;
                bool missingLoops = isAnimation && importer.clipAnimations.Length == 0;
                if (wrongMaterial || missingLoops)
                    importer.SaveAndReimport();
            }
        }

        private static void CreateMaterials()
        {
            Materials.Clear();
            AddMaterial("Knight", "knight_texture.png", Color.white);
            AddMaterial("Skeleton", "skeleton_texture.png", Color.white);
            AddMaterial("SkeletonGuardian", "skeleton_texture.png", new Color(0.78f, 0.6f, 1f));
            AddMaterial("Dungeon", "dungeon_texture.png", Color.white);
        }

        private static void AddMaterial(string key, string texture, Color tint)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/KK_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + texture);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
        }

        public static Material Material(string key) => Materials[key];

        private static void LoadClips()
        {
            Clips.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { Root + "Animations" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__"))
                        continue;
                    Clips[clip.name] = clip;
                }
            }

            if (Clips.Count == 0)
                throw new InvalidOperationException("Animasi KayKit tidak ditemukan di " + Root + "Animations");
        }

        private static AnimationClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (!Clips.TryGetValue(name, out AnimationClip clip))
                throw new InvalidOperationException($"Clip animasi '{name}' tidak ditemukan");
            return clip;
        }

        // =====================================================
        // ANIMATOR CONTROLLER
        // =====================================================

        /// <summary>
        /// Layer tunggal:
        ///   Locomotion (blend tree Speed: idle → walk → run)
        ///   Any State → Attack / Attack2 / Hit / Taunt (trigger) → kembali ke Locomotion
        ///   Any State → Death (bool Dead), tanpa keluar
        /// </summary>
        private static AnimatorController BuildController(string name, ControllerSpec spec)
        {
            EnsureFolder(ControllerDir);
            string path = $"{ControllerDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Taunt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip(spec.Idle), 0f);
            tree.AddChild(Clip(spec.Walk), spec.WalkSpeed);
            tree.AddChild(Clip(spec.Run), spec.RunSpeed);
            sm.defaultState = locomotion;

            AddActionState(sm, locomotion, "Attack", Clip(spec.Attack), spec.AttackSpeed);
            AddActionState(sm, locomotion, "Attack2", Clip(spec.Attack2), spec.Attack2Speed);
            AddActionState(sm, locomotion, "Hit", Clip(spec.Hit), 1.3f);
            AddActionState(sm, locomotion, "Taunt", Clip(spec.Taunt), 1f);

            AnimatorState death = sm.AddState("Death");
            death.motion = Clip(spec.Death);
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddActionState(AnimatorStateMachine sm, AnimatorState back, string trigger, AnimationClip clip, float speed)
        {
            if (clip == null)
                return;

            AnimatorState state = sm.AddState(trigger);
            state.motion = clip;
            state.speed = speed;

            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.15f;
        }

        // =====================================================
        // KARAKTER
        // =====================================================

        /// <summary>Pasang model KayKit ke aktor: model, material, ukuran, senjata, Animator, CharacterAnimator.</summary>
        public static GameObject AttachCharacter(GameObject actor, CharacterSpec spec)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Characters/" + spec.Model + ".fbx");
            if (prefab == null)
                throw new InvalidOperationException("Model tidak ditemukan: " + spec.Model);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model_" + spec.Model;
            model.transform.SetParent(actor.transform, false);
            model.transform.localPosition = Vector3.zero;
            if (spec.Yaw != 0f)
                model.transform.localRotation = Quaternion.Euler(0f, spec.Yaw, 0f) * model.transform.localRotation;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(Materials[spec.Material], r.sharedMaterials.Length).ToArray();

            Bounds b = RendererBounds(model);
            float factor = spec.Height / Mathf.Max(0.01f, b.size.y);
            model.transform.localScale = model.transform.localScale * factor;
            b = RendererBounds(model);
            model.transform.position += Vector3.up * (actor.transform.position.y - b.min.y);

            if (!string.IsNullOrEmpty(spec.RightWeapon))
                AttachWeapon(model, "handslot.r", spec.RightWeapon, spec.RightWeaponSize * spec.Height, spec.Material);
            if (!string.IsNullOrEmpty(spec.LeftWeapon))
                AttachWeapon(model, "handslot.l", spec.LeftWeapon, spec.LeftWeaponSize * spec.Height, spec.Material);

            var animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controllers[spec.Controller];
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var characterAnimator = actor.AddComponent<CharacterAnimator>();
            var so = new SerializedObject(characterAnimator);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("reactToHits").boolValue = spec.ReactToHits;
            so.ApplyModifiedPropertiesWithoutUndo();

            return model;
        }

        private static void AttachWeapon(GameObject model, string slotName, string weapon, float longestSize, string materialKey)
        {
            Transform slot = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == slotName);
            if (slot == null)
                throw new InvalidOperationException($"Slot '{slotName}' tidak ditemukan di {model.name}");

            GameObject w = Spawn(Root + "Weapons/" + weapon + ".fbx", null, materialKey);
            w.name = weapon;
            Vector3 prefabScale = w.transform.localScale;
            Bounds wb = RendererBounds(w);
            float longest = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
            float factor = longestSize / Mathf.Max(0.001f, longest);

            w.transform.SetParent(slot, false);
            Vector3 slotScale = slot.lossyScale;
            w.transform.localScale = new Vector3(
                prefabScale.x * factor / slotScale.x,
                prefabScale.y * factor / slotScale.y,
                prefabScale.z * factor / slotScale.z);
        }

        // =====================================================
        // MODEL STATIS (DUNGEON / PROPS)
        // =====================================================

        public static string DungeonPath(string name) => Root + "Dungeon/" + name + ".fbx";
        public static string WeaponPath(string name) => Root + "Weapons/" + name + ".fbx";

        /// <summary>Instantiate model dan pasang material KayKit.</summary>
        public static GameObject Spawn(string assetPath, Transform parent, string materialKey = "Dungeon")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                throw new InvalidOperationException("Model tidak ditemukan: " + assetPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
                go.transform.SetParent(parent, false);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(Materials[materialKey], r.sharedMaterials.Length).ToArray();
            return go;
        }

        /// <summary>
        /// Taruh model dungeon dengan ukuran target (dunia). Sumbu yang bernilai 0 mengikuti skala sumbu X
        /// (proporsi dipertahankan). Alas model diletakkan di <paramref name="basePosition"/>.
        /// </summary>
        public static GameObject Place(string name, Transform parent, Vector3 basePosition, Quaternion rotation, Vector3 targetSize)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            Spawn(DungeonPath(name), go.transform);
            go.transform.rotation = rotation;
            FitTo(go, targetSize);
            Bounds b = RendererBounds(go);
            go.transform.position += new Vector3(basePosition.x - b.center.x, basePosition.y - b.min.y, basePosition.z - b.center.z);
            return go;
        }

        /// <summary>
        /// Skala container (dalam ruang lokalnya sendiri) agar ukuran model di dalamnya sesuai target.
        /// Rotasi bawaan prefab tetap utuh karena berada di child.
        /// </summary>
        public static void FitTo(GameObject go, Vector3 targetSize)
        {
            Quaternion rot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;
            Bounds b = RendererBounds(go);
            float fx = targetSize.x > 0f ? targetSize.x / Mathf.Max(0.001f, b.size.x) : 0f;
            float fy = targetSize.y > 0f ? targetSize.y / Mathf.Max(0.001f, b.size.y) : 0f;
            float fz = targetSize.z > 0f ? targetSize.z / Mathf.Max(0.001f, b.size.z) : 0f;
            float fallback = fx > 0f ? fx : fz > 0f ? fz : fy;
            if (fx <= 0f) fx = fallback;
            if (fy <= 0f) fy = fallback;
            if (fz <= 0f) fz = fallback;

            Vector3 s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x * fx, s.y * fy, s.z * fz);
            go.transform.rotation = rot;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
