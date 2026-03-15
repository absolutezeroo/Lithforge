using System.IO;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Renders benchmark frame-time data as a PNG bar chart.
    /// Uses a 3x5 bitmap font for axis labels. Output is self-contained — no runtime dependencies.
    /// </summary>
    public static class BenchmarkPngWriter
    {
        // 3x5 bitmap font glyphs: 0-9 (indices 0-9), '.' (index 10).
        // Each glyph is 5 rows (top to bottom). Each row: bit 2=left, bit 1=center, bit 0=right.
        private static readonly byte[][] s_fontGlyphs = new byte[][]
        {
            new byte[] { 7, 5, 5, 5, 7 }, // 0
            new byte[] { 2, 6, 2, 2, 7 }, // 1
            new byte[] { 7, 1, 7, 4, 7 }, // 2
            new byte[] { 7, 1, 7, 1, 7 }, // 3
            new byte[] { 5, 5, 7, 1, 1 }, // 4
            new byte[] { 7, 4, 7, 1, 7 }, // 5
            new byte[] { 7, 4, 7, 5, 7 }, // 6
            new byte[] { 7, 1, 1, 1, 1 }, // 7
            new byte[] { 7, 5, 7, 5, 7 }, // 8
            new byte[] { 7, 5, 7, 1, 7 }, // 9
            new byte[] { 0, 0, 0, 0, 2 }, // .
        };

        public static void Write(BenchmarkResult result, string outputDir, string timestamp)
        {
            int count = result.TotalFrames;

            if (count < 2)
            {
                return;
            }

            const int marginLeft = 50;
            const int marginRight = 10;
            const int marginTop = 10;
            const int marginBottom = 10;
            const int maxGraphWidth = 1800;
            const int graphHeight = 400;

            int graphWidth;
            int framesPerBin;

            if (count <= maxGraphWidth)
            {
                graphWidth = count;
                framesPerBin = 1;
            }
            else
            {
                graphWidth = maxGraphWidth;
                framesPerBin = (count + maxGraphWidth - 1) / maxGraphWidth;
            }

            int imgWidth = graphWidth + marginLeft + marginRight;
            int imgHeight = graphHeight + marginTop + marginBottom;

            Color32[] pixels = new Color32[imgWidth * imgHeight];

            // Background
            Color32 bgColor = new Color32(25, 25, 30, 255);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }

            // Graph area background
            Color32 graphBgColor = new Color32(35, 35, 42, 255);

            for (int gy = 0; gy < graphHeight; gy++)
            {
                for (int gx = 0; gx < graphWidth; gx++)
                {
                    pixels[(marginBottom + gy) * imgWidth + (marginLeft + gx)] = graphBgColor;
                }
            }

            // Find max frame time
            float maxMs = 0f;

            for (int f = 0; f < count; f++)
            {
                if (result.FrameMs[f] > maxMs)
                {
                    maxMs = result.FrameMs[f];
                }
            }

            // Auto-scale Y axis
            float yMax;

            if (maxMs <= 20f)
            {
                yMax = 20f;
            }
            else if (maxMs <= 34f)
            {
                yMax = 34f;
            }
            else if (maxMs <= 50f)
            {
                yMax = 50f;
            }
            else if (maxMs <= 100f)
            {
                yMax = 100f;
            }
            else if (maxMs <= 200f)
            {
                yMax = 200f;
            }
            else
            {
                yMax = maxMs * 1.1f;
            }

            // Reference lines
            Color32 line60Color = new Color32(255, 255, 255, 100);
            Color32 line30Color = new Color32(220, 80, 80, 100);
            Color32 labelColor = new Color32(200, 200, 200, 255);

            DrawHorizontalLine(pixels, imgWidth, imgHeight,
                marginLeft, graphWidth, marginBottom, graphHeight, 16.667f, yMax, line60Color);

            int lineY60 = Mathf.RoundToInt((16.667f / yMax) * graphHeight);
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom + lineY60 - 5, "16.7", labelColor);

            int lineY30 = Mathf.RoundToInt((33.333f / yMax) * graphHeight);

            if (lineY30 < graphHeight)
            {
                DrawHorizontalLine(pixels, imgWidth, imgHeight,
                    marginLeft, graphWidth, marginBottom, graphHeight, 33.333f, yMax, line30Color);
                DrawTextOnPixels(pixels, imgWidth, imgHeight,
                    2, marginBottom + lineY30 - 5, "33.3", labelColor);
            }

            // Y-axis labels: 0 and max
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom, "0", labelColor);
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom + graphHeight - 12, yMax.ToString("F0"), labelColor);

            // Frame time bars
            Color32 greenColor = new Color32(0, 200, 0, 255);
            Color32 yellowColor = new Color32(220, 200, 0, 255);
            Color32 redColor = new Color32(220, 40, 40, 255);

            for (int bin = 0; bin < graphWidth; bin++)
            {
                int startFrame = bin * framesPerBin;
                int endFrame = Mathf.Min(startFrame + framesPerBin, count);

                float ms = 0f;
                int binCount = 0;

                for (int f = startFrame; f < endFrame; f++)
                {
                    ms += result.FrameMs[f];
                    binCount++;
                }

                if (binCount > 0)
                {
                    ms /= binCount;
                }

                int barH = Mathf.Clamp(Mathf.RoundToInt((ms / yMax) * graphHeight), 1, graphHeight);

                Color32 barColor;

                if (ms < 16.667f)
                {
                    barColor = greenColor;
                }
                else if (ms < 33.333f)
                {
                    barColor = yellowColor;
                }
                else
                {
                    barColor = redColor;
                }

                int px = marginLeft + bin;

                for (int row = 0; row < barH; row++)
                {
                    int py = marginBottom + row;

                    if (py >= 0 && py < imgHeight && px >= 0 && px < imgWidth)
                    {
                        pixels[py * imgWidth + px] = barColor;
                    }
                }
            }

            // Encode to PNG
            Texture2D tex = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string safeName = result.ScenarioName.Replace(' ', '_');

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }

            string path = Path.Combine(outputDir, safeName + "_" + timestamp + ".png");

            Directory.CreateDirectory(outputDir);
            File.WriteAllBytes(path, png);
            UnityEngine.Debug.Log("[Benchmark] PNG written to: " + path);
        }

        private static void DrawHorizontalLine(
            Color32[] pixels, int imgW, int imgH,
            int graphX, int graphW, int graphY, int graphH,
            float ms, float yMax, Color32 color)
        {
            int y = graphY + Mathf.RoundToInt((ms / yMax) * graphH);

            if (y < 0 || y >= imgH)
            {
                return;
            }

            for (int x = graphX; x < graphX + graphW; x++)
            {
                // Dashed: 4px on, 4px off
                if (((x - graphX) / 4) % 2 == 0 && x >= 0 && x < imgW)
                {
                    pixels[y * imgW + x] = color;
                }
            }
        }

        private static void DrawTextOnPixels(
            Color32[] pixels, int imgW, int imgH,
            int x, int y, string text, Color32 color)
        {
            const int glyphW = 3;
            const int glyphH = 5;
            const int scale = 2;

            int cx = x;

            for (int c = 0; c < text.Length; c++)
            {
                char ch = text[c];
                int glyphIndex;

                if (ch >= '0' && ch <= '9')
                {
                    glyphIndex = ch - '0';
                }
                else if (ch == '.')
                {
                    glyphIndex = 10;
                }
                else
                {
                    cx += (glyphW + 1) * scale;
                    continue;
                }

                byte[] glyph = s_fontGlyphs[glyphIndex];

                for (int row = 0; row < glyphH; row++)
                {
                    byte rowBits = glyph[row];

                    for (int col = 0; col < glyphW; col++)
                    {
                        if ((rowBits & (1 << (glyphW - 1 - col))) != 0)
                        {
                            for (int sy = 0; sy < scale; sy++)
                            {
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = cx + col * scale + sx;
                                    int py = y + (glyphH - 1 - row) * scale + sy;

                                    if (px >= 0 && px < imgW && py >= 0 && py < imgH)
                                    {
                                        pixels[py * imgW + px] = color;
                                    }
                                }
                            }
                        }
                    }
                }

                cx += (glyphW + 1) * scale;
            }
        }
    }
}
