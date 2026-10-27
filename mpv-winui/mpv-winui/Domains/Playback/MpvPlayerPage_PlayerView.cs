using Microsoft.UI.Xaml;
using mpv_winui.Infrastructure.Common.Utils;
using System;

namespace mpv_winui.Domains.Playback
{
    public record struct ViewSize(double Width, double Height, double WidthScale, double HidthScale);

    public sealed partial class MpvPlayerPage
    {
        private Action<ViewSize>? _sizeChangedAction;
        private void SetupPlayerView()
        {
            var size = new ViewSize(PlayerView.ActualWidth, PlayerView.ActualHeight, PlayerView.CompositionScaleX, PlayerView.CompositionScaleY);
            UpdatePlayerViewSize(size);
            _mediaPlayer.AttachSwapChain(PlayerView);

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
        }

        // MpvPlayer_SwapChainChanged used to live here: it re-measured the panel
        // and re-attached the swap chain on VoConfigured. That event has not
        // been raised since c49b461 renamed the raise to m_swapChainChangedEvent,
        // so the handler could not run -- and the work it did is native now
        // (222be73: AttachSwapChain records the panel, CompletePendingSwapChainAttach
        // completes it on VIDEO_RECONFIG). Panel size is covered by
        // PlayerView_SizeChanged and PlayerView_CompositionScaleChanged above.
        // Re-wiring it to SwapChainChanged would attach the chain twice.

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

            // INFO only in the degenerate case, and that case is the point.
            //
            // `1x1` is what the two clamps above produce when the panel has no
            // measured size, and it is indistinguishable from "everything worked"
            // in every other log line. A player that loads a file and shows
            // nothing looks exactly like a player whose swap chain was sized to a
            // square millimetre, so the one time it happens has to say so.
            if (width <= 1 || height <= 1)
            {
                PlayChain.SurfaceLost(string.Format(
                    "video panel measured {0}x{1} physical (logical {2}x{3}, scale {4}x{5})",
                    width, height, size.Width, size.Height, size.WidthScale, size.HidthScale));
            }
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
