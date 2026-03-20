using System.Collections.Generic;

using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Stores the Key binding for each gameplay action.
    ///     Consumed by InputSnapshotBuilder for continuous and edge-triggered input.
    ///     Persisted via UserPreferences as string key names.
    /// </summary>
    public sealed class KeyBindingConfig
    {
        /// <summary>Key bound to the move-forward action.</summary>
        public Key MoveForward { get; set; } = Key.W;

        /// <summary>Key bound to the move-back action.</summary>
        public Key MoveBack { get; set; } = Key.S;

        /// <summary>Key bound to the strafe-left action.</summary>
        public Key MoveLeft { get; set; } = Key.A;

        /// <summary>Key bound to the strafe-right action.</summary>
        public Key MoveRight { get; set; } = Key.D;

        /// <summary>Key bound to the sprint action.</summary>
        public Key Sprint { get; set; } = Key.LeftShift;

        /// <summary>Key bound to the jump action.</summary>
        public Key Jump { get; set; } = Key.Space;

        /// <summary>Key bound to the fly-mode toggle action.</summary>
        public Key FlyToggle { get; set; } = Key.F;

        /// <summary>Key bound to the noclip toggle action.</summary>
        public Key NoclipToggle { get; set; } = Key.N;

        /// <summary>Key bound to the inventory toggle action.</summary>
        public Key Inventory { get; set; } = Key.E;

        /// <summary>
        ///     Serializes all bindings to a dictionary of action-name to key-name pairs.
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> dict = new()
            {
                ["MoveForward"] = MoveForward.ToString(),
                ["MoveBack"] = MoveBack.ToString(),
                ["MoveLeft"] = MoveLeft.ToString(),
                ["MoveRight"] = MoveRight.ToString(),
                ["Sprint"] = Sprint.ToString(),
                ["Jump"] = Jump.ToString(),
                ["FlyToggle"] = FlyToggle.ToString(),
                ["NoclipToggle"] = NoclipToggle.ToString(),
                ["Inventory"] = Inventory.ToString(),
            };

            return dict;
        }

        /// <summary>
        ///     Creates a KeyBindingConfig from a dictionary of action-name to key-name pairs.
        ///     Unknown keys are silently ignored, keeping the default binding.
        /// </summary>
        public static KeyBindingConfig FromDictionary(Dictionary<string, string> dict)
        {
            KeyBindingConfig config = new();

            if (dict is null)
            {
                return config;
            }

            if (dict.TryGetValue("MoveForward", out string moveForward) &&
                TryParseKey(moveForward, out Key mf))
            {
                config.MoveForward = mf;
            }

            if (dict.TryGetValue("MoveBack", out string moveBack) &&
                TryParseKey(moveBack, out Key mb))
            {
                config.MoveBack = mb;
            }

            if (dict.TryGetValue("MoveLeft", out string moveLeft) &&
                TryParseKey(moveLeft, out Key ml))
            {
                config.MoveLeft = ml;
            }

            if (dict.TryGetValue("MoveRight", out string moveRight) &&
                TryParseKey(moveRight, out Key mr))
            {
                config.MoveRight = mr;
            }

            if (dict.TryGetValue("Sprint", out string sprint) &&
                TryParseKey(sprint, out Key sp))
            {
                config.Sprint = sp;
            }

            if (dict.TryGetValue("Jump", out string jump) &&
                TryParseKey(jump, out Key jp))
            {
                config.Jump = jp;
            }

            if (dict.TryGetValue("FlyToggle", out string flyToggle) &&
                TryParseKey(flyToggle, out Key ft))
            {
                config.FlyToggle = ft;
            }

            if (dict.TryGetValue("NoclipToggle", out string noclipToggle) &&
                TryParseKey(noclipToggle, out Key nt))
            {
                config.NoclipToggle = nt;
            }

            if (dict.TryGetValue("Inventory", out string inventory) &&
                TryParseKey(inventory, out Key inv))
            {
                config.Inventory = inv;
            }

            return config;
        }

        /// <summary>
        ///     Attempts to parse a string as an InputSystem Key enum value.
        ///     Rejects Key.None and Key.IMESelected as invalid bindings.
        /// </summary>
        private static bool TryParseKey(string name, out Key key)
        {
            return System.Enum.TryParse(name, out key) && key is not Key.None and not Key.IMESelected;
        }
    }
}
