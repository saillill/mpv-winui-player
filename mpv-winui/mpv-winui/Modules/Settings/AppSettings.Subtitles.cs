namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Subtitle rendering and loading (including image subtitles).
    /// </summary>
    public partial class AppSettings
    {
        public int SubFontSize
        {
            get => _dataSetting.GetValue(nameof(SubFontSize), 42);
            set => _dataSetting.SetValue(nameof(SubFontSize), value);
        }

        public bool SubScaleByWindow
        {
            get => _dataSetting.GetValue(nameof(SubScaleByWindow), true);
            set => _dataSetting.SetValue(nameof(SubScaleByWindow), value);
        }

        public double SubLineSpacing
        {
            get => _dataSetting.GetValue(nameof(SubLineSpacing), 0.0);
            set => _dataSetting.SetValue(nameof(SubLineSpacing), value);
        }

        public string SubJustify
        {
            get => _dataSetting.GetValue(nameof(SubJustify), "auto");
            set => _dataSetting.SetValue(nameof(SubJustify), value);
        }

        public bool SubClearOnSeek
        {
            get => _dataSetting.GetValue(nameof(SubClearOnSeek), true);
            set => _dataSetting.SetValue(nameof(SubClearOnSeek), value);
        }

        public string SubHinting
        {
            get => _dataSetting.GetValue(nameof(SubHinting), "none");
            set => _dataSetting.SetValue(nameof(SubHinting), value);
        }

        public double SubDelay
        {
            get => _dataSetting.GetValue(nameof(SubDelay), 0.0);
            set => _dataSetting.SetValue(nameof(SubDelay), value);
        }

        public int SubPos
        {
            get => _dataSetting.GetValue(nameof(SubPos), 100);
            set => _dataSetting.SetValue(nameof(SubPos), value);
        }

        public string SubtitleLanguage
        {
            get => _dataSetting.GetValue(nameof(SubtitleLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(SubtitleLanguage), value);
        }

        public string SubAssOverride
        {
            get => _dataSetting.GetValue(nameof(SubAssOverride), "scale");
            set => _dataSetting.SetValue(nameof(SubAssOverride), value);
        }

        public double SubBlur
        {
            get => _dataSetting.GetValue(nameof(SubBlur), 0.0);
            set => _dataSetting.SetValue(nameof(SubBlur), value);
        }

        public string SubAuto
        {
            get => _dataSetting.GetValue(nameof(SubAuto), "fuzzy");
            set => _dataSetting.SetValue(nameof(SubAuto), value);
        }

        public string SubFont
        {
            get
            {
                var stored = _dataSetting.GetValue(nameof(SubFont), string.Empty);
                return string.IsNullOrEmpty(stored) ? LanguageDefaultSubtitleFont() : stored;
            }
            set => _dataSetting.SetValue(nameof(SubFont), value);
        }

        public string SubFontProvider
        {
            get => _dataSetting.GetValue(nameof(SubFontProvider), "auto");
            set => _dataSetting.SetValue(nameof(SubFontProvider), value);
        }

        public string SubCodePage
        {
            get => _dataSetting.GetValue(nameof(SubCodePage), "auto");
            set => _dataSetting.SetValue(nameof(SubCodePage), value);
        }

        public string SubFontFile
        {
            get => _dataSetting.GetValue(nameof(SubFontFile), string.Empty);
            set => _dataSetting.SetValue(nameof(SubFontFile), value);
        }

        public bool SubAssScaleWithWindow
        {
            get => _dataSetting.GetValue(nameof(SubAssScaleWithWindow), false);
            set => _dataSetting.SetValue(nameof(SubAssScaleWithWindow), value);
        }

        public string SubFallback
        {
            get => _dataSetting.GetValue(nameof(SubFallback), "default");
            set => _dataSetting.SetValue(nameof(SubFallback), value);
        }

        public double SubOutlineSize
        {
            get => _dataSetting.GetValue(nameof(SubOutlineSize), 1.5);
            set => _dataSetting.SetValue(nameof(SubOutlineSize), value);
        }

        public double SubShadowOffset
        {
            get => _dataSetting.GetValue(nameof(SubShadowOffset), 2.0);
            set => _dataSetting.SetValue(nameof(SubShadowOffset), value);
        }

        public bool SubEmbeddedFonts
        {
            get => _dataSetting.GetValue(nameof(SubEmbeddedFonts), true);
            set => _dataSetting.SetValue(nameof(SubEmbeddedFonts), value);
        }

        public bool SubUseMargins
        {
            get => _dataSetting.GetValue(nameof(SubUseMargins), true);
            set => _dataSetting.SetValue(nameof(SubUseMargins), value);
        }

        public bool SubAssForceMargins
        {
            get => _dataSetting.GetValue(nameof(SubAssForceMargins), true);
            set => _dataSetting.SetValue(nameof(SubAssForceMargins), value);
        }

        public bool StretchImageSubsToScreen
        {
            get => _dataSetting.GetValue(nameof(StretchImageSubsToScreen), true);
            set => _dataSetting.SetValue(nameof(StretchImageSubsToScreen), value);
        }

        public string SubFilePaths
        {
            get => _dataSetting.GetValue(nameof(SubFilePaths), "sub;Subs;subtitles");
            set => _dataSetting.SetValue(nameof(SubFilePaths), value);
        }

        public int SubHdrPeak
        {
            get => _dataSetting.GetValue(nameof(SubHdrPeak), 100);
            set => _dataSetting.SetValue(nameof(SubHdrPeak), value);
        }

        public int ImageSubsHdrPeak
        {
            get => _dataSetting.GetValue(nameof(ImageSubsHdrPeak), 10000);
            set => _dataSetting.SetValue(nameof(ImageSubsHdrPeak), value);
        }

        public string SubAssStyleOverrides
        {
            get => _dataSetting.GetValue(nameof(SubAssStyleOverrides), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssStyleOverrides), value);
        }

        public string SubColor
        {
            get => _dataSetting.GetValue(nameof(SubColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubColor), value);
        }

        public bool ImageSubsVideoResolution
        {
            get => _dataSetting.GetValue(nameof(ImageSubsVideoResolution), false);
            set => _dataSetting.SetValue(nameof(ImageSubsVideoResolution), value);
        }

        public bool SubScaleSigns
        {
            get => _dataSetting.GetValue(nameof(SubScaleSigns), true);
            set => _dataSetting.SetValue(nameof(SubScaleSigns), value);
        }

        public string SubBackColor
        {
            get => _dataSetting.GetValue(nameof(SubBackColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubBackColor), value);
        }

        public string SubBorderColor
        {
            get => _dataSetting.GetValue(nameof(SubBorderColor), string.Empty);
            set => _dataSetting.SetValue(nameof(SubBorderColor), value);
        }

        public string SubAssUseVideoData
        {
            get => _dataSetting.GetValue(nameof(SubAssUseVideoData), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssUseVideoData), value);
        }

        public string SubAssVideoAspectOverride
        {
            get => _dataSetting.GetValue(nameof(SubAssVideoAspectOverride), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssVideoAspectOverride), value);
        }

        public string SubAssVsfilterColorCompat
        {
            get => _dataSetting.GetValue(nameof(SubAssVsfilterColorCompat), string.Empty);
            set => _dataSetting.SetValue(nameof(SubAssVsfilterColorCompat), value);
        }

        public bool SubBold
        {
            get => _dataSetting.GetValue(nameof(SubBold), false);
            set => _dataSetting.SetValue(nameof(SubBold), value);
        }

        public bool SubItalic
        {
            get => _dataSetting.GetValue(nameof(SubItalic), false);
            set => _dataSetting.SetValue(nameof(SubItalic), value);
        }

        public string SubAlignX
        {
            get => _dataSetting.GetValue(nameof(SubAlignX), "center");
            set => _dataSetting.SetValue(nameof(SubAlignX), value);
        }

        public string SubAlignY
        {
            get => _dataSetting.GetValue(nameof(SubAlignY), "bottom");
            set => _dataSetting.SetValue(nameof(SubAlignY), value);
        }

        public int SubMarginX
        {
            get => _dataSetting.GetValue(nameof(SubMarginX), 19);
            set => _dataSetting.SetValue(nameof(SubMarginX), value);
        }

        public int SubMarginY
        {
            get => _dataSetting.GetValue(nameof(SubMarginY), 34);
            set => _dataSetting.SetValue(nameof(SubMarginY), value);
        }
    }
}
