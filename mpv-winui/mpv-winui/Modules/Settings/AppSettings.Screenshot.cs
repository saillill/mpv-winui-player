namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Screenshot format, naming and destination.
    /// </summary>
    public partial class AppSettings
    {
        public string ScreenshotDirectory
        {
            get
            {
                var v = _dataSetting.GetValue(nameof(ScreenshotDirectory), string.Empty);
                return string.IsNullOrWhiteSpace(v) ? DefaultScreenshotDirectory : v;
            }
            set => _dataSetting.SetValue(nameof(ScreenshotDirectory), value);
        }

        public string ScreenshotTemplate
        {
            get => _dataSetting.GetValue(nameof(ScreenshotTemplate), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotTemplate), value);
        }

        public string ScreenshotFormat
        {
            get => _dataSetting.GetValue(nameof(ScreenshotFormat), "png");
            set => _dataSetting.SetValue(nameof(ScreenshotFormat), value);
        }

        public int ScreenshotJpegQuality
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJpegQuality), 90);
            set => _dataSetting.SetValue(nameof(ScreenshotJpegQuality), value);
        }

        public int ScreenshotPngCompression
        {
            get => _dataSetting.GetValue(nameof(ScreenshotPngCompression), 4);
            set => _dataSetting.SetValue(nameof(ScreenshotPngCompression), value);
        }

        public int ScreenshotWebpQuality
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpQuality), 100);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpQuality), value);
        }

        public bool ScreenshotHighBitDepth
        {
            get => _dataSetting.GetValue(nameof(ScreenshotHighBitDepth), false);
            set => _dataSetting.SetValue(nameof(ScreenshotHighBitDepth), value);
        }

        public bool ScreenshotTagColorspace
        {
            get => _dataSetting.GetValue(nameof(ScreenshotTagColorspace), true);
            set => _dataSetting.SetValue(nameof(ScreenshotTagColorspace), value);
        }

        public bool ScreenshotSw
        {
            get => _dataSetting.GetValue(nameof(ScreenshotSw), false);
            set => _dataSetting.SetValue(nameof(ScreenshotSw), value);
        }

        public bool ScreenshotJpegSourceChroma
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJpegSourceChroma), true);
            set => _dataSetting.SetValue(nameof(ScreenshotJpegSourceChroma), value);
        }

        public int ScreenshotPngFilter
        {
            get => _dataSetting.GetValue(nameof(ScreenshotPngFilter), 5);
            set => _dataSetting.SetValue(nameof(ScreenshotPngFilter), value);
        }

        public bool ScreenshotWebpLossless
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpLossless), true);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpLossless), value);
        }

        public int ScreenshotWebpCompression
        {
            get => _dataSetting.GetValue(nameof(ScreenshotWebpCompression), 0);
            set => _dataSetting.SetValue(nameof(ScreenshotWebpCompression), value);
        }

        public int ScreenshotJxlDistance
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlDistance), 0);
            set => _dataSetting.SetValue(nameof(ScreenshotJxlDistance), value);
        }

        public int ScreenshotJxlEffort
        {
            get => _dataSetting.GetValue(nameof(ScreenshotJxlEffort), 4);
            set => _dataSetting.SetValue(nameof(ScreenshotJxlEffort), value);
        }

        public string ScreenshotAvifEncoder
        {
            get => _dataSetting.GetValue(nameof(ScreenshotAvifEncoder), string.Empty);
            set => _dataSetting.SetValue(nameof(ScreenshotAvifEncoder), value);
        }
    }
}
