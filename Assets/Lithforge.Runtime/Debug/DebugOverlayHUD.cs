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

        private void OnGUI()
        {
            int y = 10;
            int lineHeight = 20;

            GUI.color = Color.white;
            GUI.Label(new Rect(10, y, 400, lineHeight), $"FPS: {_fps:F1}");
            y += lineHeight;

            if (_chunkManager != null)
            {
                GUI.Label(new Rect(10, y, 400, lineHeight),
                    $"Loaded Chunks: {_chunkManager.LoadedCount}");
                y += lineHeight;
            }

            if (_gameLoop != null)
            {
                GUI.Label(new Rect(10, y, 400, lineHeight),
                    $"Pending Gen: {_gameLoop.PendingGenerationCount}");
                y += lineHeight;

                GUI.Label(new Rect(10, y, 400, lineHeight),
                    $"Pending Mesh: {_gameLoop.PendingMeshCount}");
                y += lineHeight;
            }

            if (Camera.main != null)
            {
                Vector3 pos = Camera.main.transform.position;
                GUI.Label(new Rect(10, y, 400, lineHeight),
                    $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }
        }
    }
}
