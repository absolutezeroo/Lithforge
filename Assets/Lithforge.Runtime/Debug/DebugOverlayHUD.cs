using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using UnityEngine;

namespace Lithforge.Runtime.Debug
{
    public sealed class DebugOverlayHUD : MonoBehaviour
    {
        private GameLoop _gameLoop;
        private Voxel.Chunk.ChunkManager _chunkManager;
        private Rendering.ChunkRenderManager _chunkRenderManager;
        private PlayerController _playerController;

        private Camera _mainCamera;
        private float _fps;
        private float _fpsTimer;
        private int _frameCount;
        private GUIStyle _style;
        private Texture2D _bgTexture;
        private bool _visible = true;

        // Settings
        private float _fpsSampleInterval = 0.5f;
        private float _overlayBackgroundAlpha = 0.6f;
        private int _overlayMinFontSize = 18;
        private int _overlayScreenDivisor = 50;
        private int _overlayPanelWidth = 420;
        private int _overlayPadding = 8;
        private int _overlayLineSpacing = 6;

        /// <summary>
        /// Shows or hides the debug overlay. When hidden, OnGUI returns immediately.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        public void Initialize(
            GameLoop gameLoop,
            Voxel.Chunk.ChunkManager chunkManager,
            DebugSettings settings,
            Rendering.ChunkRenderManager chunkRenderManager = null,
            PlayerController playerController = null)
        {
            _gameLoop = gameLoop;
            _chunkManager = chunkManager;
            _fpsSampleInterval = settings.FpsSampleInterval;
            _overlayBackgroundAlpha = settings.OverlayBackgroundAlpha;
            _overlayMinFontSize = settings.OverlayMinFontSize;
            _overlayScreenDivisor = settings.OverlayScreenDivisor;
            _overlayPanelWidth = settings.OverlayPanelWidth;
            _overlayPadding = settings.OverlayPadding;
            _overlayLineSpacing = settings.OverlayLineSpacing;
            _chunkRenderManager = chunkRenderManager;
            _playerController = playerController;
            _visible = settings.ShowDebugOverlay;
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= _fpsSampleInterval)
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
            _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, _overlayBackgroundAlpha));
            _bgTexture.Apply();

            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = Mathf.Max(_overlayMinFontSize, Screen.height / _overlayScreenDivisor);
            _style.normal.textColor = Color.white;
            _style.fontStyle = FontStyle.Bold;
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureStyle();

            int lineHeight = _style.fontSize + _overlayLineSpacing;
            int padding = _overlayPadding;
            int y = padding;
            int x = padding;
            int lineCount = 2;
            int labelWidth = _overlayPanelWidth - padding * 2;

            if (_chunkManager != null) { lineCount++; }
            if (_chunkRenderManager != null) { lineCount++; }
            if (_gameLoop != null) { lineCount += 2; }
            if (_playerController != null && _playerController.IsFlying) { lineCount++; }

            // Background panel
            int panelHeight = lineCount * lineHeight + padding * 2;
            GUI.DrawTexture(new Rect(0, 0, _overlayPanelWidth, panelHeight), _bgTexture);

            if (_mainCamera != null)
            {
                Vector3 pos = _mainCamera.transform.position;
                GUI.Label(new Rect(x, y, labelWidth, lineHeight),
                    $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", _style);
                y += lineHeight;
            }

            GUI.Label(new Rect(x, y, labelWidth, lineHeight), $"FPS: {_fps:F1}", _style);
            y += lineHeight;

            if (_chunkManager != null)
            {
                GUI.Label(new Rect(x, y, labelWidth, lineHeight),
                    $"Chunks: {_chunkManager.LoadedCount}", _style);
                y += lineHeight;
            }

            if (_chunkRenderManager != null)
            {
                GUI.Label(new Rect(x, y, labelWidth, lineHeight),
                    $"Renderers: {_chunkRenderManager.RendererCount}", _style);
                y += lineHeight;
            }

            if (_gameLoop != null)
            {
                GUI.Label(new Rect(x, y, labelWidth, lineHeight),
                    $"Gen Queue: {_gameLoop.PendingGenerationCount}", _style);
                y += lineHeight;

                GUI.Label(new Rect(x, y, labelWidth, lineHeight),
                    $"Mesh Queue: {_gameLoop.PendingMeshCount}  LOD Queue: {_gameLoop.PendingLODMeshCount}", _style);
                y += lineHeight;
            }

            if (_playerController != null && _playerController.IsFlying)
            {
                string flyLabel = _playerController.IsNoclip
                    ? $"FLY [noclip] {_playerController.FlySpeed:F1} b/s"
                    : $"FLY {_playerController.FlySpeed:F1} b/s";
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), flyLabel, _style);
            }
        }
    }
}
