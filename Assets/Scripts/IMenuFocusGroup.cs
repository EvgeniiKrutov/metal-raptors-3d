using System;

namespace MetalRaptors
{
    public interface IMenuFocusGroup
    {
        void MoveFocus(int delta);
        void Adjust(int delta);
        void ActivateFocused();
    }

    public interface IMenuRow : IMenuFocusGroup
    {
        event Action<int> FocusChanged;

        void Refocus();
        void Blur();
    }

    public interface IMenuFocusable
    {
        event Action<IMenuFocusable> Hovered;

        void SetFocused(bool focused);
        void Activate();

        bool Adjust(int delta);
    }

    public interface IMenuOptionRow : IMenuFocusable
    {
        event Action<IMenuOptionRow> Engaged;

        void SetLive(bool live);
    }
}
