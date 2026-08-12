using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using mpv_winui.Modules.Common.View;
using System;
using Windows.Graphics;
using WinRT;

namespace mpv_winui
{
    public sealed partial class MainWindow : Window
    {
        public const int MIN_LOGICAL_WIDTH = 250;
        public const int MIN_LOGICAL_HEIGHT = 250;

        // The centered (modernx) control-bar layout needs more width than the
        // classic bar before its button clusters start overlapping.
        private const int MIN_LOGICAL_WIDTH_MODERNX = 400;

        private static int GetMinLogicalWidth()
        {
            var layout = AppContext.AppSetting.ControlBarLayout;
            return layout is "modernx" or "center" or "centered" or "right"
                ? MIN_LOGICAL_WIDTH_MODERNX
                : MIN_LOGICAL_WIDTH;
        }

        private int _x;
        private int _y;
        private int _w;
        private int _h;

        private void SetupWindowSize()
        {
            var lastRect = string.Empty;
            try
            {
                if (AppContext.AppSetting.WindowRememberSize)
                {
                    lastRect = AppContext.AppSetting.WindowPositionAndSize;
                    if (!string.IsNullOrEmpty(lastRect))
                    {
                        int[] v = Array.ConvertAll(lastRect.Split(','), int.Parse);
                        if (v.Length == 4)
                        {
                            _x = v[0];
                            _y = v[1];
                            _w = v[2];
                            _h = v[3];
                            if (_w > 0 && _h > 0)
                            {
                                var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
                                var width = Math.Min(Math.Max(100, _w), Math.Max(100, workArea.Width - 40));
                                var height = Math.Min(Math.Max(100, _h), Math.Max(100, workArea.Height - 40));
                                var x = Math.Clamp(_x, workArea.X, workArea.X + workArea.Width - width);
                                var y = Math.Clamp(_y, workArea.Y, workArea.Y + workArea.Height - height);
                                AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "restore window position and size failed, saved={}", lastRect);
            }

            this.Body.Loaded += Body_Startup_Loaded;
            this.Body.Loaded += Body_Loaded;
            this.Body.Unloaded += Body_Unloaded;

            AppWindow.Changed += Size_AppWindow_Changed;
            Closed += Size_Window_Closed;
        }

        private void Size_AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange && !args.DidPositionChange)
            {
                return;
            }

            if (sender.Presenter.Kind is AppWindowPresenterKind.Overlapped)
            {
                var overlappedPresenter = AppWindow.Presenter.As<OverlappedPresenter>();
                if (overlappedPresenter != null)
                {
                    if (overlappedPresenter.State is OverlappedPresenterState.Restored)
                    {
                        _x = sender.Position.X;
                        _y = sender.Position.Y;
                        _w = sender.Size.Width;
                        _h = sender.Size.Height;
                    }
                }
            }

            if (AppContext.AppLogger.IsTraceEnabled)
            {
                AppContext.AppLogger.Debug("window last rect: x={},y={},w={},h={}.", _x, _y, _w, _h);
            }
        }

        private void Size_Window_Closed(object sender, WindowEventArgs args)
        {
            Closed -= Size_Window_Closed;
            AppWindow.Changed -= Size_AppWindow_Changed;

            SaveWindowPositionAndSize();
        }

        private void Body_Startup_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppContext.AppSetting.WindowStartMaximized
                && string.IsNullOrEmpty(AppContext.AppSetting.WindowPositionAndSize)
                && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }

        public void SaveWindowPositionAndSize()
        {
            try
            {
                if (AppContext.AppSetting.WindowRememberSize)
                {
                    AppContext.AppSetting.WindowPositionAndSize = $"{_x},{_y},{_w},{_h}";
                }
                if (AppContext.AppLogger.IsTraceEnabled)
                {
                    AppContext.AppLogger.Debug("save window position and size: x={},y={},w={},h={}.", _x, _y, _w, _h);
                }
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "save window position and size failed");
            }
        }

        private void Body_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement body)
            {
                body.XamlRoot?.Changed += RootGridXamlRoot_Changed;
                this.SetWindowMinSize(GetMinLogicalWidth(), MIN_LOGICAL_HEIGHT);
            }
        }

        private void Body_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement body)
            {
                body.XamlRoot?.Changed -= RootGridXamlRoot_Changed;
            }
        }

        private void RootGridXamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            this.SetWindowMinSize(GetMinLogicalWidth(), MIN_LOGICAL_HEIGHT);
        }
    }
}
