using System.IO;
using UnityEngine;
using UnityEditor;

namespace TheLastBell.Editor
{
    public static class UISpriteGenerator
    {
        [MenuItem("The Last Bell/UI/Generate Mobile Circle Sprites")]
        public static void GenerateAllSprites()
        {
            string dir = "Assets/Textures/UI";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GenerateCircleButtonSprite(Path.Combine(dir, "circle_button.png"), 256);
            GenerateJoystickBgSprite(Path.Combine(dir, "circle_joystick_bg.png"), 256);
            GenerateJoystickHandleSprite(Path.Combine(dir, "circle_joystick_handle.png"), 128);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureAsSprite(Path.Combine(dir, "circle_button.png"));
            ConfigureAsSprite(Path.Combine(dir, "circle_joystick_bg.png"));
            ConfigureAsSprite(Path.Combine(dir, "circle_joystick_handle.png"));

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[UISpriteGenerator] Mobil daire arayüz sprite'ları başarıyla oluşturuldu!</color>");
        }

        private static void ConfigureAsSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void GenerateCircleButtonSprite(string filePath, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] colors = new Color32[size * size];

            float center = (size - 1) * 0.5f;
            float maxR = center - 2f;
            float borderWidth = 8f;
            float innerR = maxR - borderWidth;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 1.5f)
                    {
                        colors[y * size + x] = new Color32(0, 0, 0, 0);
                    }
                    else if (dist >= innerR)
                    {
                        // Border ring (bright white with anti-aliasing on outer edge)
                        float alpha = 1f;
                        if (dist > maxR - 1f)
                        {
                            alpha = Mathf.Clamp01(maxR + 1f - dist);
                        }
                        byte a = (byte)(alpha * 255);
                        colors[y * size + x] = new Color32(255, 255, 255, a);
                    }
                    else
                    {
                        // Inner circle fill (solid white so tint color controls brightness/transparency)
                        // Slight subtle darkening towards center for depth
                        float t = dist / innerR;
                        byte val = (byte)Mathf.RoundToInt(Mathf.Lerp(210f, 255f, t));
                        colors[y * size + x] = new Color32(val, val, val, 255);
                    }
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateJoystickBgSprite(string filePath, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] colors = new Color32[size * size];

            float center = (size - 1) * 0.5f;
            float maxR = center - 3f;
            float ringWidth = 6f;
            float innerR = maxR - ringWidth;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 1.5f)
                    {
                        colors[y * size + x] = new Color32(0, 0, 0, 0);
                    }
                    else if (dist >= innerR)
                    {
                        // Outer ring
                        float alpha = 1f;
                        if (dist > maxR - 1f)
                        {
                            alpha = Mathf.Clamp01(maxR + 1f - dist);
                        }
                        byte a = (byte)(alpha * 220);
                        colors[y * size + x] = new Color32(255, 255, 255, a);
                    }
                    else
                    {
                        // Inner translucent disc with faint crosshair/center hint
                        float centerDist = dist;
                        float alpha = 0.45f;
                        if (centerDist < 4f) alpha = 0.85f; // Center dot
                        byte a = (byte)(alpha * 255);
                        colors[y * size + x] = new Color32(220, 220, 230, a);
                    }
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateJoystickHandleSprite(string filePath, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] colors = new Color32[size * size];

            float center = (size - 1) * 0.5f;
            float maxR = center - 2f;
            float ringWidth = 4f;
            float innerR = maxR - ringWidth;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 1.5f)
                    {
                        colors[y * size + x] = new Color32(0, 0, 0, 0);
                    }
                    else if (dist >= innerR)
                    {
                        float alpha = 1f;
                        if (dist > maxR - 1f)
                        {
                            alpha = Mathf.Clamp01(maxR + 1f - dist);
                        }
                        byte a = (byte)(alpha * 255);
                        colors[y * size + x] = new Color32(255, 255, 255, a);
                    }
                    else
                    {
                        // Radial gradient: bright in middle, slightly darker towards edge
                        float t = 1f - (dist / innerR);
                        byte val = (byte)Mathf.RoundToInt(Mathf.Lerp(200f, 255f, t));
                        colors[y * size + x] = new Color32(val, val, val, 240);
                    }
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
