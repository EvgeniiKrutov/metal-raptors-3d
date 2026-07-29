using System;

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
        void Adjust(int delta);
        void ActivateFocused();
    }

    /// <summary>
    /// One thing a <see cref="MenuPanel"/> can put its highlight on — a text entry
    /// (<see cref="MenuItemView"/>) or a value selector (<see cref="MenuSelectorRow"/>).
    /// See docs/main-menu.md.
    /// </summary>
    public interface IMenuFocusable
    {
        event Action<IMenuFocusable> Hovered;

        void SetFocused(bool focused);
        void Activate();

        /// <summary>Left/right while focused. True when the entry consumed it, so the
        /// panel leaves the highlight where it is.</summary>
        bool Adjust(int delta);
    }
}
