using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.AppModel;

namespace mpv_winui.Modules.About
{
    public sealed partial class AboutControl : UserControl
    {
        private AboutControl()
        {
            this.InitializeComponent();
        }

        public static AboutControl Create(string? mpvVersion)
        {
            var control = new AboutControl();
            var lang = global::mpv_winui.AppContext.AppLang;

            control.AppNameTextBlock.Text = PackageHelper.AppName;
            control.AppVersionTextBlock.Text = PackageHelper.AppVersion;

            control.MpvSectionTitle.Text = "mpv";
            control.MpvVersionTextBlock.Text = string.IsNullOrEmpty(mpvVersion) ? "mpv" : mpvVersion;
            control.MpvLinkButton.Content = "github.com/mpv-player/mpv";

            control.ProjectSectionTitle.Text = PackageHelper.AppName;
            control.ProjectLinkButton.Content = "github.com/ikas-mc/mpv-winui-player";

            return control;
        }
    }
}
