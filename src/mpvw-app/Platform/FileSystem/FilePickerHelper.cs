using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Platform.FileSystem
{
    /// <summary>
    /// The app's four file dialogs, on the Windows App SDK pickers.
    ///
    /// These used to be Windows.Storage.Pickers (the UWP surface), which the WinUI
    /// analyzer flags as WUI1001: that API is only present for compatibility and the
    /// Windows App SDK ships its own replacement. The replacement is NOT a namespace
    /// rename -- verified with `winapp find-api --project-dir`:
    ///
    ///   * it returns PickFileResult / PickFolderResult, which expose a Path and
    ///     nothing else. There is no StorageFile any more, so the return types below
    ///     changed rather than the bodies.
    ///   * it takes a WindowId in the constructor, so the old
    ///     InitializeWithWindow/GetWindowHandle dance is gone. That is why
    ///     TryAttachWindow disappeared from this file.
    ///   * SuggestedStartFolder is a String path where the old picker wanted a
    ///     StorageFolder, and SuggestedStartLocation is a different enum namespace.
    ///     Both are set by callers, so they moved to the same names on the new type
    ///     and nothing had to change shape.
    ///
    /// Every caller already used only `.Path`, so the call sites are unchanged apart
    /// from their lambdas' parameter type.
    /// </summary>
    public static class FilePickerHelper
    {
        /// <summary>
        /// The window every picker must be given.
        ///
        /// Null means the app is not showing a window yet, and a picker cannot be
        /// opened without one -- the same condition the old TryAttachWindow guard
        /// expressed with a bool. Both failure paths log, deliberately: callers read
        /// null as "the user cancelled", so without this a missing window would look
        /// like a cancel forever, and this is the only place that can tell them
        /// apart.
        /// </summary>
        private static bool TryGetWindowId(out WindowId windowId)
        {
            windowId = default;
            if (App.Window is null)
            {
                AppContext.AppLogger.Warn("picker not opened: no window");
                return false;
            }
            try
            {
                windowId = App.Window.AppWindow.Id;
                return true;
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "picker not opened: no AppWindow id");
                return false;
            }
        }

        public static async Task<PickFileResult?> PickSingleFileAsync(Action<FileOpenPicker>? configure = null)
        {
            if (!TryGetWindowId(out var windowId))
            {
                return null;
            }

            var picker = new FileOpenPicker(windowId);
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            return await picker.PickSingleFileAsync();
        }

        public static async Task<PickFileResult?> PickSaveFileAsync(Action<FileSavePicker>? configure = null)
        {
            if (!TryGetWindowId(out var windowId))
            {
                return null;
            }

            var picker = new FileSavePicker(windowId);
            configure?.Invoke(picker);
            EnsureFileTypeChoices(picker.FileTypeChoices);

            return await picker.PickSaveFileAsync();
        }

        /// <summary>
        /// Folder picker returning plain paths, ONE folder per invocation.
        ///
        /// The Windows App SDK picker does have PickMultipleFoldersAsync -- the
        /// comment that used to sit here claimed no multi-select existed, which was
        /// true of the UWP picker and is not true of this one. Single selection is
        /// kept deliberately: the caller (AddLibraryFoldersAsync) appends one folder
        /// per press to a bound list, so a multi-select would change what the button
        /// means rather than what it can do. Switching it is a separate decision.
        ///
        /// Null means "the user cancelled", which callers treat as "change nothing"
        /// rather than as an empty selection.
        /// </summary>
        public static async Task<IReadOnlyList<string>?> PickFoldersAsync(Action<FolderPicker>? configure = null)
        {
            if (!TryGetWindowId(out var windowId))
            {
                return null;
            }

            var picker = new FolderPicker(windowId);
            configure?.Invoke(picker);

            var folder = await picker.PickSingleFolderAsync();

            // Explicit array cast: a bare collection expression here is typed as
            // IReadOnlyList<string>, which has no concrete type for the WinRT
            // marshaller and is not trim/AOT safe (CsWinRT1032). One folder, one
            // element, and the concrete array is what the boundary needs.
            return folder is null ? null : (string[])[folder.Path];
        }

        /// <summary>
        /// Multi-file picker.
        ///
        /// This used to be called PickFolderAsync and take a FolderPicker, which was
        /// a misnomer kept alive by an overload: it opens a FILE picker and returns
        /// files. The name is now honest, and the ambiguous overload is gone -- the
        /// call site that relied on its lambda's parameter type to disambiguate no
        /// longer has to.
        /// </summary>
        public static async Task<IReadOnlyList<PickFileResult>?> PickMultipleFilesAsync(Action<FileOpenPicker>? configure = null)
        {
            if (!TryGetWindowId(out var windowId))
            {
                return null;
            }

            var picker = new FileOpenPicker(windowId);
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

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
    }
}
