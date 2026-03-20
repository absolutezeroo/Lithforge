using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Chat
{
    /// <summary>
    ///     UI Toolkit chat overlay panel. Shows a scrollable message list and text input.
    ///     Toggled with T key. Sends ChatCmdMessage on submit.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        /// <summary>Maximum number of messages retained in the scrollable history.</summary>
        private const int MaxMessages = 100;

        /// <summary>Time in seconds before the chat panel auto-fades when unfocused.</summary>
        private const float FadeDelay = 8f;

        /// <summary>The root UI document for this panel.</summary>
        private UIDocument _document;

        /// <summary>The scrollable container for chat messages.</summary>
        private ScrollView _messageList;

        /// <summary>The text input field for typing messages.</summary>
        private TextField _inputField;

        /// <summary>The root visual element of the chat panel.</summary>
        private VisualElement _root;

        /// <summary>Whether the input field is currently focused and accepting input.</summary>
        private bool _isActive;

        /// <summary>Cached message elements for cleanup when exceeding MaxMessages.</summary>
        private readonly List<Label> _messageLabels = new();

        /// <summary>Time when the last message was received or input was deactivated.</summary>
        private float _lastActivityTime;

        /// <summary>
        ///     Callback invoked when the user submits a chat message or command.
        ///     The string parameter is the full text content.
        /// </summary>
        public Action<string> OnSubmit;

        /// <summary>Initializes the chat panel with a UI Toolkit panel settings reference.</summary>
        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 500;

            VisualTreeAsset template = Resources.Load<VisualTreeAsset>("UI/Chat/ChatPanel");

            if (template is not null)
            {
                _root = template.Instantiate();
                _document.rootVisualElement.Add(_root);
                _messageList = _root.Q<ScrollView>("chat-messages");
                _inputField = _root.Q<TextField>("chat-input");
            }
            else
            {
                // Fallback: build UI programmatically
                _root = new VisualElement();
                _root.style.position = Position.Absolute;
                _root.style.bottom = 40;
                _root.style.left = 8;
                _root.style.width = 400;
                _root.style.maxHeight = 300;
                _root.pickingMode = PickingMode.Ignore;

                _messageList = new ScrollView(ScrollViewMode.Vertical);
                _messageList.style.flexGrow = 1;
                _messageList.pickingMode = PickingMode.Ignore;
                _root.Add(_messageList);

                _inputField = new TextField();
                _inputField.style.display = DisplayStyle.None;
                _inputField.maxLength = 256;
                _root.Add(_inputField);

                _document.rootVisualElement.Add(_root);
            }

            if (_inputField is not null)
            {
                _inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown);
            }

            _lastActivityTime = Time.realtimeSinceStartup;
        }

        /// <summary>Adds a received chat message to the display.</summary>
        public void AddMessage(string senderName, string content)
        {
            if (_messageList == null)
            {
                return;
            }

            string display = string.IsNullOrEmpty(senderName)
                ? content
                : $"<{senderName}> {content}";

            Label label = new(display);
            label.style.color = new StyleColor(Color.white);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.pickingMode = PickingMode.Ignore;

            _messageList.Add(label);
            _messageLabels.Add(label);

            // Trim old messages
            while (_messageLabels.Count > MaxMessages)
            {
                Label oldest = _messageLabels[0];
                _messageList.Remove(oldest);
                _messageLabels.RemoveAt(0);
            }

            _messageList.ScrollTo(label);
            _lastActivityTime = Time.realtimeSinceStartup;
        }

        /// <summary>Adds a system message (no sender name).</summary>
        public void AddSystemMessage(string content)
        {
            AddMessage("", content);
        }

        /// <summary>Toggles the chat input field active/inactive.</summary>
        public void ToggleInput()
        {
            _isActive = !_isActive;

            if (_inputField == null)
            {
                return;
            }

            if (_isActive)
            {
                _inputField.style.display = DisplayStyle.Flex;
                _inputField.value = "";
                _inputField.Focus();
                _root.style.opacity = 1f;
            }
            else
            {
                _inputField.style.display = DisplayStyle.None;
                _lastActivityTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>Returns true if the chat input is currently active.</summary>
        public bool IsInputActive
        {
            get { return _isActive; }
        }

        /// <summary>Updates fade logic each frame.</summary>
        private void Update()
        {
            if (_isActive || _root == null)
            {
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _lastActivityTime;

            if (elapsed > FadeDelay)
            {
                float fade = Mathf.Clamp01(1f - (elapsed - FadeDelay) / 2f);
                _root.style.opacity = fade;
            }
            else
            {
                _root.style.opacity = 1f;
            }
        }

        /// <summary>Handles key events in the input field (Enter to submit, Escape to cancel).</summary>
        private void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                string text = _inputField?.value?.Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    OnSubmit?.Invoke(text);
                }

                ToggleInput();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                ToggleInput();
                evt.StopPropagation();
            }
        }
    }
}
