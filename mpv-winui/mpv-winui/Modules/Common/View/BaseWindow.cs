using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace mpv_winui.Modules.Common.View
{
    public abstract partial class BaseWindow : Window, IWindowStyleRefreshSupport
    {
        protected WindowStyleManager? _styleManager;

        protected BaseWindow()
        {
            Closed += OnClosed;
        }

        protected void SetupStyle()
        {
            _styleManager = new WindowStyleManager(this);
            _styleManager?.Setup();
        }

        protected void CleanupStyle()
        {
            _styleManager?.Dispose();
            _styleManager = null;
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            Closed -= OnClosed;
            CleanupStyle();
        }

        public void MoveAndResize(RectInt32 rect)
        {
            AppWindow?.MoveAndResize(rect);
        }

        public virtual void UpdateTheme()
        {
            var theme = _styleManager?.GetThemeType();
            if (theme is not null)
            {
                _styleManager?.UpdateTheme(theme.Value);
            }
        }

        public virtual void UpdateBackdrop()
        {
            // The upstream WindowStyleManager re-reads the backdrop material from
            // the current setting itself (ApplyBackdrop), so there is no longer a
            // material argument to thread through here.
            _styleManager?.UpdateBackdrop();
        }
    }
}
