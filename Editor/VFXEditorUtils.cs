using UnityEngine;

namespace VFXTools.Editor
{
    public static class VFXEditorUtils
    {
        private const string ToolVersion = "v0.22.4";

        public static Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Color GetContrastColor(Color backgroundColor)
        {
            float relativeLuminance = GetRelativeLuminance(backgroundColor);
            
            float contrastWithWhite = (1.0f + 0.05f) / (relativeLuminance + 0.05f);
            float contrastWithBlack = (relativeLuminance + 0.05f) / (0.0f + 0.05f);
            
            if (contrastWithWhite >= contrastWithBlack)
            {
                return Color.white;
            }
            else
            {
                return Color.black;
            }
        }

        public static float GetRelativeLuminance(Color color)
        {
            float r = color.r <= 0.03928f ? color.r / 12.92f : Mathf.Pow((color.r + 0.055f) / 1.055f, 2.4f);
            float g = color.g <= 0.03928f ? color.g / 12.92f : Mathf.Pow((color.g + 0.055f) / 1.055f, 2.4f);
            float b = color.b <= 0.03928f ? color.b / 12.92f : Mathf.Pow((color.b + 0.055f) / 1.055f, 2.4f);
            
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        public static float GetContrastRatio(Color color1, Color color2)
        {
            float lum1 = GetRelativeLuminance(color1);
            float lum2 = GetRelativeLuminance(color2);
            
            float lighter = Mathf.Max(lum1, lum2);
            float darker = Mathf.Min(lum1, lum2);
            
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        public static string GetToolVersion()
        {
            return ToolVersion;
        }
    }
}