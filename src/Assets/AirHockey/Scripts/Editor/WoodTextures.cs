using System.IO;
using UnityEditor;
using UnityEngine;

namespace AirHockey.EditorTools
{
    /// <summary>木目テクスチャ（アルベド＋法線）を手続き生成する。</summary>
    public static class WoodTextures
    {
        const string Dir = "Assets/AirHockey/Textures";

        public struct WoodStyle
        {
            public Color light, dark;
            public float rings, grain, planks, warp;
            public int seed;
        }

        public static void Generate(string name, WoodStyle s, int size, out Texture2D albedo, out Texture2D normal)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/AirHockey", "Textures");
            var col = new Color[size * size];
            var h = new float[size * size];
            Random.InitState(s.seed);
            float ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // 板ごとのずれ
                    float plank = s.planks > 0 ? Mathf.Floor(u * s.planks) : 0f;
                    float pu = s.planks > 0 ? Mathf.Repeat(u * s.planks, 1f) : u;
                    float shift = plank * 13.7f;
                    float warp = (Mathf.PerlinNoise(ox + u * 3f + shift, oy + v * 1.2f) - 0.5f) * s.warp;
                    float ring = Mathf.Sin((u + warp + shift * 0.1f) * s.rings * Mathf.PI * 2f + Mathf.PerlinNoise(ox + v * 2f, shift) * 6f);
                    ring = Mathf.Pow(ring * 0.5f + 0.5f, 3f);
                    float fine = Mathf.PerlinNoise(ox + u * s.grain * 8f + shift, oy + v * 6f);
                    float t = Mathf.Clamp01(ring * 0.6f + fine * 0.35f + (Mathf.PerlinNoise(shift + 3f, 1f) - 0.5f) * 0.25f);
                    Color c = Color.Lerp(s.light, s.dark, t);
                    float seam = 1f;
                    if (s.planks > 0)
                    {
                        float e = Mathf.Min(pu, 1f - pu) * size / s.planks;
                        seam = Mathf.Clamp01(e / 1.5f);
                        c *= Mathf.Lerp(0.55f, 1f, seam);
                    }
                    c.a = 1f;
                    col[y * size + x] = c;
                    h[y * size + x] = (1f - t) * 0.5f + seam * 0.5f;
                }
            }
            var nrm = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float hl = h[y * size + (x + size - 1) % size], hr = h[y * size + (x + 1) % size];
                    float hd = h[((y + size - 1) % size) * size + x], hu = h[((y + 1) % size) * size + x];
                    var n = new Vector3((hl - hr) * 2f, (hd - hu) * 2f, 1f).normalized;
                    nrm[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            albedo = Save($"{Dir}/{name}_Albedo.png", col, size, false);
            normal = Save($"{Dir}/{name}_Normal.png", nrm, size, true);
        }

        static Texture2D Save(string path, Color[] px, int size, bool isNormal)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, isNormal);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !isNormal;
            imp.anisoLevel = 8;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
