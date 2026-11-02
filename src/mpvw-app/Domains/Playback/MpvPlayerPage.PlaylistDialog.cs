using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Domains.Playlist;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Domains.Playback
{
    /// <summary>
    /// The one place a playlist name is typed, for both "new list" and "rename".
    ///
    /// <para><b>Validated while the dialog is up, not after it closes.</b> A name
    /// becomes a file name, and a file name can be rejected for reasons the user
    /// has no way to guess: a slash, a colon, a trailing space that looks
    /// identical to no trailing space. Checking afterwards would mean the dialog
    /// closes, nothing happens, and the reason lives in a log the user cannot
    /// read.</para>
    /// </summary>
    public sealed partial class MpvPlayerPage
    {
        /// <summary>
        /// Asks for a name, returning null when the user backed out.
        ///
        /// <paramref name="extraCheck"/> runs after the built-in rules and returns
        /// a message when the name is unusable for a reason this method cannot
        /// know - a rename landing on an existing list, say. Returning null from
        /// it means "fine".
        /// </summary>
        private async Task<string?> PromptForPlaylistNameAsync(
            string initial,
            Func<string, string?>? extraCheck = null)
        {
            var lang = AppContext.AppLang;

            var input = new TextBox
            {
                Text = initial,
                PlaceholderText = lang.PlaylistNamePlaceholder,
            };

            var problem = new TextBlock
            {
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(input);
            panel.Children.Add(problem);

            var dialog = new ContentDialog
            {
                Title = lang.PlaylistCreate,
                Content = panel,
                PrimaryButtonText = lang.PlaylistCreate,
                CloseButtonText = lang.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            // Judged on every keystroke so the primary button is never a trap: a
            // dialog that accepts a bad name and then refuses it after closing is
            // strictly worse than one that will not let you press the button.
            void Evaluate()
            {
                var text = input.Text;

                string? reason = NamedPlaylistFile.IsValidName(text, out var builtIn)
                    ? extraCheck?.Invoke(text)
                    : builtIn;

                dialog.IsPrimaryButtonEnabled = reason is null;

                problem.Text = reason ?? string.Empty;
                problem.Visibility = reason is null ? Visibility.Collapsed : Visibility.Visible;
            }

            input.TextChanged += (_, _) => Evaluate();

            Evaluate();

            input.Loaded += (_, _) =>
            {
                input.Focus(FocusState.Programmatic);
                input.SelectAll();
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return null;
            }

            return input.Text;
        }

        /// <summary>
        /// The empty state's 「新建播放列表」: a name, then an empty list.
        ///
        /// Separate from the selection bar's path, which also puts the ticked
        /// files in: an empty list is what you make before you have anything to
        /// put in it, and requiring a selection first would make the button
        /// unreachable from the one screen that offers it.
        /// </summary>
        private async Task CreateEmptyPlaylistAsync()
        {
            var typed = await PromptForPlaylistNameAsync(
                string.Empty,
                candidate => _playlistStore.List().Any(
                    list => string.Equals(list.Name, candidate, StringComparison.CurrentCultureIgnoreCase))
                        ? AppContext.AppLang.PlaylistNameTaken
                        : null);

            if (typed is null)
            {
                return;
            }

            _playlistStore.Create(typed, []);

            BuildPlaylistRows();
        }
    }
}
