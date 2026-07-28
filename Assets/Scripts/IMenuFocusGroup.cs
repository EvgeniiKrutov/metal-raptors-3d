namespace MetalRaptors
{
    /// <summary>
    /// A set of entries owning one highlight, driven by the menu's navigation keys.
    /// Implemented by <see cref="MenuPanel"/> (a vertical stack) and
    /// <see cref="MenuCardRow"/> (a horizontal run of cards).
    /// </summary>
    public interface IMenuFocusGroup
    {
        void MoveFocus(int delta);
        void ActivateFocused();
    }
}
