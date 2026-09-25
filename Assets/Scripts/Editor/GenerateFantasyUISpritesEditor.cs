using System.IO;
using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Generates high-resolution, pixel-crisp fantasy UI sprites for the HUD:
    /// dark slate panels, burnished gold frames, ruby vitality bars, pill badges, and filigree dividers.
    /// All textures are saved as 9-slice PNGs in Assets/UI/Sprites/.
    /// </summary>
    public static class GenerateFantasyUISpritesEditor
    {
        private const string SpritesDir = "Assets/UI/Sprites";

        [MenuItem("CastleOfDice/Generate Fantasy HUD Sprites", false, 40)]
        public static void GenerateAllSprites()
        {
            if (!Directory.Exists(SpritesDir))
            {
                Directory.CreateDirectory(SpritesDir);
            }

            CreatePanelDarkSprite();
            CreateSlotFrameSprite();
            CreateBarTrackSprite();
            CreateBarFillRubySprite();
            CreatePillBadgeSprite();
            CreateDividerGoldSprite();
            CreateCrestPlateSprite();

            AssetDatabase.Refresh();
            ConfigureSpriteImports();
            Debug.Log("[GenerateFantasyUISpritesEditor] Successfully generated all Fantasy HUD Sprites!");
        }

        #region Sprite Creators

        private static void CreatePanelDarkSprite()
        {
            int w = 128;
            int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color bgTop = new Color(0.08f, 0.10f, 0.15f, 0.95f);      // #141926
            Color bgBottom = new Color(0.05f, 0.07f, 0.10f, 0.96f);   // #0D121A
            Color goldOuter = new Color(0.40f, 0.28f, 0.10f, 1f);     // #66471A
            Color goldMain = new Color(0.80f, 0.62f, 0.24f, 1f);      // #C89E3D
            Color goldInner = new Color(0.92f, 0.78f, 0.42f, 1f);     // #EBC76B
            Color rivetColor = new Color(1.0f, 0.88f, 0.50f, 1f);

            float radius = 14f;

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                Color currentBg = Color.Lerp(bgBottom, bgTop, v);

                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > -1.5f)
                    {
                        // Outer gold border edge
                        colors[idx] = goldOuter;
                    }
                    else if (dist > -3.5f)
                    {
                        // Main burnished gold border
                        colors[idx] = goldMain;
                    }
                    else if (dist > -4.5f)
                    {
                        // Inner fine gold highlight
                        colors[idx] = goldInner;
                    }
                    else if (dist > -6.0f)
                    {
                        // Inner dark groove
                        colors[idx] = new Color(0.02f, 0.03f, 0.05f, 0.98f);
                    }
                    else
                    {
                        // Fill panel with subtle vignette
                        float cx = (x - w * 0.5f) / (w * 0.5f);
                        float cy = (y - h * 0.5f) / (h * 0.5f);
                        float radDist = Mathf.Sqrt(cx * cx + cy * cy);
                        Color fill = Color.Lerp(currentBg, new Color(0.04f, 0.05f, 0.08f, 0.96f), radDist * 0.4f);
                        colors[idx] = fill;
                    }
                }
            }

            // Draw corner rivets
            DrawRivet(colors, w, h, 14, 14, 3, rivetColor, goldOuter);
            DrawRivet(colors, w, h, w - 15, 14, 3, rivetColor, goldOuter);
            DrawRivet(colors, w, h, 14, h - 15, 3, rivetColor, goldOuter);
            DrawRivet(colors, w, h, w - 15, h - 15, 3, rivetColor, goldOuter);

            SaveTexture(tex, colors, "UI_Fantasy_Panel_Dark.png");
        }

        private static void CreateSlotFrameSprite()
        {
            int w = 64;
            int h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color bg = new Color(0.05f, 0.06f, 0.08f, 0.96f);
            Color rimOuter = new Color(0.35f, 0.25f, 0.08f, 1f);
            Color rimGold = new Color(0.82f, 0.65f, 0.26f, 1f);
            Color rimHighlight = new Color(0.96f, 0.85f, 0.52f, 1f);

            float radius = 8f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > -1.5f)
                    {
                        colors[idx] = rimOuter;
                    }
                    else if (dist > -3.0f)
                    {
                        colors[idx] = rimGold;
                    }
                    else if (dist > -4.0f)
                    {
                        colors[idx] = rimHighlight;
                    }
                    else if (dist > -6.0f)
                    {
                        colors[idx] = new Color(0.02f, 0.02f, 0.03f, 1f);
                    }
                    else
                    {
                        colors[idx] = bg;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Slot_Frame.png");
        }

        private static void CreateBarTrackSprite()
        {
            int w = 64;
            int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color trackBg = new Color(0.08f, 0.03f, 0.04f, 0.98f);
            Color trackShadow = new Color(0.02f, 0.01f, 0.02f, 1f);
            Color borderDark = new Color(0.28f, 0.18f, 0.08f, 1f);
            Color borderGold = new Color(0.70f, 0.52f, 0.18f, 1f);

            float radius = 6f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > -1.5f)
                    {
                        colors[idx] = borderDark;
                    }
                    else if (dist > -2.5f)
                    {
                        colors[idx] = borderGold;
                    }
                    else if (y >= h - 5)
                    {
                        colors[idx] = trackShadow;
                    }
                    else
                    {
                        colors[idx] = trackBg;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Bar_Track.png");
        }

        private static void CreateBarFillRubySprite()
        {
            int w = 64;
            int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color rubyDark = new Color(0.55f, 0.06f, 0.06f, 1f);     // #8C0F0F
            Color rubyMid = new Color(0.78f, 0.15f, 0.12f, 1f);      // #C7261F
            Color rubyBright = new Color(0.92f, 0.28f, 0.22f, 1f);   // #EB4738
            Color rubyGloss = new Color(1.0f, 0.65f, 0.60f, 0.95f);  // Top highlight

            float radius = 5f;

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                Color currentRuby;
                if (v < 0.5f)
                {
                    currentRuby = Color.Lerp(rubyDark, rubyMid, v * 2f);
                }
                else
                {
                    currentRuby = Color.Lerp(rubyMid, rubyBright, (v - 0.5f) * 2f);
                }

                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (y >= h - 4 && dist <= -1.5f)
                    {
                        // Gloss shine line along the top of health fill
                        colors[idx] = Color.Lerp(rubyGloss, currentRuby, (h - 1 - y) * 0.3f);
                    }
                    else
                    {
                        colors[idx] = currentRuby;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Bar_Fill_Ruby.png");
        }

        private static void CreatePillBadgeSprite()
        {
            int w = 64;
            int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color pillBg = new Color(0.09f, 0.11f, 0.16f, 0.95f);
            Color pillGold = new Color(0.85f, 0.68f, 0.25f, 1f);
            Color pillOuter = new Color(0.35f, 0.25f, 0.08f, 1f);

            float radius = 12f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > -1.5f)
                    {
                        colors[idx] = pillOuter;
                    }
                    else if (dist > -3.0f)
                    {
                        colors[idx] = pillGold;
                    }
                    else
                    {
                        colors[idx] = pillBg;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Pill_Badge.png");
        }

        private static void CreateDividerGoldSprite()
        {
            int w = 256;
            int h = 16;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color goldBright = new Color(0.95f, 0.82f, 0.40f, 1f);
            Color goldMid = new Color(0.78f, 0.58f, 0.20f, 0.85f);

            int cx = w / 2;
            int cy = h / 2;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    int dx = Mathf.Abs(x - cx);
                    int dy = Mathf.Abs(y - cy);

                    // Central diamond
                    if (dx + dy <= 6)
                    {
                        colors[idx] = goldBright;
                    }
                    else if (dy <= 1)
                    {
                        // Tapering line
                        float alpha = Mathf.Clamp01(1f - ((float)dx / (w * 0.5f)));
                        alpha = Mathf.Pow(alpha, 1.5f);
                        Color c = Color.Lerp(goldMid, goldBright, alpha);
                        c.a *= alpha;
                        colors[idx] = c;
                    }
                    else
                    {
                        colors[idx] = Color.clear;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Divider_Gold.png");
        }

        private static void CreateCrestPlateSprite()
        {
            int w = 64;
            int h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];

            Color plateBg = new Color(0.12f, 0.15f, 0.22f, 0.98f);
            Color goldRim = new Color(0.85f, 0.68f, 0.25f, 1f);
            Color darkRim = new Color(0.28f, 0.20f, 0.08f, 1f);

            float radius = 10f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = GetRoundedRectDist(x, y, w, h, radius);
                    int idx = y * w + x;

                    if (dist > 0.5f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > -1.5f)
                    {
                        colors[idx] = darkRim;
                    }
                    else if (dist > -3.5f)
                    {
                        colors[idx] = goldRim;
                    }
                    else
                    {
                        colors[idx] = plateBg;
                    }
                }
            }

            SaveTexture(tex, colors, "UI_Fantasy_Crest_Plate.png");
        }

        #endregion

        #region Helpers

        private static float GetRoundedRectDist(int x, int y, int w, int h, float r)
        {
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float px = Mathf.Abs(x - halfW) - (halfW - r);
            float py = Mathf.Abs(y - halfH) - (halfH - r);

            if (px <= 0 && py <= 0) return -r;
            if (px <= 0) return py - r;
            if (py <= 0) return px - r;

            return Mathf.Sqrt(px * px + py * py) - r;
        }

        private static void DrawRivet(Color[] colors, int w, int h, int rx, int ry, int r, Color rivetColor, Color shadowColor)
        {
            for (int y = ry - r; y <= ry + r; y++)
            {
                for (int x = rx - r; x <= rx + r; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float d = Mathf.Sqrt((x - rx) * (x - rx) + (y - ry) * (y - ry));
                    if (d <= r)
                    {
                        int idx = y * w + x;
                        if (y > ry)
                        {
                            colors[idx] = rivetColor;
                        }
                        else
                        {
                            colors[idx] = Color.Lerp(rivetColor, shadowColor, 0.5f);
                        }
                    }
                }
            }
        }

        private static void SaveTexture(Texture2D tex, Color[] colors, string fileName)
        {
            tex.SetPixels(colors);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            string path = Path.Combine(SpritesDir, fileName);
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);
        }

        public static void ConfigureSpriteImports()
        {
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Panel_Dark.png", new Vector4(20, 20, 20, 20));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Slot_Frame.png", new Vector4(12, 12, 12, 12));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Bar_Track.png", new Vector4(8, 8, 8, 8));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Bar_Fill_Ruby.png", new Vector4(8, 8, 8, 8));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Pill_Badge.png", new Vector4(14, 10, 14, 10));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Divider_Gold.png", new Vector4(8, 2, 8, 2));
            SetSpriteImporter($"{SpritesDir}/UI_Fantasy_Crest_Plate.png", new Vector4(12, 12, 12, 12));
        }

        private static void SetSpriteImporter(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        #endregion
    }
}
