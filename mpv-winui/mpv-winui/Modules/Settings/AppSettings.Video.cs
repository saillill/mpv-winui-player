namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Video rendering: output chain, HDR/tone-mapping, scaling filters, shaders.
    /// </summary>
    public partial class AppSettings
    {
        public string Hwdec
        {
            get => _dataSetting.GetValue(nameof(Hwdec), "auto-safe");
            set => _dataSetting.SetValue(nameof(Hwdec), value);
        }

        public string Deinterlace
        {
            get => _dataSetting.GetValue(nameof(Deinterlace), "no");
            set => _dataSetting.SetValue(nameof(Deinterlace), value);
        }

        public string AspectRatio
        {
            get
            {
                // mpv's "auto" spelling is invalid for video-aspect-override; the
                // legal value for "automatic" is "no". Migrate legacy stores.
                var value = _dataSetting.GetValue(nameof(AspectRatio), "no");
                return value == "auto" ? "no" : value;
            }
            set => _dataSetting.SetValue(nameof(AspectRatio), value);
        }

        public string VideoSync
        {
            get
            {
                // "cfr" was removed from mpv; migrate the legacy preset to the
                // equivalent modern mode.
                var value = _dataSetting.GetValue(nameof(VideoSync), "audio");
                return value == "cfr" ? "display-resample" : value;
            }
            set => _dataSetting.SetValue(nameof(VideoSync), value);
        }

        public bool Interpolation
        {
            get => _dataSetting.GetValue(nameof(Interpolation), false);
            set => _dataSetting.SetValue(nameof(Interpolation), value);
        }

        public bool CorrectDownscaling
        {
            get => _dataSetting.GetValue(nameof(CorrectDownscaling), true);
            set => _dataSetting.SetValue(nameof(CorrectDownscaling), value);
        }

        public string Scale
        {
            get => _dataSetting.GetValue(nameof(Scale), "lanczos");
            set => _dataSetting.SetValue(nameof(Scale), value);
        }

        public string VideoRotate
        {
            get => _dataSetting.GetValue(nameof(VideoRotate), "no");
            set => _dataSetting.SetValue(nameof(VideoRotate), value);
        }

        public bool Deband
        {
            get => _dataSetting.GetValue(nameof(Deband), false);
            set => _dataSetting.SetValue(nameof(Deband), value);
        }

        public bool LinearDownscaling
        {
            get => _dataSetting.GetValue(nameof(LinearDownscaling), true);
            set => _dataSetting.SetValue(nameof(LinearDownscaling), value);
        }

        public bool SigmoidUpscaling
        {
            get => _dataSetting.GetValue(nameof(SigmoidUpscaling), true);
            set => _dataSetting.SetValue(nameof(SigmoidUpscaling), value);
        }

        public string ToneMapping
        {
            get => _dataSetting.GetValue(nameof(ToneMapping), "bt.2390");
            set => _dataSetting.SetValue(nameof(ToneMapping), value);
        }

        public string TargetGamut
        {
            get => _dataSetting.GetValue(nameof(TargetGamut), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetGamut), value);
        }

        public double ToneMappingMaxBoost
        {
            get => _dataSetting.GetValue(nameof(ToneMappingMaxBoost), 1.0);
            set => _dataSetting.SetValue(nameof(ToneMappingMaxBoost), value);
        }

        public string HdrComputePeak
        {
            get => _dataSetting.GetValue(nameof(HdrComputePeak), "auto");
            set => _dataSetting.SetValue(nameof(HdrComputePeak), value);
        }

        public double HdrPeakDecayRate
        {
            get => _dataSetting.GetValue(nameof(HdrPeakDecayRate), 20.0);
            set => _dataSetting.SetValue(nameof(HdrPeakDecayRate), value);
        }

        public double HdrSceneThresholdLow
        {
            get => _dataSetting.GetValue(nameof(HdrSceneThresholdLow), 1.0);
            set => _dataSetting.SetValue(nameof(HdrSceneThresholdLow), value);
        }

        public double HdrSceneThresholdHigh
        {
            get => _dataSetting.GetValue(nameof(HdrSceneThresholdHigh), 3.0);
            set => _dataSetting.SetValue(nameof(HdrSceneThresholdHigh), value);
        }

        public double HdrContrastRecovery
        {
            get => _dataSetting.GetValue(nameof(HdrContrastRecovery), 0.0);
            set => _dataSetting.SetValue(nameof(HdrContrastRecovery), value);
        }

        public double HdrContrastSmoothness
        {
            get => _dataSetting.GetValue(nameof(HdrContrastSmoothness), 3.5);
            set => _dataSetting.SetValue(nameof(HdrContrastSmoothness), value);
        }

        public int D3d11SyncInterval
        {
            get => _dataSetting.GetValue(nameof(D3d11SyncInterval), 1);
            set => _dataSetting.SetValue(nameof(D3d11SyncInterval), value);
        }

        public bool ToneMappingVisualize
        {
            get => _dataSetting.GetValue(nameof(ToneMappingVisualize), false);
            set => _dataSetting.SetValue(nameof(ToneMappingVisualize), value);
        }

        public string D3d11Warp
        {
            get => _dataSetting.GetValue(nameof(D3d11Warp), "auto");
            set => _dataSetting.SetValue(nameof(D3d11Warp), value);
        }

        public int VideoReversalBuffer
        {
            get => _dataSetting.GetValue(nameof(VideoReversalBuffer), 0);
            set => _dataSetting.SetValue(nameof(VideoReversalBuffer), value);
        }

        public string DitherDepth
        {
            get => _dataSetting.GetValue(nameof(DitherDepth), "no");
            set => _dataSetting.SetValue(nameof(DitherDepth), value);
        }

        public string VideoOutputLevels
        {
            get => _dataSetting.GetValue(nameof(VideoOutputLevels), "auto");
            set => _dataSetting.SetValue(nameof(VideoOutputLevels), value);
        }

        public string VideoDecodeDirect
        {
            get => _dataSetting.GetValue(nameof(VideoDecodeDirect), "auto");
            set => _dataSetting.SetValue(nameof(VideoDecodeDirect), value);
        }

        public bool IccProfileAuto
        {
            get => _dataSetting.GetValue(nameof(IccProfileAuto), false);
            set => _dataSetting.SetValue(nameof(IccProfileAuto), value);
        }

        public string D3d11OutputFormat
        {
            get => _dataSetting.GetValue(nameof(D3d11OutputFormat), string.Empty);
            set => _dataSetting.SetValue(nameof(D3d11OutputFormat), value);
        }

        public string BlendSubtitles
        {
            get => _dataSetting.GetValue(nameof(BlendSubtitles), "no");
            set => _dataSetting.SetValue(nameof(BlendSubtitles), value);
        }

        public string Cscale
        {
            get => _dataSetting.GetValue(nameof(Cscale), "lanczos");
            set => _dataSetting.SetValue(nameof(Cscale), value);
        }

        public string Tscale
        {
            get
            {
                // mpv has no "cubic" tscale; the preset was a typo for "bicubic".
                var value = _dataSetting.GetValue(nameof(Tscale), "oversample");
                return value == "cubic" ? "bicubic" : value;
            }
            set => _dataSetting.SetValue(nameof(Tscale), value);
        }

        public bool LinearUpscaling
        {
            get => _dataSetting.GetValue(nameof(LinearUpscaling), false);
            set => _dataSetting.SetValue(nameof(LinearUpscaling), value);
        }

        public string Dither
        {
            get => _dataSetting.GetValue(nameof(Dither), "fruit");
            set => _dataSetting.SetValue(nameof(Dither), value);
        }

        public double Panscan
        {
            get => _dataSetting.GetValue(nameof(Panscan), 0.0);
            set => _dataSetting.SetValue(nameof(Panscan), value);
        }

        public string TargetColorspaceHint
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHint), "yes");
            set => _dataSetting.SetValue(nameof(TargetColorspaceHint), value);
        }

        public string TargetPrim
        {
            get => _dataSetting.GetValue(nameof(TargetPrim), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetPrim), value);
        }

        public string TargetTrc
        {
            get => _dataSetting.GetValue(nameof(TargetTrc), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetTrc), value);
        }

        public int TargetPeak
        {
            get => _dataSetting.GetValue(nameof(TargetPeak), 0);
            set => _dataSetting.SetValue(nameof(TargetPeak), value);
        }

        public bool HdrAutoLog
        {
            get => _dataSetting.GetValue(nameof(HdrAutoLog), false);
            set => _dataSetting.SetValue(nameof(HdrAutoLog), value);
        }

        public string D3d11OutputCsp
        {
            get
            {
                // "bt.709" is not a legal d3d11-output-csp value; fall back to
                // auto (empty) for values written by older builds.
                var value = _dataSetting.GetValue(nameof(D3d11OutputCsp), string.Empty);
                return value == "bt.709" ? string.Empty : value;
            }
            set => _dataSetting.SetValue(nameof(D3d11OutputCsp), value);
        }

        public bool D3d11ExclusiveFs
        {
            get => _dataSetting.GetValue(nameof(D3d11ExclusiveFs), false);
            set => _dataSetting.SetValue(nameof(D3d11ExclusiveFs), value);
        }

        public bool D3d11Flip
        {
            get => _dataSetting.GetValue(nameof(D3d11Flip), true);
            set => _dataSetting.SetValue(nameof(D3d11Flip), value);
        }

        public string HwdecCodecs
        {
            get => _dataSetting.GetValue(nameof(HwdecCodecs), "all");
            set => _dataSetting.SetValue(nameof(HwdecCodecs), value);
        }

        public string TargetColorspaceHintMode
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintMode), string.Empty);
            set => _dataSetting.SetValue(nameof(TargetColorspaceHintMode), value);
        }

        public bool TargetColorspaceHintStrict
        {
            get => _dataSetting.GetValue(nameof(TargetColorspaceHintStrict), true);
            set => _dataSetting.SetValue(nameof(TargetColorspaceHintStrict), value);
        }

        public string GamutMappingMode
        {
            get => _dataSetting.GetValue(nameof(GamutMappingMode), string.Empty);
            set => _dataSetting.SetValue(nameof(GamutMappingMode), value);
        }

        public int VideoSyncMaxVideoChange
        {
            get => _dataSetting.GetValue(nameof(VideoSyncMaxVideoChange), 5);
            set => _dataSetting.SetValue(nameof(VideoSyncMaxVideoChange), value);
        }

        public string BackgroundTileColor0
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileColor0), "#B4B4B4");
            set => _dataSetting.SetValue(nameof(BackgroundTileColor0), value);
        }

        public string BackgroundTileColor1
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileColor1), "#DCDCDC");
            set => _dataSetting.SetValue(nameof(BackgroundTileColor1), value);
        }

        public int BackgroundTileSize
        {
            get => _dataSetting.GetValue(nameof(BackgroundTileSize), 128);
            set => _dataSetting.SetValue(nameof(BackgroundTileSize), value);
        }

        public string IccProfile
        {
            get => _dataSetting.GetValue(nameof(IccProfile), string.Empty);
            set => _dataSetting.SetValue(nameof(IccProfile), value);
        }

        public string VideoUnscaled
        {
            get => _dataSetting.GetValue(nameof(VideoUnscaled), string.Empty);
            set => _dataSetting.SetValue(nameof(VideoUnscaled), value);
        }

        /// <summary>Seek-bar preview thumbnail width in logical px (120..480).</summary>
        /// <summary>Picture brightness adjustment (-100..100).</summary>
        public int PictureBrightness { get => _dataSetting.GetValue(nameof(PictureBrightness), 0); set => _dataSetting.SetValue(nameof(PictureBrightness), value); }

        /// <summary>Picture contrast adjustment (-100..100).</summary>
        public int PictureContrast { get => _dataSetting.GetValue(nameof(PictureContrast), 0); set => _dataSetting.SetValue(nameof(PictureContrast), value); }

        /// <summary>Picture saturation adjustment (-100..100).</summary>
        public int PictureSaturation { get => _dataSetting.GetValue(nameof(PictureSaturation), 0); set => _dataSetting.SetValue(nameof(PictureSaturation), value); }

        /// <summary>Picture gamma adjustment (-100..100).</summary>
        public int PictureGamma { get => _dataSetting.GetValue(nameof(PictureGamma), 0); set => _dataSetting.SetValue(nameof(PictureGamma), value); }

        /// <summary>Picture hue adjustment (-100..100).</summary>
        public int PictureHue { get => _dataSetting.GetValue(nameof(PictureHue), 0); set => _dataSetting.SetValue(nameof(PictureHue), value); }

        /// <summary>Sharpening strength (0..5, gpu-next only).</summary>
        public double PictureSharpen { get => _dataSetting.GetValue(nameof(PictureSharpen), 0.0); set => _dataSetting.SetValue(nameof(PictureSharpen), value); }

        public string D3d11Adapter
        {
            get => _dataSetting.GetValue(nameof(D3d11Adapter), string.Empty);
            set => _dataSetting.SetValue(nameof(D3d11Adapter), value);
        }

        public string GlslShadersAppend
        {
            get => _dataSetting.GetValue(nameof(GlslShadersAppend), string.Empty);
            set => _dataSetting.SetValue(nameof(GlslShadersAppend), value);
        }

        public string GlslShaders
        {
            get => _dataSetting.GetValue(nameof(GlslShaders), string.Empty);
            set => _dataSetting.SetValue(nameof(GlslShaders), value);
        }

        public string GlslShaderOpts
        {
            get => _dataSetting.GetValue(nameof(GlslShaderOpts), string.Empty);
            set => _dataSetting.SetValue(nameof(GlslShaderOpts), value);
        }

        public double ImageDisplayDuration
        {
            get => _dataSetting.GetValue(nameof(ImageDisplayDuration), 5.0);
            set => _dataSetting.SetValue(nameof(ImageDisplayDuration), value);
        }
    }
}
