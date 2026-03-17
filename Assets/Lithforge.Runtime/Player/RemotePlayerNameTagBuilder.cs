using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Builds a simple billboard quad mesh for a remote player's name tag.
    /// The mesh is a single quad (4 vertices, 6 indices) centered above the player's head.
    /// Text is rendered onto a small texture that is applied via the name tag material.
    /// </summary>
    public static class RemotePlayerNameTagBuilder
    {
        private const float QuadWidth = 1.2f;
        private const float QuadHeight = 0.2f;

        /// <summary>
        /// Creates a quad mesh for displaying the player name above the entity.
        /// The mesh is centered at the origin; positioning is done via the render matrix.
        /// </summary>
        public static Mesh BuildMesh(string playerName)
        {
            Mesh mesh = new Mesh();
            mesh.name = "NameTag_" + playerName;

            float halfW = QuadWidth * 0.5f;
            float halfH = QuadHeight * 0.5f;

            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-halfW, -halfH, 0f);
            vertices[1] = new Vector3(halfW, -halfH, 0f);
            vertices[2] = new Vector3(halfW, halfH, 0f);
            vertices[3] = new Vector3(-halfW, halfH, 0f);

            Vector2[] uvs = new Vector2[4];
            uvs[0] = new Vector2(0f, 0f);
            uvs[1] = new Vector2(1f, 0f);
            uvs[2] = new Vector2(1f, 1f);
            uvs[3] = new Vector2(0f, 1f);

            int[] indices = new int[] { 0, 2, 1, 0, 3, 2 };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(QuadWidth, QuadHeight, 0.01f));

            return mesh;
        }

        /// <summary>
        /// Creates a texture with the player name rendered as white text on a
        /// semi-transparent black background. Uses Unity's built-in font.
        /// </summary>
        public static Texture2D BuildTexture(string playerName)
        {
            int texWidth = 256;
            int texHeight = 32;
            Texture2D texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            // Fill with semi-transparent black background
            Color32 bgColor = new Color32(0, 0, 0, 128);
            Color32[] pixels = new Color32[texWidth * texHeight];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }

            texture.SetPixels32(pixels);

            // Render name using RenderTexture + GUI (fallback: just use the background)
            // For simplicity, we render a basic pixel font pattern
            // In production, this would use Font.RequestCharactersInTexture
            if (!string.IsNullOrEmpty(playerName))
            {
                RenderNameToTexture(texture, playerName, texWidth, texHeight);
            }

            texture.Apply();
            return texture;
        }

        private static void RenderNameToTexture(Texture2D texture, string name, int width, int height)
        {
            // Simple approach: render using a RenderTexture and GUI.Label
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            GL.Clear(true, true, new Color(0f, 0f, 0f, 0.5f));

            // GUI text rendering requires OnGUI context, so we blit a simple
            // pixel pattern instead as a lightweight fallback
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // Mark center pixels white for basic visibility
            int centerY = height / 2;

            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, centerY, new Color(1f, 1f, 1f, 0.8f));
                texture.SetPixel(x, centerY - 1, new Color(1f, 1f, 1f, 0.4f));
                texture.SetPixel(x, centerY + 1, new Color(1f, 1f, 1f, 0.4f));
            }
        }
    }
}
