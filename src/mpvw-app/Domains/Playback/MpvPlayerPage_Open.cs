using mpv_winui.Infrastructure.Common.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace mpv_winui.Domains.Playback
{
    public sealed partial class MpvPlayerPage
    {
        private async Task OpenFileAsync()
        {
            var picker = new FileOpenPicker(_appWindow.Id);
            var files = await picker.PickMultipleFilesAsync();
            if (files?.Count > 0)
            {
                await PlayFiles(files.Select(x => x.Path).ToList(), OpenMode.Replace);
            }
        }

        private async Task OpenFolderAsync()
        {
            var picker = new FolderPicker(_appWindow.Id);
            var folders = await picker.PickMultipleFoldersAsync();
            if (folders?.Count > 0)
            {
                await PlayFolders(folders.Select(x => x.Path).ToList(), OpenMode.Replace);
            }
        }

        private async Task OpenUrlAsync()
        {
            var urlBox = new TextBox
            {
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                PlaceholderText = "http://...",
                MinHeight = 80,
                MaxHeight = 200,
                MinWidth = 400
            };
            var urlDialog = new ContentDialog
            {
                Title = "Open URL",
                Content = urlBox,
                PrimaryButtonText = "Open",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            _suppressKeyboard = true;
            try
            {
                if (await urlDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (urlBox.Text?.Trim() is string path && !string.IsNullOrEmpty(path))
                    {
                        await PlayUrl(path, OpenMode.Replace);
                    }
                }
            }
            finally
            {
                _suppressKeyboard = false;
            }
        }

        private async Task OpenClipboardAsync()
        {
            var package = Clipboard.GetContent();
            if (package.Contains(StandardDataFormats.Text))
            {
                var text = await package.GetTextAsync();
                if (text?.Trim() is string path && !string.IsNullOrEmpty(path))
                {
                    await PlayUrl(path, OpenMode.Replace);
                }
            }
            else if (package.Contains(StandardDataFormats.Uri))
            {
                var uri = await package.GetUriAsync();
                if (uri?.ToString() is string path && !string.IsNullOrEmpty(path))
                {
                    await PlayUrl(path, OpenMode.Replace);
                }
            }
            else if (package.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await package.GetStorageItemsAsync();
                if (storageItems?.Count > 0)
                {
                    await PlayStorageItems(storageItems, OpenMode.Replace);
                }
            }
        }

        private async Task LoadSubtitleAsync()
        {
            var subPicker = new FileOpenPicker(_appWindow.Id);
            var subFile = await subPicker.PickSingleFileAsync();
            if (!string.IsNullOrEmpty(subFile?.Path))
            {
                _mediaPlayer.AddSubtitle(subFile.Path, true, "");
            }
        }

        //TODO list
        private IReadOnlyList<FileItem>? _pendingPaths;
        private async ValueTask OpenPendingPath()
        {
            if (_pendingPaths is { } paths && paths.Count > 0)
            {
                await _mediaPlayer.OpenAsync(paths, OpenMode.Replace);
            }
            _pendingPaths = null;
        }

        // move
        private async ValueTask PlayStorageItems(IReadOnlyList<IStorageItem> storageItems, OpenMode openMode)
        {
            var items = storageItems
                .Where(x => !x.IsOfType(StorageItemTypes.None))
                .Select(x => new FileItem(x.Path, x.IsOfType(Windows.Storage.StorageItemTypes.File) ? FileType.File : FileType.Folder))
                .ToList();

            if (items?.Count > 0)
            {
                await _mediaPlayer.OpenAsync(items, openMode);
            }
        }

        private async ValueTask PlayFiles(IReadOnlyList<string> files, OpenMode openMode)
        {
            // The playing marker is moved HERE rather than at each call site, because
            // there are four of them that can start a library file: a card click, a
            // card's hover button, the album page's play-all, and the history list.
            // Only the first two went through PlayLibraryEntry, which is why the flag
            // was never set when an album was played from its own page - measured as
            // a marker that never appeared.
            MarkPlayingFirst(files);

            var items = files.Select(file => new FileItem(file, FileType.File)).ToList();
            await _mediaPlayer.OpenAsync(items, openMode);
        }

        /// <summary>
        /// Marks the library entry for the first file about to play.
        ///
        /// The first file, not every file: an album queues all of its tracks, and the
        /// card that should light up is the one for the track that is playing now.
        /// Whatever the queue advances to later is the queue's business, not a card's.
        ///
        /// A path with no entry in the library is not an error - a dropped file, a URL,
        /// a history row that has aged out - so this does nothing rather than failing
        /// the play.
        /// </summary>
        private void MarkPlayingFirst(IReadOnlyList<string> files)
        {
            if (files.Count == 0)
            {
                return;
            }

            var first = files[0];

            var entry = _libraryAll.FirstOrDefault(
                e => string.Equals(e.Path, first, StringComparison.OrdinalIgnoreCase));

            if (entry is not null)
            {
                MarkNowPlaying(entry);
            }
        }

        private async ValueTask PlayFolders(IReadOnlyList<string> folders, OpenMode openMode)
        {
            var items = folders.Select(file => new FileItem(file, FileType.Folder)).ToList();
            await _mediaPlayer.OpenAsync(items, openMode);
        }

        private async ValueTask PlayUrl(string url, OpenMode openMode)
        {
            await _mediaPlayer.OpenAsync((FileItem[])[new FileItem(url, FileType.Url)], openMode);
        }

    }
}