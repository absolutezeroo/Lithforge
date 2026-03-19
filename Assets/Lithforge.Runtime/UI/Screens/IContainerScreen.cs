namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Minimal interface allowing <see cref="ContainerScreenManager"/> and
    /// <see cref="HudVisibilityController"/> to operate on screens without
    /// knowing their concrete type. Implemented by <see cref="ContainerScreen"/>.
    /// </summary>
    public interface IContainerScreen
    {
        /// <summary>True if the container screen is currently open and visible.</summary>
        public bool IsOpen { get; }

        /// <summary>Closes the container screen and returns held items to inventory.</summary>
        public void Close();

        /// <summary>Controls root document visibility, used during loading screen transitions.</summary>
        public void SetVisible(bool visible);
    }
}
