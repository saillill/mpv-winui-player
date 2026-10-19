using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace mpv_winui.Modules.FileSystem
{
    public static class FilePickerHelper
    {
        public static async Task<StorageFile?> PickSingleFileAsync(Action<FileOpenPicker>? configure = null)
        {
            var picker = new FileOpenPicker();
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSingleFileAsync();
        }

        public static async Task<StorageFile?> PickSaveFileAsync(Action<FileSavePicker>? configure = null)
        {
            var picker = new FileSavePicker();
            configure?.Invoke(picker);
            EnsureFileTypeChoices(picker.FileTypeChoices);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSaveFileAsync();
        }

        public static async Task<StorageFolder?> PickFolderAsync(Action<FolderPicker>? configure = null)
        {
            var picker = new FolderPicker();
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSingleFolderAsync();
        }

        /// <summary>
        /// Folder picker returning plain paths.
        ///
        /// FolderPicker is the only picker here that yields folders rather than
        /// files, and it needs the window handle attached before it will show in a
        /// desktop app - hence the same TryAttachWindow guard as its siblings.
        /// Null means "the user cancelled", which the caller treats as "change
        /// nothing" rather than as an empty selection.
        /// </summary>
        public static async Task<IReadOnlyList<string>?> PickFoldersAsync(Action<FolderPicker>? configure = null)
        {
            var picker = new FolderPicker();

            configure?.Invoke(picker);

            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            // FolderPicker has no multi-select in the Windows App SDK surface
            // (only FileOpenPicker does), so one folder per invocation. The
            // caller adds to the bound list, which is what makes adding a second
            // folder a second press rather than a lost selection.
            var folder = await picker.PickSingleFolderAsync();

            return folder is null ? null : [folder.Path];
        }

        public static async Task<IReadOnlyList<StorageFile>?> PickFolderAsync(Action<FileOpenPicker>? configure = null)
        {
            var picker = new FileOpenPicker();
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickMultipleFilesAsync();
        }

        private static void EnsureFileTypeFilter(IList<string> filter)
        {
            if (filter.Count == 0)
            {
                filter.Add("*");
            }
        }

        private static void EnsureFileTypeChoices(IDictionary<string, IList<string>> choices)
        {
            if (choices.Count == 0)
            {
                choices["All files"] = new List<string> { "*" };
            }
        }

        public static bool TryAttachWindow(object picker)
        {
            if (App.Window is null)
            {
                return false;
            }

            var hwnd = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(picker, hwnd);
            return true;
        }
    }
}
