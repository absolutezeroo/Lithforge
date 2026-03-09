using UnityEngine;

namespace Lithforge.Runtime.Debug
{
    public sealed class DebugOverlayHUD : MonoBehaviour
    {
        private GameLoop _gameLoop;
        private Voxel.Chunk.ChunkManager _chunkManager;

        private float _fps;
        private float _fpsTimer;
        private int _frameCount;
        private GUIStyle _style;
        private Texture2D _bgTexture;

        public void Initialize(GameLoop gameLoop, Voxel.Chunk.ChunkManager chunkManager)
        {
            _gameLoop = gameLoop;
            _chunkManager = chunkManager;
        }

        private void Update()
        {
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= 0.5f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }
        }

        private void EnsureStyle()
        {
            if (_style != null)
            {
                return;
            }

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _bgTexture.Apply();

            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = Mathf.Max(18, Screen.height / 50);
            _style.normal.textColor = Color.white;
            _style.fontStyle = FontStyle.Bold;
        }

        private void OnGUI()
        {
            EnsureStyle();

            int lineHeight = _style.fontSize + 6;
            int padding = 8;
            int y = padding;
            int x = padding;
            int lineCount = 2;

            if (_chunkManager != null) { lineCount++; }
            if (_gameLoop != null) { lineCount += 2; }

            // Background panel
            int panelHeight = lineCount * lineHeight + padding * 2;
            GUI.DrawTexture(new Rect(0, 0, 420, panelHeight), _bgTexture);

            if (Camera.main != null)
            {
                Vector3 pos = Camera.main.transform.position;
                GUI.Label(new Rect(x, y, 400, lineHeight),
                    $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", _style);
                y += lineHeight;
            }

            GUI.Label(new Rect(x, y, 400, lineHeight), $"FPS: {_fps:F1}", _style);
            y += lineHeight;

            if (_chunkManager != null)
            {
                GUI.Label(new Rect(x, y, 400, lineHeight),
                    $"Chunks: {_chunkManager.LoadedCount}", _style);
                y += lineHeight;
            }

            if (_gameLoop != null)
            {
                GUI.Label(new Rect(x, y, 400, lineHeight),
                    $"Gen Queue: {_gameLoop.PendingGenerationCount}", _style);
                y += lineHeight;

                GUI.Label(new Rect(x, y, 400, lineHeight),
                    $"Mesh Queue: {_gameLoop.PendingMeshCount}", _style);
            }
        }
    }
}
