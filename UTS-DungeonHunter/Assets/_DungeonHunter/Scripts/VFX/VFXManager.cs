using System.Collections.Generic;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Pusat VFX / "game juice".
    /// Beberapa ParticleSystem bersama (world space) dipakai ulang lewat Emit(), sehingga efek bisa
    /// dimunculkan di posisi mana pun tanpa membuat objek baru:
    ///   Sparks, Glow, Smoke, Debris, Ring (shockwave), Telegraph (lingkaran peringatan), Charge, Heal.
    /// Ditambah: busur tebasan, garis telegraph, angka damage, ikon alert (! / ?), hit-stop, slow-motion,
    /// dan camera shake (lewat TopDownCamera).
    /// Semua method statis aman dipanggil walau VFXManager tidak ada di scene.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Materials (dibuat oleh scene builder)")]
        [SerializeField] private Material additive;
        [SerializeField] private Material alphaBlend;
        [SerializeField] private Material ring;
        [SerializeField] private Material telegraph;
        [SerializeField] private Material debris;
        [SerializeField] private Material line;

        private ParticleSystem sparks;
        private ParticleSystem glow;
        private ParticleSystem smoke;
        private ParticleSystem debrisPS;
        private ParticleSystem rings;
        private ParticleSystem telegraphs;
        private ParticleSystem charge;
        private ParticleSystem heal;

        private float hitStopUntil;
        private float slowMoUntil;
        private float slowMoScale = 1f;

        private readonly List<FloatingText> texts = new List<FloatingText>();
        private GUIStyle textStyle;

        public Material Additive => additive;
        public Material AlphaBlend => alphaBlend;

        private class FloatingText
        {
            public Vector3 Position;
            public Transform Follow;
            public Vector3 FollowOffset;
            public string Text;
            public Color Color;
            public float Born;
            public float Life;
            public float Size;
            public float Rise;
        }

        // =====================================================
        // SETUP
        // =====================================================

        private void Awake()
        {
            Instance = this;

            sparks = CreateSystem("Sparks", additive, ParticleSystemRenderMode.Stretch, 1.2f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.06f;
            sr.lengthScale = 1.5f;
            SizeOverLife(sparks, Curve(1f, 0f));
            AlphaOverLife(sparks, 1f, 0f);

            glow = CreateSystem("Glow", additive, ParticleSystemRenderMode.Billboard, 0f);
            SizeOverLife(glow, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.2f)));
            AlphaOverLife(glow, 1f, 0f);

            smoke = CreateSystem("Smoke", alphaBlend, ParticleSystemRenderMode.Billboard, -0.05f);
            SizeOverLife(smoke, Curve(0.5f, 1.4f));
            AlphaOverLife(smoke, 0.8f, 0f);
            var smokeVel = smoke.limitVelocityOverLifetime;
            smokeVel.enabled = true;
            smokeVel.limit = 1.5f;
            smokeVel.dampen = 0.15f;

            debrisPS = CreateSystem("Debris", debris, ParticleSystemRenderMode.Mesh, 2.2f);
            var dr = debrisPS.GetComponent<ParticleSystemRenderer>();
            dr.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            dr.alignment = ParticleSystemRenderSpace.World;
            var main = debrisPS.main;
            main.startRotation3D = true;
            var rot = debrisPS.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var col = debrisPS.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            var groundPlane = new GameObject("DebrisGround").transform;
            groundPlane.SetParent(transform, false);
            groundPlane.position = new Vector3(0f, 0.05f, 0f);
            col.SetPlane(0, groundPlane);
            col.bounce = 0.35f;
            col.dampen = 0.4f;
            col.lifetimeLoss = 0f;
            SizeOverLife(debrisPS, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

            rings = CreateSystem("Rings", ring, ParticleSystemRenderMode.HorizontalBillboard, 0f);
            SizeOverLife(rings, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f)));
            AlphaOverLife(rings, 1f, 0f);

            telegraphs = CreateSystem("Telegraph", telegraph, ParticleSystemRenderMode.HorizontalBillboard, 0f);
            SizeOverLife(telegraphs, Curve(0.25f, 1f));
            var tCol = telegraphs.colorOverLifetime;
            tCol.enabled = true;
            var tg = new Gradient();
            tg.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.7f, 0.8f), new GradientAlphaKey(1f, 0.97f), new GradientAlphaKey(0f, 1f) });
            tCol.color = tg;

            charge = CreateSystem("Charge", additive, ParticleSystemRenderMode.Billboard, 0f);
            SizeOverLife(charge, Curve(0.15f, 1f));
            var cCol = charge.colorOverLifetime;
            cCol.enabled = true;
            var cg = new Gradient();
            cg.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
            cCol.color = cg;

            heal = CreateSystem("Heal", additive, ParticleSystemRenderMode.Billboard, -0.4f);
            SizeOverLife(heal, Curve(1f, 0.2f));
            AlphaOverLife(heal, 1f, 0f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        private ParticleSystem CreateSystem(string systemName, Material mat, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.shadowCastingMode = mode == ParticleSystemRenderMode.Mesh
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = mode == ParticleSystemRenderMode.HorizontalBillboard ? 10f : 0f;

            ps.Play();
            return ps;
        }

        private static AnimationCurve Curve(float from, float to) => AnimationCurve.Linear(0f, from, 1f, to);

        private static void SizeOverLife(ParticleSystem ps, AnimationCurve curve)
        {
            var m = ps.sizeOverLifetime;
            m.enabled = true;
            m.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void AlphaOverLife(ParticleSystem ps, float from, float to)
        {
            var m = ps.colorOverLifetime;
            m.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(from, 0f), new GradientAlphaKey(to, 1f) });
            m.color = g;
        }

        // =====================================================
        // EMIT HELPERS
        // =====================================================

        private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color color, float size, float life, float rotation = 0f)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = color,
                startSize = size,
                startLifetime = life,
                rotation = rotation,
                applyShapeToPosition = false
            };
            ps.Emit(p, 1);
        }

        private static void Emit3D(ParticleSystem ps, Vector3 pos, Vector3 vel, Color color, Vector3 size, float life)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = color,
                startSize3D = size,
                startLifetime = life,
                rotation3D = Random.insideUnitSphere * 180f,
                applyShapeToPosition = false
            };
            ps.Emit(p, 1);
        }

        private static Vector3 RandomCone(Vector3 dir, float spread)
        {
            if (dir.sqrMagnitude < 0.001f)
                return Random.onUnitSphere;
            Vector3 d = (dir.normalized + Random.insideUnitSphere * spread).normalized;
            return d;
        }

        // =====================================================
        // PUBLIC API — PARTIKEL
        // =====================================================

        /// <summary>Percikan memanjang (stretched) ke arah dir.</summary>
        public static void Sparks(Vector3 pos, Vector3 dir, Color color, int count = 12, float speed = 7f, float spread = 0.7f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = RandomCone(dir, spread) * speed * Random.Range(0.4f, 1.1f) + Vector3.up * Random.Range(0.5f, 2.5f);
                Emit(Instance.sparks, pos, v, color, Random.Range(0.08f, 0.16f), Random.Range(0.18f, 0.38f));
            }
        }

        /// <summary>Kilatan cahaya lembut.</summary>
        public static void Glow(Vector3 pos, Color color, float size = 1.2f, float life = 0.15f)
        {
            if (Instance == null) return;
            Emit(Instance.glow, pos, Vector3.zero, color, size, life);
        }

        /// <summary>Kepulan asap / debu yang menyebar horizontal.</summary>
        public static void Smoke(Vector3 pos, Color color, int count = 8, float size = 1f, float spread = 2f)
        {
            if (Instance == null) return;
            // Debu sedikit lebih terang dari warna lantai supaya kontras di dungeon yang gelap
            color = new Color(Mathf.Min(1f, color.r * 1.6f + 0.1f), Mathf.Min(1f, color.g * 1.6f + 0.1f),
                Mathf.Min(1f, color.b * 1.6f + 0.1f), Mathf.Min(1f, color.a * 1.1f));
            size *= 1.3f;
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(0.3f, 1f);
                Vector3 v = new Vector3(c.x, Random.Range(0.1f, 0.5f), c.y) * spread;
                Emit(Instance.smoke, pos + new Vector3(c.x, 0f, c.y) * 0.2f, v, color, size * Random.Range(0.7f, 1.3f),
                    Random.Range(0.5f, 0.9f), Random.Range(0f, 360f));
            }
        }

        /// <summary>Pecahan (tulang / batu) yang terlempar, memantul di lantai.</summary>
        public static void Debris(Vector3 pos, Color color, int count = 12, float force = 5f, float size = 0.14f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (Random.insideUnitSphere + Vector3.up * 1.2f).normalized * force * Random.Range(0.5f, 1.1f);
                float s = size * Random.Range(0.6f, 1.4f);
                Emit3D(Instance.debrisPS, pos + Random.insideUnitSphere * 0.3f, v, color,
                    new Vector3(s, s * Random.Range(0.5f, 2.2f), s), Random.Range(1.2f, 2f));
            }
        }

        /// <summary>Gelombang kejut melingkar di lantai.</summary>
        public static void Ring(Vector3 pos, Color color, float radius = 3f, float life = 0.4f)
        {
            if (Instance == null) return;
            Emit(Instance.rings, new Vector3(pos.x, pos.y + 0.08f, pos.z), Vector3.zero, color, radius * 2f, life);
        }

        /// <summary>Lingkaran peringatan di lantai yang membesar selama windup.</summary>
        public static void Telegraph(Vector3 pos, float radius, float duration, Color color)
        {
            if (Instance == null) return;
            Emit(Instance.telegraphs, new Vector3(pos.x, pos.y + 0.06f, pos.z), Vector3.zero, color, radius * 2f, duration);
            Emit(Instance.rings, new Vector3(pos.x, pos.y + 0.07f, pos.z), Vector3.zero, color, radius * 2.05f, Mathf.Min(0.25f, duration));
        }

        /// <summary>Bola energi yang mengisi (dipakai saat ranged membidik).</summary>
        public static void ChargeUp(Vector3 pos, Color color, float size, float duration)
        {
            if (Instance == null) return;
            Emit(Instance.charge, pos, Vector3.zero, color, size, duration);
            for (int i = 0; i < 6; i++)
            {
                Vector3 from = pos + Random.onUnitSphere * 0.9f;
                Emit(Instance.sparks, from, (pos - from) / Mathf.Max(0.1f, duration * 0.8f), color, 0.06f, duration * 0.8f);
            }
        }

        /// <summary>Partikel hijau "+" yang naik (regenerasi HP).</summary>
        public static void Heal(Vector3 pos, int count = 3)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle * 0.5f;
                Emit(Instance.heal, pos + new Vector3(c.x, Random.Range(0f, 1.2f), c.y), Vector3.up * Random.Range(0.6f, 1.2f),
                    new Color(0.3f, 1f, 0.45f), Random.Range(0.15f, 0.28f), Random.Range(0.6f, 1f));
            }
        }

        /// <summary>Kombinasi cepat untuk benturan proyektil / pukulan.</summary>
        public static void Impact(Vector3 pos, Vector3 dir, Color color, float scale = 1f)
        {
            Glow(pos, color, 1.4f * scale, 0.12f);
            Sparks(pos, dir, color, Mathf.RoundToInt(10 * scale), 6f * scale);
            Smoke(pos, new Color(0.35f, 0.33f, 0.38f, 0.6f), Mathf.RoundToInt(3 * scale), 0.6f * scale, 1f);
        }

        /// <summary>Ledakan meriah untuk pickup (key / artifact).</summary>
        public static void PickupBurst(Vector3 pos, Color color)
        {
            Ring(pos, color, 2.2f, 0.5f);
            Glow(pos + Vector3.up, color, 3f, 0.35f);
            Sparks(pos + Vector3.up, Vector3.up, color, 26, 6f, 1.2f);
        }

        // =====================================================
        // PUBLIC API — OBJEK SEMENTARA
        // =====================================================

        /// <summary>Busur tebasan senjata di depan owner.</summary>
        public static void Slash(Transform owner, Color color, float radius = 2.2f, float arcDegrees = 140f, float height = 1.1f)
        {
            if (Instance == null || owner == null) return;
            SlashArc.Spawn(owner, Instance.additive, color, radius, arcDegrees, height);

            // Percikan di ujung busur (sisi kanan, arah sapuan berakhir)
            Vector3 fwd = owner.forward;
            fwd.y = 0f;
            Vector3 tip = Quaternion.Euler(0f, arcDegrees * 0.5f, 0f) * fwd.normalized;
            Vector3 tipPos = owner.position + Vector3.up * height + tip * radius * 0.9f;
            Sparks(tipPos, Vector3.Cross(Vector3.up, tip), color, 6, 5f, 0.5f);
        }

        /// <summary>Garis peringatan lurus (jalur Charge).</summary>
        public static void LineTelegraph(Vector3 start, Vector3 dir, float length, float width, float duration, Color color)
        {
            if (Instance == null) return;
            TelegraphLine.Spawn(Instance.line != null ? Instance.line : Instance.alphaBlend, start, dir, length, width, duration, color);
        }

        /// <summary>Aura partikel yang menempel pada target (mis. Guardian saat enrage).</summary>
        public static ParticleSystem Aura(Transform target, Color color, float radius = 1f, float height = 2.5f)
        {
            if (Instance == null || target == null) return null;
            var go = new GameObject("Aura");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = color;
            main.gravityModifier = -0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            go.transform.localPosition = Vector3.up * (height * 0.15f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Instance.additive;
            ps.Play();
            return ps;
        }

        // =====================================================
        // PUBLIC API — TEKS & ALERT
        // =====================================================

        public static void DamageNumber(Vector3 pos, float amount, Color color, bool big = false)
        {
            if (Instance == null) return;
            Instance.texts.Add(new FloatingText
            {
                Position = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.2f, 0.2f)),
                Text = Mathf.RoundToInt(amount).ToString(),
                Color = color,
                Born = Time.unscaledTime,
                Life = 0.8f,
                Size = big ? 34f : 24f,
                Rise = 1.6f
            });
        }

        public static void FloatingLabel(Vector3 pos, string text, Color color, float size = 26f)
        {
            if (Instance == null) return;
            Instance.texts.Add(new FloatingText
            {
                Position = pos, Text = text, Color = color, Born = Time.unscaledTime, Life = 1.3f, Size = size, Rise = 1f
            });
        }

        /// <summary>Ikon "!" / "?" di atas kepala enemy saat state AI berubah.</summary>
        public static void Alert(Transform target, string symbol, Color color, float headHeight = 2.4f)
        {
            if (Instance == null || target == null) return;
            Instance.texts.RemoveAll(t => t.Follow == target);
            Instance.texts.Add(new FloatingText
            {
                Follow = target,
                FollowOffset = Vector3.up * headHeight,
                Text = symbol,
                Color = color,
                Born = Time.unscaledTime,
                Life = 1.1f,
                Size = 40f,
                Rise = 0.25f
            });
        }

        // =====================================================
        // PUBLIC API — WAKTU & KAMERA
        // =====================================================

        /// <summary>Hit-stop: game "membeku" sesaat agar pukulan terasa berat.</summary>
        public static void HitStop(float duration)
        {
            if (Instance == null) return;
            Instance.hitStopUntil = Mathf.Max(Instance.hitStopUntil, Time.unscaledTime + duration);
        }

        /// <summary>Slow-motion dramatis (mis. Guardian mati, Artifact diambil).</summary>
        public static void SlowMotion(float scale, float duration)
        {
            if (Instance == null) return;
            Instance.slowMoScale = scale;
            Instance.slowMoUntil = Time.unscaledTime + duration;
        }

        public static void Shake(float amount) => TopDownCamera.Shake(amount);

        private void Update()
        {
            float target = 1f;
            if (Time.unscaledTime < slowMoUntil)
                target = slowMoScale;
            if (Time.unscaledTime < hitStopUntil)
                target = 0.02f;
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
                target = 0f;
            Time.timeScale = target;

            texts.RemoveAll(t => Time.unscaledTime - t.Born > t.Life || (t.Follow != null && !t.Follow.gameObject.activeInHierarchy));
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || texts.Count == 0)
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            foreach (FloatingText t in texts)
            {
                float age = (Time.unscaledTime - t.Born) / t.Life;
                Vector3 world = (t.Follow != null ? t.Follow.position + t.FollowOffset : t.Position) + Vector3.up * (t.Rise * age);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z < 0f)
                    continue;

                // Pop: membesar cepat lalu mengecil ke ukuran normal, memudar di akhir
                float pop = age < 0.12f ? Mathf.Lerp(1.9f, 1f, age / 0.12f) : 1f;
                float alpha = age > 0.7f ? Mathf.InverseLerp(1f, 0.7f, age) : 1f;
                int size = Mathf.RoundToInt(t.Size * pop);
                textStyle.fontSize = size;

                var rect = new Rect(screen.x - 100f, Screen.height - screen.y - size, 200f, size * 1.6f);
                textStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.85f);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), t.Text, textStyle);
                textStyle.normal.textColor = new Color(t.Color.r, t.Color.g, t.Color.b, alpha);
                GUI.Label(rect, t.Text, textStyle);
            }
        }
    }
}
