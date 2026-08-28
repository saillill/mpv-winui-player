using System;
using Windows.ApplicationModel.DataTransfer;

namespace mpv_winui.Modules.Common.Utils
{
    public static class ClipboardHelper
    {
        public static void SetCopyText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SetContent(package => package.SetText(text));
        }

        private static void SetContent(Action<DataPackage> fill)
        {
            try
            {
                var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                fill(data);
                Clipboard.SetContent(data);
                // Without Flush the copied data is lost as soon as the app
                // exits; media players copy URLs/paths for use elsewhere.
                Clipboard.Flush();
            }
            catch (Exception)
            {
                // The clipboard can be transiently locked by another process;
                // a failed copy is not worth crashing the player for.
            }
        }
    }
}
