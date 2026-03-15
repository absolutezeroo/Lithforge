namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Minimal interface allowing <see cref="ContainerScreenManager"/> and
    /// <see cref="HudVisibilityController"/> to operate on screens without
    /// knowing their concrete type. Implemented by <see cref="ContainerScreen"/>.
    /// </summary>
    public interface IContainerScreen
    {
        public bool IsOpen { get; }

        public void Close();

        public void SetVisible(bool visible);
    }
}
