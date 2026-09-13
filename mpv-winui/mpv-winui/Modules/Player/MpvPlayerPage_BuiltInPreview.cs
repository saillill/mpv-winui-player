using Microsoft.UI.Xaml;
using System;

namespace mpv_winui.Modules.Player
{
    public sealed partial class MpvPlayerPage
    {
        private bool _enableBuiltInPreview = AppContext.AppSetting.EnableVideoBuiltInPreview;

        /// <summary>
        /// Materialises the built-in preview control (declared with
        /// <c>x:Load</c>, so it is not part of the tree until requested) and
        /// seeds its keep-alive policy from the settings.
        ///
        /// Pointer plumbing is owned by <c>MpvPlayerPage_Preview.cs</c> - this
        /// method only prepares the control itself.
        /// </summary>
        private void SetupBuiltInPreview()
        {
            if (!_enableBuiltInPreview)
            {
                return;
            }

            FindName("MpvPreview");
            if (MpvPreview is null)
            {
                return;
            }

            MpvPreview.KeepAlive = AppContext.AppSetting.KeepVideoBuiltInPreviewAlive;
            MpvPreview.KeepAliveTimeout = AppContext.AppSetting.BuiltInPreviewAliveTimeout;
        }

        private void CleanupBuiltInPreview()
        {
            MpvPreview?.Close();
        }
    }
}
