namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// On-screen display look and behaviour.
    /// </summary>
    public partial class AppSettings
    {
        /// <summary>Whether to show the playback-start OSD message.</summary>
        public bool ShowOsdPlayingMsg
        {
            get => _dataSetting.GetValue(nameof(ShowOsdPlayingMsg), false);
            set => _dataSetting.SetValue(nameof(ShowOsdPlayingMsg), value);
        }

        public int OsdFontSize
        {
            get => _dataSetting.GetValue(nameof(OsdFontSize), 20);
            set => _dataSetting.SetValue(nameof(OsdFontSize), value);
        }

        public string OsdFont
        {
            get => _dataSetting.GetValue(nameof(OsdFont), "sans-serif");
            set => _dataSetting.SetValue(nameof(OsdFont), value);
        }

        public string OsdOnSeek
        {
            get => _dataSetting.GetValue(nameof(OsdOnSeek), "msg");
            set => _dataSetting.SetValue(nameof(OsdOnSeek), value);
        }

        public int OsdDuration
        {
            get => _dataSetting.GetValue(nameof(OsdDuration), 2000);
            set => _dataSetting.SetValue(nameof(OsdDuration), value);
        }

        public string OsdPlayingMsg
        {
            get => _dataSetting.GetValue(nameof(OsdPlayingMsg), "${filename}");
            set => _dataSetting.SetValue(nameof(OsdPlayingMsg), value);
        }

        public int OsdPlayingMsgDuration
        {
            get => _dataSetting.GetValue(nameof(OsdPlayingMsgDuration), 3000);
            set => _dataSetting.SetValue(nameof(OsdPlayingMsgDuration), value);
        }

        public int OsdBarWidth
        {
            get => _dataSetting.GetValue(nameof(OsdBarWidth), 100);
            set => _dataSetting.SetValue(nameof(OsdBarWidth), value);
        }

        public double OsdBarHeight
        {
            get => _dataSetting.GetValue(nameof(OsdBarHeight), 1.8);
            set => _dataSetting.SetValue(nameof(OsdBarHeight), value);
        }

        public double OsdBlur
        {
            get => _dataSetting.GetValue(nameof(OsdBlur), 0.0);
            set => _dataSetting.SetValue(nameof(OsdBlur), value);
        }

        public double OsdOutlineSize
        {
            get => _dataSetting.GetValue(nameof(OsdOutlineSize), 0.8);
            set => _dataSetting.SetValue(nameof(OsdOutlineSize), value);
        }

        public bool OsdFractions
        {
            get => _dataSetting.GetValue(nameof(OsdFractions), true);
            set => _dataSetting.SetValue(nameof(OsdFractions), value);
        }

        public string OsdColor
        {
            get => _dataSetting.GetValue(nameof(OsdColor), string.Empty);
            set => _dataSetting.SetValue(nameof(OsdColor), value);
        }

        public string OsdOutlineColor
        {
            get => _dataSetting.GetValue(nameof(OsdOutlineColor), string.Empty);
            set => _dataSetting.SetValue(nameof(OsdOutlineColor), value);
        }

        public int OsdLevel
        {
            get => _dataSetting.GetValue(nameof(OsdLevel), 1);
            set => _dataSetting.SetValue(nameof(OsdLevel), value);
        }

        public string OsdAlignX
        {
            get => _dataSetting.GetValue(nameof(OsdAlignX), "left");
            set => _dataSetting.SetValue(nameof(OsdAlignX), value);
        }

        public string OsdAlignY
        {
            get => _dataSetting.GetValue(nameof(OsdAlignY), "top");
            set => _dataSetting.SetValue(nameof(OsdAlignY), value);
        }

        public int OsdMarginX
        {
            get => _dataSetting.GetValue(nameof(OsdMarginX), 16);
            set => _dataSetting.SetValue(nameof(OsdMarginX), value);
        }

        public int OsdMarginY
        {
            get => _dataSetting.GetValue(nameof(OsdMarginY), 16);
            set => _dataSetting.SetValue(nameof(OsdMarginY), value);
        }
        public double OsdScale
        {
            get => _dataSetting.GetValue(nameof(OsdScale), 1.0);
            set => _dataSetting.SetValue(nameof(OsdScale), value);
        }

        public double OsdSpacing
        {
            get => _dataSetting.GetValue(nameof(OsdSpacing), 0.0);
            set => _dataSetting.SetValue(nameof(OsdSpacing), value);
        }

        public bool OsdBold
        {
            get => _dataSetting.GetValue(nameof(OsdBold), false);
            set => _dataSetting.SetValue(nameof(OsdBold), value);
        }

        public bool OsdItalic
        {
            get => _dataSetting.GetValue(nameof(OsdItalic), false);
            set => _dataSetting.SetValue(nameof(OsdItalic), value);
        }

        public string OsdJustify
        {
            get => _dataSetting.GetValue(nameof(OsdJustify), "auto");
            set => _dataSetting.SetValue(nameof(OsdJustify), value);
        }

        public double OsdShadowOffset
        {
            get => _dataSetting.GetValue(nameof(OsdShadowOffset), 0.0);
            set => _dataSetting.SetValue(nameof(OsdShadowOffset), value);
        }

        public string OsdFontsDir
        {
            get => _dataSetting.GetValue(nameof(OsdFontsDir), "");
            set => _dataSetting.SetValue(nameof(OsdFontsDir), value);
        }

        public string OsdFontProvider
        {
            get => _dataSetting.GetValue(nameof(OsdFontProvider), "auto");
            set => _dataSetting.SetValue(nameof(OsdFontProvider), value);
        }

        public string OsdShaper
        {
            get => _dataSetting.GetValue(nameof(OsdShaper), "complex");
            set => _dataSetting.SetValue(nameof(OsdShaper), value);
        }

        public string OsdBackColor
        {
            get => _dataSetting.GetValue(nameof(OsdBackColor), "#AF000000");
            set => _dataSetting.SetValue(nameof(OsdBackColor), value);
        }

        public string OsdSelectedColor
        {
            get => _dataSetting.GetValue(nameof(OsdSelectedColor), "#FFFABD2F");
            set => _dataSetting.SetValue(nameof(OsdSelectedColor), value);
        }

        public string OsdSelectedOutlineColor
        {
            get => _dataSetting.GetValue(nameof(OsdSelectedOutlineColor), "#FF000000");
            set => _dataSetting.SetValue(nameof(OsdSelectedOutlineColor), value);
        }

        public bool OsdScaleByWindow
        {
            get => _dataSetting.GetValue(nameof(OsdScaleByWindow), true);
            set => _dataSetting.SetValue(nameof(OsdScaleByWindow), value);
        }

        public int OsdMarginYOffset
        {
            get => _dataSetting.GetValue(nameof(OsdMarginYOffset), 0);
            set => _dataSetting.SetValue(nameof(OsdMarginYOffset), value);
        }

    }
}
