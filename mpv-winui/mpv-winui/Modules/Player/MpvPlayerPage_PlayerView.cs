using Microsoft.UI.Xaml;
using mpv_winui.Modules.Common.Utils;
using System;

namespace mpv_winui.Modules.Player
{
    public record struct ViewSize(double Width, double Height, double WidthScale, double HidthScale);

    public sealed partial class MpvPlayerPage
    {
        private Action<ViewSize>? _sizeChangedAction;
        private bool _playerViewLoaded = false;
        private void SetupPlayerView()
        {
            var size = new ViewSize(PlayerView.ActualWidth, PlayerView.ActualHeight, PlayerView.CompositionScaleX, PlayerView.CompositionScaleY);
            UpdatePlayerViewSize(size);
            _mediaPlayer.AttachSwapChain(PlayerView);
            _playerViewLoaded = true;

            // Throttle, not debounce: during a drag-resize the panel size
            // changes continuously and the composition swapchain must track it
            // in near-real-time. The old 100ms debounce restarted on every
            // change, so the video only caught up ~100ms after the pointer
            // stopped - which read as "the picture lags the window".
            _sizeChangedAction = DebounceUtil.Throttle<ViewSize>(UpdatePlayerViewSize, TimeSpan.FromMilliseconds(16));
            PlayerView.SizeChanged += PlayerView_SizeChanged;

            _lastCompositionScaleX = PlayerView.CompositionScaleX;
            _lastCompositionScaleY = PlayerView.CompositionScaleY;
            PlayerView.CompositionScaleChanged += PlayerView_CompositionScaleChanged;
        }

        private void TeardownPlayerView()
        {
            _sizeChangedAction = null;
            PlayerView.SizeChanged -= PlayerView_SizeChanged;
            PlayerView.CompositionScaleChanged -= PlayerView_CompositionScaleChanged;
            _mediaPlayer.VoConfigured -= MpvPlayer_SwapChainChanged;
        }

        private void MpvPlayer_SwapChainChanged()
        {
            DispatcherQueue.RunAsync(() =>
            {
                if (_playerViewLoaded)
                {
                    var size = new ViewSize(PlayerView.ActualWidth, PlayerView.ActualHeight, PlayerView.CompositionScaleX, PlayerView.CompositionScaleY);
                    UpdatePlayerViewSize(size);
                    _mediaPlayer.AttachSwapChain(PlayerView);
                }
            });
        }

        private async void PlayerView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isPlayerInitialized)
            {
                return;
            }

            var size = new ViewSize(e.NewSize.Width, e.NewSize.Height, PlayerView.CompositionScaleX, PlayerView.CompositionScaleY);
            _sizeChangedAction?.Invoke(size);
        }

        private void UpdatePlayerViewSize(ViewSize size)
        {
            // Ceil, never floor: at fractional DPI (e.g. 175%) the logical
            // width * scale lands a fraction below the physical size (3839.99
            // vs 3840), and flooring leaves a 1px seam on the right edge of
            // fullscreen. The swap chain is clipped by the panel, so a 1px
            // overshoot is invisible while undershoot shows the background.
            var width = (uint)Math.Ceiling(size.Width * size.WidthScale);
            var height = (uint)Math.Ceiling(size.Height * size.HidthScale);
            if (width <= 0)
            {
                width = 1;
            }
            if (height <= 0)
            {
                height = 1;
            }

            // Publish the panel's physical size: the window aspect fit must
            // target the panel (menu row + control row take the rest of the
            // client), not the whole client.
            MainWindow.VideoPanelPhysicalWidth = width;
            MainWindow.VideoPanelPhysicalHeight = height;

            _mediaPlayer?.UpdateSize(width, height);
        }

        private double _lastCompositionScaleX;
        private double _lastCompositionScaleY;
        private void PlayerView_CompositionScaleChanged(Microsoft.UI.Xaml.Controls.SwapChainPanel sender, object args)
        {
            if (_lastCompositionScaleX != sender.CompositionScaleX || _lastCompositionScaleY != sender.CompositionScaleY)
            {
                _lastCompositionScaleX = sender.CompositionScaleX;
                _lastCompositionScaleY = sender.CompositionScaleY;

                _mediaPlayer?.UpdateSwapChainScale(sender.CompositionScaleX, sender.CompositionScaleY);

                var size = new ViewSize(sender.ActualWidth, sender.ActualHeight, sender.CompositionScaleX, sender.CompositionScaleY);
                _sizeChangedAction?.Invoke(size);
            }
        }
    }
}
