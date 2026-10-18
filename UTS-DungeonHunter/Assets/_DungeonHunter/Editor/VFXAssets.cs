using System.IO;
using UnityEditor;
using UnityEngine;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Membuat aset VFX secara prosedural (tanpa aset luar):
    /// tekstur partikel (soft dot, ring, disc telegraph) dan material partikel additive / alpha / debris.
    /// </summary>
    public static class VFXAssets
    {
        private const string Dir = "Assets/_DungeonHunter/VFX";

        public static Material Additive { get; private set; }
        public static Material AlphaBlend { get; private set; }
        public static Material Ring { get; private set; }
        public static Material Telegraph { get; private set; }
        public static Material Debris { get; private set; }
        public static Material Trail { get; private set; }
        public static Material Line { get; private set; }

        public static void Create()
        {
            EnsureFolder(Dir);

            Texture2D dot = MakeTexture("VFX_SoftDot", 64, (x, y) =>
            {
                float r = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
                float a = Mathf.Pow(1f - r, 2.2f);
                return a;
            });

            Texture2D ring = MakeTexture("VFX_Ring", 128, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float band = Mathf.Exp(-Mathf.Pow((r - 0.84f) / 0.1f, 2f));
                float inner = r < 0.84f ? 0.3f * r * r : 0f;
                return Mathf.Clamp01(band + inner) * (r <= 1f ? 1f : 0f);
            });

            Texture2D disc = MakeTexture("VFX_Disc", 128, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                if (r > 1f)
                    return 0f;
                float rim = Mathf.Exp(-Mathf.Pow((r - 0.94f) / 0.04f, 2f));
                float fill = 0.45f + 0.2f * r;
                float stripes = 0.08f * Mathf.Clamp01(Mathf.Sin(r * 40f));
                return Mathf.Clamp01(fill + stripes + rim);
            });

            // Strip: alpha hanya bergantung pada posisi melintang (untuk trail & garis)
            Texture2D strip = MakeTexture("VFX_Strip", 64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 1.5f));
            Texture2D edgeStrip = MakeTexture("VFX_EdgeStrip", 64, (x, y) =>
            {
                float ay = Mathf.Abs(y);
                float edge = Mathf.Exp(-Mathf.Pow((ay - 0.82f) / 0.1f, 2f));
                return ay > 0.97f ? 0f : Mathf.Clamp01(0.3f + edge);
            });

            Additive = ParticleMaterial("VFX_Additive", dot, true);
            AlphaBlend = ParticleMaterial("VFX_Alpha", dot, false);
            Ring = ParticleMaterial("VFX_Ring", ring, true);
            Telegraph = ParticleMaterial("VFX_Telegraph", disc, false);
            Trail = ParticleMaterial("VFX_Trail", strip, true);
            Line = ParticleMaterial("VFX_Line", edgeStrip, false);

            string debrisPath = Dir + "/VFX_Debris.mat";
            Debris = AssetDatabase.LoadAssetAtPath<Material>(debrisPath);
            if (Debris == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                Debris = new Material(s);
                AssetDatabase.CreateAsset(Debris, debrisPath);
            }
            Debris.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(Debris);
            AssetDatabase.SaveAssets();
        }

        private static Material ParticleMaterial(string name, Texture2D tex, bool additive)
        {
            string path = $"{Dir}/{name}.mat";
            Shader shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended")
                            ?? Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = tex;
            // Legacy particle shader: hasil = 2 × tint × warna partikel. Additive dibuat lebih terang (1.6×) agar "pop".
            if (mat.HasProperty("_TintColor"))
                mat.SetColor("_TintColor", additive ? new Color(0.8f, 0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Tekstur putih dengan alpha dari fungsi radial f(x, y), x/y dalam [-1, 1].</summary>
        private static Texture2D MakeTexture(string name, int size, System.Func<float, float, float> alpha)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f;
                    float y = (py + 0.5f) / size * 2f - 1f;
                    tex.SetPixel(px, py, new Color(1f, 1f, 1f, alpha(x, y)));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
