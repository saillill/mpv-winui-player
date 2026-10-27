using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// 设置项 → mpv 命令 的映射：设置页改动时即时下发，播放器初始化后批量应用。
/// </summary>
public static partial class MpvSettings
{
    /// <summary>
    /// Quote a value for mpv's command string parser. mpv_command_string uses
    /// C-style escapes inside quotes, so Windows paths must have their
    /// backslashes doubled, otherwise "C:\Users\..." fails to parse and the
    /// option silently keeps its previous value.
    ///
    /// <para>The implementation moved to <see cref="MpvConfValueCodec"/> so its
    /// inverse is reachable from the test project. Keeping the pair private here
    /// is what allowed the write side to escape without the read side
    /// unescaping, which cost a real mpv.conf 896 MB — see that type's own
    /// documentation.</para>
    /// </summary>
    private static string Q(string value) => MpvConfValueCodec.Quote(value);

    public static string? ToCommand(string key, object value)
    {
        var cmd = key switch
        {
            nameof(AppSettings.Hwdec) => $"set hwdec {value}",
            nameof(AppSettings.HwdecCodecs) => $"set hwdec-codecs {(string)value}",
            nameof(AppSettings.AlwaysOnTop) => $"set ontop {(value is true ? "yes" : "no")}",
            nameof(AppSettings.InputIme) => $"set input-ime {(value is true ? "yes" : "no")}",
            nameof(AppSettings.StartFullscreen) => $"set fullscreen {(value is true ? "yes" : "no")}",
            nameof(AppSettings.D3d11OutputCsp) => string.IsNullOrWhiteSpace((string)value) ? null : $"set d3d11-output-csp {(string)value}",
            nameof(AppSettings.D3d11OutputFormat) => string.IsNullOrWhiteSpace((string)value) ? null : $"set d3d11-output-format {(string)value}",
            nameof(AppSettings.D3d11SyncInterval) => $"set d3d11-sync-interval {(int)value}",
            nameof(AppSettings.D3d11Warp) => $"set d3d11-warp {(string)value}",
            nameof(AppSettings.D3d11ExclusiveFs) => $"set d3d11-exclusive-fs {(value is true ? "yes" : "no")}",
            nameof(AppSettings.D3d11Flip) => $"set d3d11-flip {(value is true ? "yes" : "no")}",
            nameof(AppSettings.D3d11Adapter) => string.IsNullOrWhiteSpace((string)value) ? null : $"set d3d11-adapter {Q((string)value)}",
            // volume-max: observed to fail, cause not established.
            //
            // What was measured: this app sends `no-osd set volume-max 100` at
            // startup and mpv answers "Command 'no-osd set volume-max 100' not
            // found" -- the same line, once per launch, in three consecutive
            // launches. That is the whole of the evidence.
            //
            // What was NOT established: why. The plausible story is that
            // volume-max is derived from mixer state and ao=wasapi hands the
            // mixer to the system, but I could not confirm it here. The test
            // harness (mpv-winrt-test) could not be made to observe a rejection
            // at all -- even a deliberately bogus option name (`set
            // no-such-option 1`) came back silent -- so it cannot settle this,
            // and the only other mpv host is mpvw.exe, which the project
            // forbids driving.
            //
            // Why mapping to null is still the right move: nothing reads mpv's
            // volume-max back. The volume slider's ceiling comes from
            // AppContext.AppSetting.VolumeMax directly (PlayerControl), and the
            // engine gets volume-max from mpv.conf via BuildConfigLines. So the
            // runtime set had no observable benefit, and it produced an error
            // line per launch. Mapping to null removes a known-failing command
            // without removing the setting or its persistence.
            //
            // If someone later establishes the cause and it turns out a
            // different command form works, this is the line to revisit.
            nameof(AppSettings.VolumeMax) => null,
            nameof(AppSettings.AudioGapless) => $"set gapless-audio {(string)value}",
            nameof(AppSettings.KeepOpen) => $"set keep-open {value}",
            nameof(AppSettings.LoopPlaylist) => $"set loop-playlist {(string)value}",
            nameof(AppSettings.LoopFile) => $"set loop-file {(value is true ? "inf" : "no")}",
            nameof(AppSettings.CacheEnabled) => $"set cache {(string)value}",
            nameof(AppSettings.DemuxerReadahead) => $"set demuxer-readahead-secs {(double)value}",
            nameof(AppSettings.UserAgent) => string.IsNullOrWhiteSpace((string)value) ? null : $"set user-agent {Q((string)value)}",
            nameof(AppSettings.Referrer) => string.IsNullOrWhiteSpace((string)value) ? null : $"set referrer {Q((string)value)}",
            nameof(AppSettings.HttpHeaderFields) => string.IsNullOrWhiteSpace((string)value) ? null : $"set http-header-fields {Q((string)value)}",
            nameof(AppSettings.HttpProxy) => string.IsNullOrWhiteSpace((string)value) ? null : $"set http-proxy {Q((string)value)}",
            nameof(AppSettings.CookiesFile) => string.IsNullOrWhiteSpace((string)value) ? null : $"set cookies-file {Q((string)value)}",
            nameof(AppSettings.TlsVerify) => $"set tls-verify {(value is true ? "yes" : "no")}",
            nameof(AppSettings.NetworkTimeout) => $"set network-timeout {value}",
            nameof(AppSettings.CurlMaxRedirects) => $"set curl-max-redirects {value}",
            nameof(AppSettings.CurlMaxRetries) => $"set curl-max-retries {value}",
            nameof(AppSettings.CurlConnectTimeout) => $"set curl-connect-timeout {value}",
            nameof(AppSettings.CurlBufferSize) => $"set curl-buffer-size {value}",
            nameof(AppSettings.CurlMaxRequestSize) => $"set curl-max-request-size {value}",
            // The patched native autocreate-playlist scans the file's whole
            // directory synchronously inside loadfile (minutes on huge
            // dirs). Always disable it here; mpv_winrt.MpvPlayer fills the playlist
            // lazily in the background instead, driven by the same setting.
            nameof(AppSettings.PictureBrightness) => $"set brightness {(int)AppContext.AppSetting.PictureBrightness}",
            nameof(AppSettings.PictureContrast) => $"set contrast {(int)AppContext.AppSetting.PictureContrast}",
            nameof(AppSettings.PictureSaturation) => $"set saturation {(int)AppContext.AppSetting.PictureSaturation}",
            nameof(AppSettings.PictureGamma) => $"set gamma {(int)AppContext.AppSetting.PictureGamma}",
            nameof(AppSettings.PictureHue) => $"set hue {(int)AppContext.AppSetting.PictureHue}",
            nameof(AppSettings.PictureSharpen) => $"set sharpen {AppContext.AppSetting.PictureSharpen:0.0##}",
            nameof(AppSettings.AutoWindowResize) => (bool)value ? "set auto-window-resize yes" : "set auto-window-resize no",
            nameof(AppSettings.AdLavcDownmix) => (bool)value ? "set ad-lavc-downmix yes" : "set ad-lavc-downmix no",
            nameof(AppSettings.AutoCreatePlaylist) => "set autocreate-playlist no",
            nameof(AppSettings.DirectoryMode) => $"set directory-mode {(string)value}",
            nameof(AppSettings.DirectoryFilterTypes) => $"set directory-filter-types {(string)value}",
            nameof(AppSettings.VideoExts) => $"set video-exts {(string)value}",
            nameof(AppSettings.ImageExts) => $"set image-exts {(string)value}",
            nameof(AppSettings.AudioExts) => $"set audio-exts {(string)value}",
            nameof(AppSettings.InputIpcServer) => string.IsNullOrWhiteSpace((string)value) ? null : $"set input-ipc-server {Q((string)value)}",
            nameof(AppSettings.WatchLaterDir) => string.IsNullOrWhiteSpace((string)value) ? null : $"set watch-later-dir {Q((string)value)}",
            nameof(AppSettings.WatchLaterOptions) => string.IsNullOrWhiteSpace((string)value) ? null : $"set watch-later-options {(string)value}",
            nameof(AppSettings.IccCacheDir) => string.IsNullOrWhiteSpace((string)value) ? null : $"set icc-cache-dir {Q((string)value)}",
            nameof(AppSettings.GpuShaderCacheDir) => string.IsNullOrWhiteSpace((string)value) ? null : $"set gpu-shader-cache-dir {Q((string)value)}",
            nameof(AppSettings.Deinterlace) => $"set deinterlace {value}",
            nameof(AppSettings.AspectRatio) => $"set video-aspect-override {value}",
            nameof(AppSettings.Cscale) => $"set cscale {(string)value}",
            nameof(AppSettings.Tscale) => $"set tscale {(string)value}",
            nameof(AppSettings.LinearUpscaling) => $"set linear-upscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.Dither) => $"set dither {(string)value}",
            nameof(AppSettings.Panscan) => $"set panscan {(double)value}",
            nameof(AppSettings.VideoUnscaled) => string.IsNullOrWhiteSpace((string)value) ? null : $"set video-unscaled {(string)value}",
            nameof(AppSettings.BackgroundTileColor0) => string.IsNullOrWhiteSpace((string)value) ? null : $"set background-tile-color-0 {Q((string)value)}",
            nameof(AppSettings.BackgroundTileColor1) => string.IsNullOrWhiteSpace((string)value) ? null : $"set background-tile-color-1 {Q((string)value)}",
            nameof(AppSettings.BackgroundTileSize) => $"set background-tile-size {(int)value}",
            nameof(AppSettings.SubFontSize) => $"set sub-font-size {value}",
            nameof(AppSettings.SubScaleByWindow) => $"set sub-scale-by-window {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubLineSpacing) => $"set sub-line-spacing {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.SubJustify) => $"set sub-justify {(string)value}",
            nameof(AppSettings.SubClearOnSeek) => $"set sub-clear-on-seek {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubHinting) => $"set sub-hinting {(string)value}",
            nameof(AppSettings.SubDelay) => $"set sub-delay {value}",
            nameof(AppSettings.SubPos) => $"set sub-pos {value}",
            nameof(AppSettings.AudioLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set alang {(string)value}",
            nameof(AppSettings.SubtitleLanguage) => string.IsNullOrWhiteSpace((string)value) ? null : $"set slang {(string)value}",
            nameof(AppSettings.SubFilePaths) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-file-paths {Q((string)value)}",
            nameof(AppSettings.SubHdrPeak) => $"set sub-hdr-peak {(int)value}",
            nameof(AppSettings.ImageSubsHdrPeak) => $"set image-subs-hdr-peak {(int)value}",
            nameof(AppSettings.ImageSubsVideoResolution) => $"set image-subs-video-resolution {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubAssStyleOverrides) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-ass-style-overrides {Q((string)value)}",
            nameof(AppSettings.SubColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-color {Q((string)value)}",
            nameof(AppSettings.SubBackColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-back-color {Q((string)value)}",
            nameof(AppSettings.SubBorderColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-border-color {Q((string)value)}",
            nameof(AppSettings.SubScaleSigns) => $"set sub-scale-signs {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubAssUseVideoData) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-ass-use-video-data {(string)value}",
            nameof(AppSettings.SubAssVideoAspectOverride) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-ass-video-aspect-override {(string)value}",
            nameof(AppSettings.SubAssVsfilterColorCompat) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-ass-vsfilter-color-compat {(string)value}",
            nameof(AppSettings.AudioDevice) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-device {Q((string)value)}",
            nameof(AppSettings.ScreenshotDirectory) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-directory {Q((string)value)}",
            nameof(AppSettings.ScreenshotTemplate) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-template {(string)value}",
            nameof(AppSettings.SavePositionOnQuit) => $"set save-position-on-quit {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ResumePlayback) => $"set resume-playback {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotFormat) => $"set screenshot-format {(string)value}",
            nameof(AppSettings.ScreenshotJpegQuality) => $"set screenshot-jpeg-quality {value}",
            nameof(AppSettings.ScreenshotJpegSourceChroma) => $"set screenshot-jpeg-source-chroma {(value is true ? "yes" : "no")}",
            nameof(AppSettings.VideoSync) => $"set video-sync {(string)value}",
            nameof(AppSettings.VideoSyncMaxVideoChange) => $"set video-sync-max-video-change {(int)value}",
            nameof(AppSettings.Interpolation) => $"set interpolation {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CorrectDownscaling) => $"set correct-downscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.Scale) => $"set scale {(string)value}",
            nameof(AppSettings.DScale) => $"set dscale {(string)value}",
            nameof(AppSettings.VideoRotate) => $"set video-rotate {(string)value}",
            nameof(AppSettings.Deband) => $"set deband {(value is true ? "yes" : "no")}",
            nameof(AppSettings.LinearDownscaling) => $"set linear-downscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SigmoidUpscaling) => $"set sigmoid-upscaling {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ToneMapping) => $"set tone-mapping {(string)value}",
            nameof(AppSettings.TargetGamut) => string.IsNullOrWhiteSpace((string)value) ? null : $"set target-gamut {(string)value}",
            nameof(AppSettings.ToneMappingMaxBoost) => $"set tone-mapping-max-boost {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.HdrComputePeak) => $"set hdr-compute-peak {(string)value}",
            nameof(AppSettings.HdrPeakDecayRate) => $"set hdr-peak-decay-rate {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.HdrSceneThresholdLow) => $"set hdr-scene-threshold-low {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.HdrSceneThresholdHigh) => $"set hdr-scene-threshold-high {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.HdrContrastRecovery) => $"set hdr-contrast-recovery {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.HdrContrastSmoothness) => $"set hdr-contrast-smoothness {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.InverseToneMapping) => $"set inverse-tone-mapping {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ToneMappingVisualize) => $"set tone-mapping-visualize {(value is true ? "yes" : "no")}",
            nameof(AppSettings.VideoReversalBuffer) => (value is int vb && vb > 0) ? $"set video-reversal-buffer {vb}" : null,
            nameof(AppSettings.AudioReversalBuffer) => (value is int ab && ab > 0) ? $"set audio-reversal-buffer {ab}" : null,
            nameof(AppSettings.DitherDepth) => $"set dither-depth {(string)value}",
            nameof(AppSettings.HrSeek) => $"set hr-seek {(value is true ? "yes" : "no")}",
            nameof(AppSettings.HrSeekFramedrop) => $"set hr-seek-framedrop {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CacheOnDisk) => $"set cache-on-disk {(value is true ? "yes" : "no")}",
            nameof(AppSettings.TargetColorspaceHint) => $"set target-colorspace-hint {(string)value}",
            nameof(AppSettings.TargetColorspaceHintMode) => string.IsNullOrWhiteSpace((string)value) ? null : $"set target-colorspace-hint-mode {(string)value}",
            nameof(AppSettings.TargetColorspaceHintStrict) => $"set target-colorspace-hint-strict {(value is true ? "yes" : "no")}",
            nameof(AppSettings.TargetPrim) => string.IsNullOrWhiteSpace((string)value) ? null : $"set target-prim {(string)value}",
            nameof(AppSettings.TargetTrc) => string.IsNullOrWhiteSpace((string)value) ? null : $"set target-trc {(string)value}",
            nameof(AppSettings.TargetPeak) => (value is int peak && peak > 0) ? $"set target-peak {peak}" : null,
            nameof(AppSettings.GamutMappingMode) => string.IsNullOrWhiteSpace((string)value) ? null : $"set gamut-mapping-mode {(string)value}",
            nameof(AppSettings.VideoOutputLevels) => $"set video-output-levels {(string)value}",
            nameof(AppSettings.VideoDecodeDirect) => $"set vd-lavc-dr {(string)value}",
            nameof(AppSettings.IccProfileAuto) => $"set icc-profile-auto {(value is true ? "yes" : "no")}",
            nameof(AppSettings.IccProfile) => string.IsNullOrWhiteSpace((string)value) ? null : $"set icc-profile {Q((string)value)}",
            nameof(AppSettings.IccForceContrast) => (value is int contrast && contrast > 0) ? $"set icc-force-contrast {contrast}" : null,
            nameof(AppSettings.IccCache) => $"set icc-cache {(value is true ? "yes" : "no")}",
            nameof(AppSettings.Icc3dlutSize) => $"set icc-3dlut-size {(string)value}",
            nameof(AppSettings.DemuxerMaxBytes) => $"set demuxer-max-bytes {(int)value}MiB",
            nameof(AppSettings.DemuxerMaxBackBytes) => (value is int back && back > 0) ? $"set demuxer-max-back-bytes {back}MiB" : null,
            nameof(AppSettings.GpuShaderCache) => $"set gpu-shader-cache {(value is true ? "yes" : "no")}",
            nameof(AppSettings.GlslShadersAppend) => string.IsNullOrWhiteSpace((string)value) ? null : $"set glsl-shaders-append {Q((string)value)}",
            nameof(AppSettings.GlslShaders) => BuildShaderListCommand((string)value),
            nameof(AppSettings.GlslShaderOpts) => string.IsNullOrWhiteSpace((string)value) ? null : $"set glsl-shader-opts {Q((string)value)}",
            nameof(AppSettings.AudioChannels) => $"set audio-channels {(string)value}",
            nameof(AppSettings.AudioFormat) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-format {(string)value}",
            nameof(AppSettings.AudioSampleRate) => (value is int rate && rate > 0) ? $"set audio-samplerate {rate}" : null,
            nameof(AppSettings.AudioStreamSilence) => $"set audio-stream-silence {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioWaitOpen) => $"set audio-wait-open {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.AudioBuffer) => (value is double buf && buf > 0) ? $"set audio-buffer {buf.ToString(CultureInfo.InvariantCulture)}" : null,
            nameof(AppSettings.AudioSpdif) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-spdif {(string)value}",
            nameof(AppSettings.Replaygain) => $"set replaygain {(string)value}",
            nameof(AppSettings.OsdLevel) => $"set osd-level {(int)value}",
            nameof(AppSettings.ImageDisplayDuration) => $"set image-display-duration {((double)value).ToString(CultureInfo.InvariantCulture)}",
            // Startup-only option in this mpv build: runtime "set" fails, so
            // the value is written into the managed mpv.conf block instead.
            nameof(AppSettings.OverrideDisplayFps) => null,
            nameof(AppSettings.CachePause) => $"set cache-pause {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CachePauseInitial) => $"set cache-pause-initial {(value is true ? "yes" : "no")}",
            nameof(AppSettings.CachePauseWait) => $"set cache-pause-wait {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.PrefetchPlaylist) => $"set prefetch-playlist {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubBold) => $"set sub-bold {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubItalic) => $"set sub-italic {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubAlignX) => $"set sub-align-x {(string)value}",
            nameof(AppSettings.SubAlignY) => $"set sub-align-y {(string)value}",
            nameof(AppSettings.SubMarginX) => $"set sub-margin-x {(int)value}",
            nameof(AppSettings.SubMarginY) => $"set sub-margin-y {(int)value}",
            nameof(AppSettings.OsdAlignX) => $"set osd-align-x {(string)value}",
            nameof(AppSettings.OsdAlignY) => $"set osd-align-y {(string)value}",
            nameof(AppSettings.OsdMarginX) => $"set osd-margin-x {(int)value}",
            nameof(AppSettings.OsdMarginY) => $"set osd-margin-y {(int)value}",
            nameof(AppSettings.DemuxerHysteresisSecs) => $"set demuxer-hysteresis-secs {((double)value).ToString(CultureInfo.InvariantCulture)}",
            nameof(AppSettings.DemuxerCacheDir) => string.IsNullOrWhiteSpace((string)value) ? null : $"set demuxer-cache-dir {Q((string)value)}",
            nameof(AppSettings.AudioExclusive) => $"set audio-exclusive {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioPitchCorrection) => $"set audio-pitch-correction {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioNormalizeDownmix) => $"set audio-normalize-downmix {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AudioFileAuto) => $"set audio-file-auto {(string)value}",
            nameof(AppSettings.AudioFilePaths) => string.IsNullOrWhiteSpace((string)value) ? null : $"set audio-file-paths {Q((string)value)}",
            nameof(AppSettings.AudioDisplay) => $"set audio-display {(string)value}",
            nameof(AppSettings.AudioDelay) => $"set audio-delay {value}",
            nameof(AppSettings.SubAssOverride) => $"set sub-ass-override {(string)value}",
            nameof(AppSettings.SubBlur) => $"set sub-blur {value}",
            nameof(AppSettings.SubAuto) => $"set sub-auto {(string)value}",
            nameof(AppSettings.SubFont) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-font {Q((string)value)}",
            nameof(AppSettings.SubFontProvider) => $"set sub-font-provider {(string)value}",
            nameof(AppSettings.SubFontFile) => value is string fontFile && !string.IsNullOrWhiteSpace(fontFile)
                ? $"set sub-fonts-dir {Q(Path.GetDirectoryName(fontFile) ?? fontFile)}"
                : null,
            nameof(AppSettings.SubAssScaleWithWindow) => $"set sub-ass-scale-with-window {(value is true ? "yes" : "no")}",
            nameof(AppSettings.BlendSubtitles) => $"set blend-subtitles {(string)value}",
            nameof(AppSettings.SubFallback) => $"set subs-fallback {(string)value}",
            nameof(AppSettings.SubCodePage) => $"set sub-codepage {(string)value}",
            nameof(AppSettings.SubOutlineSize) => $"set sub-outline-size {value}",
            nameof(AppSettings.SubShadowOffset) => $"set sub-shadow-offset {value}",
            nameof(AppSettings.SubEmbeddedFonts) => $"set embeddedfonts {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubUseMargins) => $"set sub-use-margins {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubAssForceMargins) => $"set sub-ass-force-margins {(value is true ? "yes" : "no")}",
            nameof(AppSettings.StretchImageSubsToScreen) => $"set stretch-image-subs-to-screen {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdFontSize) => $"set osd-font-size {value}",
            nameof(AppSettings.OsdFont) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-font {Q((string)value)}",
            nameof(AppSettings.OsdColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-color {Q((string)value)}",
            nameof(AppSettings.OsdOutlineColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-outline-color {Q((string)value)}",
            nameof(AppSettings.OsdPlayingMsg) => AppContext.AppSetting.ShowOsdPlayingMsg
                ? (string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-playing-msg {Q((string)value)}")
                : "set osd-playing-msg \"\"",
            nameof(AppSettings.OsdPlayingMsgDuration) => $"set osd-playing-msg-duration {(int)value}",
            nameof(AppSettings.OsdBarWidth) => $"set osd-bar-w {(int)value}",
            nameof(AppSettings.OsdBarHeight) => $"set osd-bar-h {(double)value}",
            nameof(AppSettings.OsdBlur) => $"set osd-blur {(double)value}",
            nameof(AppSettings.OsdOutlineSize) => $"set osd-outline-size {(double)value}",
            nameof(AppSettings.OsdFractions) => $"set osd-fractions {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdOnSeek) => $"set osd-on-seek {(string)value}",
            nameof(AppSettings.OsdDuration) => $"set osd-duration {value}",
            nameof(AppSettings.CacheSecs) => (value is int n && n > 0) ? $"set cache-secs {n}" : null,
            nameof(AppSettings.ScreenshotPngCompression) => $"set screenshot-png-compression {value}",
            nameof(AppSettings.ScreenshotPngFilter) => $"set screenshot-png-filter {(int)value}",
            nameof(AppSettings.ScreenshotWebpQuality) => $"set screenshot-webp-quality {value}",
            nameof(AppSettings.ScreenshotWebpLossless) => $"set screenshot-webp-lossless {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotWebpCompression) => $"set screenshot-webp-compression {(int)value}",
            nameof(AppSettings.ScreenshotJxlDistance) => $"set screenshot-jxl-distance {(int)value}",
            nameof(AppSettings.ScreenshotJxlEffort) => $"set screenshot-jxl-effort {(int)value}",
            nameof(AppSettings.ScreenshotAvifEncoder) => string.IsNullOrWhiteSpace((string)value) ? null : $"set screenshot-avif-encoder {Q((string)value)}",
            nameof(AppSettings.ScreenshotHighBitDepth) => $"set screenshot-high-bit-depth {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotTagColorspace) => $"set screenshot-tag-colorspace {(value is true ? "yes" : "no")}",
            nameof(AppSettings.ScreenshotSw) => $"set screenshot-sw {(value is true ? "yes" : "no")}",
            nameof(AppSettings.VsrAutoEnabled) => $"set user-data/mpvw/vsr-auto {(value is true ? "yes" : "no")}",
            nameof(AppSettings.HdrAutoMode) => $"script-message-to hdr_auto mode {(string)value}",
            nameof(AppSettings.SeekHoldEnabled) => $"set user-data/mpvw/seek-hold {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdScale) => $"set osd-scale {(double)value}",
            nameof(AppSettings.OsdSpacing) => $"set osd-spacing {(double)value}",
            nameof(AppSettings.OsdBold) => $"set osd-bold {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdItalic) => $"set osd-italic {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdJustify) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-justify {(string)value}",
            nameof(AppSettings.OsdShadowOffset) => $"set osd-shadow-offset {(double)value}",
            nameof(AppSettings.OsdFontsDir) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-fonts-dir {(string)value}",
            nameof(AppSettings.OsdFontProvider) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-font-provider {(string)value}",
            nameof(AppSettings.OsdShaper) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-shaper {(string)value}",
            nameof(AppSettings.OsdBackColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-back-color {(string)value}",
            nameof(AppSettings.OsdSelectedColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-selected-color {(string)value}",
            nameof(AppSettings.OsdSelectedOutlineColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set osd-selected-outline-color {(string)value}",
            nameof(AppSettings.OsdScaleByWindow) => $"set osd-scale-by-window {(value is true ? "yes" : "no")}",
            nameof(AppSettings.OsdMarginYOffset) => $"set osd-margin-y-offset {(int)value}",
            nameof(AppSettings.SubVisibility) => $"set sub-visibility {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubFixTiming) => $"set sub-fix-timing {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubFixTimingThreshold) => $"set sub-fix-timing-threshold {(int)value}",
            nameof(AppSettings.SubsMatchOsLanguage) => $"set subs-match-os-language {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubsWithMatchingAudio) => string.IsNullOrWhiteSpace((string)value) ? null : $"set subs-with-matching-audio {(string)value}",
            nameof(AppSettings.SubsFallbackForced) => string.IsNullOrWhiteSpace((string)value) ? null : $"set subs-fallback-forced {(string)value}",
            nameof(AppSettings.SubForcedEventsOnly) => $"set sub-forced-events-only {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubShaper) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-shaper {(string)value}",
            nameof(AppSettings.SubScale) => $"set sub-scale {(double)value}",
            nameof(AppSettings.SubSpacing) => $"set sub-spacing {(double)value}",
            nameof(AppSettings.SubOutlineColor) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-outline-color {(string)value}",
            nameof(AppSettings.SubGauss) => $"set sub-gauss {(double)value}",
            nameof(AppSettings.SubMarginYOffset) => $"set sub-margin-y-offset {(int)value}",
            nameof(AppSettings.SubAssJustify) => $"set sub-ass-justify {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubAssPruneDelay) => $"set sub-ass-prune-delay {(double)value}",
            nameof(AppSettings.SubBitmapMaxSize) => $"set sub-bitmap-max-size {(int)value}",
            nameof(AppSettings.SecondarySubAssOverride) => string.IsNullOrWhiteSpace((string)value) ? null : $"set secondary-sub-ass-override {(string)value}",
            nameof(AppSettings.SubFilterSdh) => $"set sub-filter-sdh {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SubFilterSdhEnclosures) => string.IsNullOrWhiteSpace((string)value) ? null : $"set sub-filter-sdh-enclosures {(string)value}",
            nameof(AppSettings.SubFilterSdhHarder) => $"set sub-filter-sdh-harder {(value is true ? "yes" : "no")}",
            nameof(AppSettings.AbLoopA) => ((double)value > 0 ? $"set ab-loop-a {(double)value}" : "set ab-loop-a no"),
            nameof(AppSettings.AbLoopB) => ((double)value > 0 ? $"set ab-loop-b {(double)value}" : "set ab-loop-b no"),
            nameof(AppSettings.AbLoopCount) => ((int)value < 0 ? "set ab-loop-count inf" : $"set ab-loop-count {(int)value}"),
            nameof(AppSettings.CursorAutohide) => string.IsNullOrWhiteSpace((string)value) ? null : $"set cursor-autohide {(string)value}",
            nameof(AppSettings.CursorAutohideFsOnly) => $"set cursor-autohide-fs-only {(value is true ? "yes" : "no")}",
            nameof(AppSettings.WindowBorder) => $"set border {(value is true ? "yes" : "no")}",
            nameof(AppSettings.TitleBar) => string.IsNullOrWhiteSpace((string)value) ? null : $"set title-bar {(string)value}",
            nameof(AppSettings.VideoZoom) => $"set video-zoom {(double)value}",
            nameof(AppSettings.VideoPanX) => $"set video-pan-x {(double)value}",
            nameof(AppSettings.VideoPanY) => $"set video-pan-y {(double)value}",
            nameof(AppSettings.VideoAlignX) => $"set video-align-x {(double)value}",
            nameof(AppSettings.VideoAlignY) => $"set video-align-y {(double)value}",
            nameof(AppSettings.SecondarySubVisibility) => $"set secondary-sub-visibility {(value is true ? "yes" : "no")}",
            nameof(AppSettings.SecondarySubDelay) => $"set secondary-sub-delay {(double)value}",
            nameof(AppSettings.SecondarySubPos) => $"set secondary-sub-pos {(double)value}",
            nameof(AppSettings.SecondarySubScale) => $"set secondary-sub-scale {(double)value}",
            nameof(AppSettings.GpuApi) => string.IsNullOrWhiteSpace((string)value) ? null : $"set gpu-api {(string)value}",
            nameof(AppSettings.GpuContext) => string.IsNullOrWhiteSpace((string)value) ? null : $"set gpu-context {(string)value}",
            nameof(AppSettings.ScaleAntiring) => $"set scale-antiring {(double)value}",
            nameof(AppSettings.CscaleAntiring) => $"set cscale-antiring {(double)value}",

            // Playback speed. This arm used to be missing, and the absence was
            // silent rather than loud: the fall-through below is
            // GeneratedCommand, which only understands Mpv_* keys, so the
            // hand-written Speed row on the Playback page stored a value, called
            // ApplyMpv, got null back and sent nothing. Meanwhile the generated
            // Mpv_speed row -- a second row for the same mpv option, on the
            // Program page -- did work. Two rows for `speed`, and the one a user
            // is likelier to find was the dead one.
            //
            // Adding the arm fixes the row and removes its twin at the same
            // time: the generator derives its skip list from `set <name>` in
            // this file, so regenerating drops Mpv_speed by itself.
            //
            // It stays out of ApplyAll -- see SessionStateKeys -- so that
            // Reset All cannot clobber the speed of a live session.
            nameof(AppSettings.Speed) => $"set speed {(double)value}",

            // Everything else is a schema-generated row. Those 548 options
            // share one grammar, so they share one branch instead of 548:
            // the schema declared the type, the generator wrote the typed
            // AppSettings property, and the literal is written here from
            // that property's runtime type. Adding a generated row needs no
            // change to this method at all.
            //
            // The curated cases above win, which is what makes it safe to
            // generate a row for an option and later hand-write a better
            // one: deleting the generated row is enough, and until then the
            // hand-written case is the one that runs.
            _ => GeneratedCommand(key, value),
        };

        // mpv shows an OSD for every property change made with "set" (e.g.
        // "Hardware decoding: ...", "user-data/mpvw/seek-hold: yes").
        // Batch/applying settings would spam the screen at startup and from
        // the settings window; the app has its own UI feedback, so suppress
        // mpv's built-in set OSD.
        return cmd is { } c && c.StartsWith("set ", StringComparison.Ordinal)
            ? "no-osd " + c
            : cmd;
    }

    /// <summary>
    /// Applies every AppSettings property that has an mpv mapping to the live
    /// player. <see cref="ToCommand"/> is the single source of truth: this
    /// method enumerates the properties reflectively, so adding a setting only
    /// requires a new ToCommand case (no second table to keep in sync).
    /// Read-only properties (e.g. AppVersion) and unmapped keys return null
    /// from ToCommand and are skipped. Volume/Speed are intentionally not
    /// mapped for apply-time: volume is passed at mpv Initialize and speed
    /// defaults to 1.0, so a settings reset never clobbers the live session.
    /// </summary>
    public static List<string> BuildApplyAllCommands()
    {
        var settings = AppContext.AppSetting;
        var props = typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var commands = new List<string>();
        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite)
            {
                continue;
            }

            // Startup-only options cannot be changed through runtime `set`;
            // they are written to mpv.conf instead. Keep this set conservative
            // until each entry is verified against the manual (audit A5).
            if (ConfigOnlyKeys.Contains(prop.Name))
            {
                continue;
            }

            // A hand-written line in mpv.conf wins. mpv has already applied it
            // at startup, so sending our own value here would silently undo the
            // user's edit - the one thing the precedence model promises not to
            // do. Skipping is what makes "edited mpv.conf before launch" mean
            // "mpv.conf decides".
            if (MpvConfOverrides.IsUserOwned(ToMpvOptionName(prop.Name)))
            {
                continue;
            }

            // Commands that target scripts must wait until the scripts are
            // actually loaded (first FileLoaded); sending them during the
            // startup batch races script registration and silently fails.
            if (StartupDeferredKeys.Contains(prop.Name))
            {
                continue;
            }

            // Session state: mapping it exists (so the row works and the value
            // persists), but a blanket re-apply is not a reset the user asked
            // for. See SessionStateKeys.
            if (SessionStateKeys.Contains(prop.Name))
            {
                continue;
            }

            // Options the embedding depends on. Sending these would override a decision
            // Initialize made for the embedded case -- and for `idle` that override is what
            // stopped the app from playing anything at all. See EmbeddingOwnedKeys.
            if (EmbeddingOwnedKeys.Contains(prop.Name))
            {
                continue;
            }

            object? value;
            try
            {
                value = prop.GetValue(settings);
            }
            catch
            {
                // A failing getter must not abort the whole apply pass.
                continue;
            }
            if (value is null)
            {
                continue;
            }

            if (ToCommand(prop.Name, value) is { } cmd)
            {
                commands.Add(cmd);
            }
        }
        return commands;
    }

    public static void ApplyAll(Action<string> run)
    {
        foreach (var cmd in BuildApplyAllCommands())
        {
            run(cmd);
        }
    }

    /// <summary>
    /// Command for a schema-generated row, or null when the key is not a
    /// generated one.
    ///
    /// Splitting this out of the switch keeps the switch a list of curated
    /// decisions. The generated rows share one grammar -- the schema declared
    /// the type and the generator wrote the typed property -- so the literal
    /// is written once from the property's runtime type instead of 548 times
    /// by hand. A type the schema cannot currently produce returns null
    /// rather than emitting a guess, so an unknown value never reaches mpv as
    /// a malformed command.
    /// </summary>
    private static string? GeneratedCommand(string key, object value)
    {
        if (!GeneratedOptionNames.TryGetValue(key, out var option))
        {
            return null;
        }

        return value switch
        {
            // mpv's config and command grammars both spell a flag yes/no.
            bool b => $"set {option} {(b ? "yes" : "no")}",
            int i => $"set {option} {i.ToString(CultureInfo.InvariantCulture)}",
            double d => $"set {option} {d.ToString("0.0#####", CultureInfo.InvariantCulture)}",
            // An empty string means "leave it to mpv", so it must produce NO
            // command at all. This used to emit `set option ` with a trailing
            // space, which reads like "set it to empty" but is not: measured
            // against the live player, that line is not answered with an error,
            // it makes mpv drop the IPC connection. 62 of the 76 commands
            // rejected on a single launch were this shape.
            string s when string.IsNullOrWhiteSpace(s) => null,
            // A zero size or duration is the schema's way of saying "no limit",
            // which is mpv's default, so it produces no command either. Some
            // options reject `0` and `0B` alike (demuxer-lavf-probesize,
            // stream-buffer-size) and there is nothing to gain by trying.
            string s when IsZeroQuantity(s) => null,
            // A size or duration formatted by the schema generator arrives as
            // "512.000 MiB". mpv wants the number and the unit adjacent and
            // unquoted, e.g. 512MiB, and rejects the quoted form (6 more of
            // the 76).
            //
            // Do not write an example as "<set option value>" prose here: the
            // generator scrapes MpvSettings.cs with a regex that looks for that
            // shape to learn which options the hand-written rows own, and a
            // command inside a comment counts as a mapping. One such comment
            // named an option the schema generates, so the generator silently
            // stopped generating it and the settings row and its two language
            // keys became orphans.
            string s when TryUnitValue(s, out var unit) => $"set {option} {unit}",
            // An enumerated option takes a bare token. Quoting it is an error, not a
            // harmless extra: measured in one day's log, the player rejected the quoted
            // form eleven times, and every enumerated option in the schema shared the fault.
            //
            // The set comes from the generator, because the runtime shape cannot answer
            // this -- an enumerated option and a free-text one are both `string` here. A
            // free-text value with a space in it does need Q(); an enum value never does,
            // because the schema lists the spellings mpv accepts.
            //
            // Note for the next reader: do NOT write this fault out as a literal command in
            // a comment. The generator scrapes this file for the shape of a hand-written
            // mapping, and a command inside a comment counts -- that has already caused one
            // option to silently stop being generated.
            string s when GeneratedEnumOptions.Contains(option) => $"set {option} {s}",
            string s => $"set {option} {Q(s)}",
            _ => null,
        };
    }

    /// <summary>
    /// Whether a formatted quantity is zero, so nothing should be sent for it.
    ///
    /// Separate from <see cref="TryUnitValue"/> because that one answers "can I
    /// spell this as number+unit", and a zero fails there while still needing to
    /// be recognised: falling through to the generic branch would emit `"0 B"`
    /// quoted, which mpv rejects.
    /// </summary>
    private static bool IsZeroQuantity(string value)
    {
        var match = UnitValuePattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }
        return double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) == 0;
    }

    /// <summary>
    /// A size or duration the schema formats as "512.000 MiB", rewritten to the
    /// form mpv accepts: "512MiB".
    ///
    /// The generator stores these as strings because the model has no numeric
    /// type with a unit, so they arrive here already formatted. mpv reads the
    /// number and the unit as adjacent tokens and refuses them inside quotes.
    /// </summary>
    private static bool TryUnitValue(string value, out string result)
    {
        result = string.Empty;
        var match = UnitValuePattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }
        var number = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
        // "1.000 MiB" -> "1MiB": the formatter always writes a fractional
        // part, and a trailing .0 is not what the mpv manual shows.
        var text = number == Math.Floor(number) && Math.Abs(number) < 1e15
            ? ((long)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString("0.0###", CultureInfo.InvariantCulture);
        result = text + match.Groups["u"].Value;
        return true;
    }

    private static readonly Regex UnitValuePattern = new(
        @"^(?<n>\d+(?:\.\d+)?)\s*(?<u>B|KiB|MiB|GiB|TiB|kB|KB|MB|GB|TB|[mM]s|s|min|h)$",
        RegexOptions.Compiled);

    /// <summary>
    /// The mpv option a settings key writes, or null when the key does not map
    /// to one (a script message, a config-only entry). Callers use it to ask
    /// whether mpv.conf already names that option.
    /// </summary>
    public static string? ToMpvOptionName(string key) =>
        MpvOptionNames.TryGetValue(key, out var name)
            ? name
            // The generated rows live in their own dictionary so an mpv
            // upgrade rewrites one file. Both are consulted here so callers
            // keep asking one question -- "does this key name an option?" --
            // rather than having to know which half a key came from.
            : GeneratedOptionNames.TryGetValue(key, out var generated) ? generated : null;

    /// <summary>
    /// A real mpv.conf option name: lowercase, digits and dashes, nothing else.
    ///
    /// The filter matters. <see cref="MpvOptionNames"/> also carries the
    /// script-communication keys (<c>user-data/mpvw/vsr-auto</c> and friends),
    /// which are runtime properties, not options. Writing them into mpv.conf
    /// would append lines mpv cannot parse, so they are rejected here.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ConfigOptionName =
        new("^[a-z0-9][a-z0-9-]*$",
            System.Text.RegularExpressions.RegexOptions.Compiled
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// One <c>name=value</c> line for mpv.conf, or null when the key cannot be
    /// written there.
    ///
    /// Derived from <see cref="ToCommand"/> rather than restating names and
    /// value grammar: the command already quotes strings, spells booleans
    /// yes/no and maps enums, and the config syntax accepts the same value
    /// grammar after <c>name=</c>. Reformatting that output keeps one source of
    /// truth, so a new ToCommand case is automatically persistable.
    /// </summary>
    public static string? ToConfLine(string key, object? value)
    {
        if (value is null)
        {
            return null;
        }

        var name = ToMpvOptionName(key);
        if (name is null || !ConfigOptionName.IsMatch(name))
        {
            return null;
        }

        // Startup-only options have no runtime command by design, so ToCommand
        // cannot format them. They are still options the window owns, so they
        // get their line from here - otherwise they would be silently dropped
        // from the file the moment they became config-only.
        if (ConfigOnlyConfLine(key, value) is { } configOnly)
        {
            return configOnly;
        }

        if (ToCommand(key, value) is not { } command)
        {
            return null;
        }

        // ToCommand prefixes "no-osd " to suppress mpv's per-set OSD, and that
        // prefix is a runtime concern with no meaning in a config file.
        const string osdPrefix = "no-osd ";
        var set = command.StartsWith(osdPrefix, StringComparison.Ordinal)
            ? command[osdPrefix.Length..]
            : command;

        const string setPrefix = "set ";
        if (!set.StartsWith(setPrefix, StringComparison.Ordinal))
        {
            // script-message-to and friends: not an option assignment.
            return null;
        }

        var body = set[setPrefix.Length..];
        var space = body.IndexOf(' ');
        if (space <= 0 || space == body.Length - 1)
        {
            return null;
        }

        return body[..space] + "=" + body[(space + 1)..];
    }

    /// <summary>
    /// Config line for an option that is deliberately absent from
    /// <see cref="ToCommand"/> because it only takes effect at mpv start.
    ///
    /// Returns null when the value means "leave it alone", so no line is written
    /// and mpv's own default stands - the same meaning the old writer gave a
    /// commented-out placeholder, without the dead line.
    /// </summary>
    private static string? ConfigOnlyConfLine(string key, object value)
    {
        return key switch
        {
            // 0 is mpv's "off"; anything above it is a forced frame rate, and
            // mpv wants the number as written (1000/1001 is a valid value).
            nameof(AppSettings.OverrideDisplayFps) =>
                (double)value > 0
                    ? "override-display-fps="
                      + ((double)value).ToString(CultureInfo.InvariantCulture)
                    : null,

            // No runtime command (see ToCommand), so this is the only producer.
            // mpv.conf takes the integer as written; the option is a float with
            // a 100..1000 range and an integer is a legal value for it.
            nameof(AppSettings.VolumeMax) => "volume-max=" + (int)value,
            _ => null,
        };
    }

    /// <summary>
    /// Every mpv.conf line the settings window owns, sorted by option name.
    ///
    /// Sorted because the block is rewritten on every change and compared
    /// against what was written last time; a stable order means an unchanged
    /// setting produces a byte-identical block rather than a reshuffle. It also
    /// makes the file readable.
    /// </summary>
    public static List<string> BuildConfigLines()
    {
        var settings = AppContext.AppSetting;
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prop in typeof(AppSettings).GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite)
            {
                continue;
            }

            object? value;
            try
            {
                value = prop.GetValue(settings);
            }
            catch
            {
                // One failing getter must not cost the whole file.
                continue;
            }

            if (ToConfLine(prop.Name, value) is not { } line)
            {
                continue;
            }

            // Two settings can map to the same option (a bool and its detail
            // field); the first wins so the file never carries a duplicate.
            var name = line[..line.IndexOf('=')];
            if (seen.Add(name))
            {
                lines.Add(line);
            }
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    /// <summary>
    /// Maps a settings key to the mpv option name it writes through
    /// <see cref="ToCommand"/>. Derived from that switch, not a second source of
    /// truth: it exists so the settings window can ask whether mpv.conf already
    /// names the same option, and so the config writer can name the line it
    /// persists.
    ///
    /// Keys with no mpv option - script messages - are absent and report as
    /// unmapped. The startup-only entries ARE here: they have no runtime
    /// command, but they are options the window owns and writes to the file.
    /// </summary>
    private static readonly Dictionary<string, string> MpvOptionNames = new(StringComparer.Ordinal)
    {
        [nameof(AppSettings.Hwdec)] = "hwdec",
        [nameof(AppSettings.HwdecCodecs)] = "hwdec-codecs",
        [nameof(AppSettings.OverrideDisplayFps)] = "override-display-fps",
        [nameof(AppSettings.AlwaysOnTop)] = "ontop",
        [nameof(AppSettings.InputIme)] = "input-ime",
        [nameof(AppSettings.StartFullscreen)] = "fullscreen",
        [nameof(AppSettings.D3d11OutputCsp)] = "d3d11-output-csp",
        [nameof(AppSettings.D3d11OutputFormat)] = "d3d11-output-format",
        [nameof(AppSettings.D3d11SyncInterval)] = "d3d11-sync-interval",
        [nameof(AppSettings.D3d11Warp)] = "d3d11-warp",
        [nameof(AppSettings.D3d11ExclusiveFs)] = "d3d11-exclusive-fs",
        [nameof(AppSettings.D3d11Flip)] = "d3d11-flip",
        [nameof(AppSettings.D3d11Adapter)] = "d3d11-adapter",
        [nameof(AppSettings.VolumeMax)] = "volume-max",
        [nameof(AppSettings.AudioGapless)] = "gapless-audio",
        [nameof(AppSettings.KeepOpen)] = "keep-open",
        [nameof(AppSettings.LoopPlaylist)] = "loop-playlist",
        [nameof(AppSettings.LoopFile)] = "loop-file",
        [nameof(AppSettings.CacheEnabled)] = "cache",
        [nameof(AppSettings.DemuxerReadahead)] = "demuxer-readahead-secs",
        [nameof(AppSettings.UserAgent)] = "user-agent",
        [nameof(AppSettings.Referrer)] = "referrer",
        [nameof(AppSettings.HttpHeaderFields)] = "http-header-fields",
        [nameof(AppSettings.HttpProxy)] = "http-proxy",
        [nameof(AppSettings.CookiesFile)] = "cookies-file",
        [nameof(AppSettings.TlsVerify)] = "tls-verify",
        [nameof(AppSettings.NetworkTimeout)] = "network-timeout",
        [nameof(AppSettings.CurlMaxRedirects)] = "curl-max-redirects",
        [nameof(AppSettings.CurlMaxRetries)] = "curl-max-retries",
        [nameof(AppSettings.CurlConnectTimeout)] = "curl-connect-timeout",
        [nameof(AppSettings.CurlBufferSize)] = "curl-buffer-size",
        [nameof(AppSettings.CurlMaxRequestSize)] = "curl-max-request-size",
        [nameof(AppSettings.PictureBrightness)] = "brightness",
        [nameof(AppSettings.PictureContrast)] = "contrast",
        [nameof(AppSettings.PictureSaturation)] = "saturation",
        [nameof(AppSettings.PictureGamma)] = "gamma",
        [nameof(AppSettings.PictureHue)] = "hue",
        [nameof(AppSettings.PictureSharpen)] = "sharpen",
        [nameof(AppSettings.AutoWindowResize)] = "auto-window-resize",
        [nameof(AppSettings.AdLavcDownmix)] = "ad-lavc-downmix",
        [nameof(AppSettings.AutoCreatePlaylist)] = "autocreate-playlist",
        [nameof(AppSettings.DirectoryMode)] = "directory-mode",
        [nameof(AppSettings.DirectoryFilterTypes)] = "directory-filter-types",
        [nameof(AppSettings.VideoExts)] = "video-exts",
        [nameof(AppSettings.ImageExts)] = "image-exts",
        [nameof(AppSettings.AudioExts)] = "audio-exts",
        [nameof(AppSettings.InputIpcServer)] = "input-ipc-server",
        [nameof(AppSettings.WatchLaterDir)] = "watch-later-dir",
        [nameof(AppSettings.WatchLaterOptions)] = "watch-later-options",
        [nameof(AppSettings.IccCacheDir)] = "icc-cache-dir",
        [nameof(AppSettings.GpuShaderCacheDir)] = "gpu-shader-cache-dir",
        [nameof(AppSettings.Deinterlace)] = "deinterlace",
        [nameof(AppSettings.AspectRatio)] = "video-aspect-override",
        [nameof(AppSettings.Cscale)] = "cscale",
        [nameof(AppSettings.Tscale)] = "tscale",
        [nameof(AppSettings.LinearUpscaling)] = "linear-upscaling",
        [nameof(AppSettings.Dither)] = "dither",
        [nameof(AppSettings.Panscan)] = "panscan",
        [nameof(AppSettings.VideoUnscaled)] = "video-unscaled",
        [nameof(AppSettings.BackgroundTileColor0)] = "background-tile-color-0",
        [nameof(AppSettings.BackgroundTileColor1)] = "background-tile-color-1",
        [nameof(AppSettings.BackgroundTileSize)] = "background-tile-size",
        [nameof(AppSettings.SubFontSize)] = "sub-font-size",
        [nameof(AppSettings.SubScaleByWindow)] = "sub-scale-by-window",
        [nameof(AppSettings.SubLineSpacing)] = "sub-line-spacing",
        [nameof(AppSettings.SubJustify)] = "sub-justify",
        [nameof(AppSettings.SubClearOnSeek)] = "sub-clear-on-seek",
        [nameof(AppSettings.SubHinting)] = "sub-hinting",
        [nameof(AppSettings.SubDelay)] = "sub-delay",
        [nameof(AppSettings.SubPos)] = "sub-pos",
        [nameof(AppSettings.AudioLanguage)] = "alang",
        [nameof(AppSettings.SubtitleLanguage)] = "slang",
        [nameof(AppSettings.SubFilePaths)] = "sub-file-paths",
        [nameof(AppSettings.SubHdrPeak)] = "sub-hdr-peak",
        [nameof(AppSettings.ImageSubsHdrPeak)] = "image-subs-hdr-peak",
        [nameof(AppSettings.ImageSubsVideoResolution)] = "image-subs-video-resolution",
        [nameof(AppSettings.SubAssStyleOverrides)] = "sub-ass-style-overrides",
        [nameof(AppSettings.SubColor)] = "sub-color",
        [nameof(AppSettings.SubBackColor)] = "sub-back-color",
        [nameof(AppSettings.SubBorderColor)] = "sub-border-color",
        [nameof(AppSettings.SubScaleSigns)] = "sub-scale-signs",
        [nameof(AppSettings.SubAssUseVideoData)] = "sub-ass-use-video-data",
        [nameof(AppSettings.SubAssVideoAspectOverride)] = "sub-ass-video-aspect-override",
        [nameof(AppSettings.SubAssVsfilterColorCompat)] = "sub-ass-vsfilter-color-compat",
        [nameof(AppSettings.AudioDevice)] = "audio-device",
        [nameof(AppSettings.ScreenshotDirectory)] = "screenshot-directory",
        [nameof(AppSettings.ScreenshotTemplate)] = "screenshot-template",
        [nameof(AppSettings.SavePositionOnQuit)] = "save-position-on-quit",
        [nameof(AppSettings.ResumePlayback)] = "resume-playback",
        // The playback-speed row. It needs both halves to be a first-class
        // setting: a ToCommand arm so a change reaches mpv, and this entry so
        // the window can ask whether a hand-written `speed=` line in mpv.conf
        // owns the option. With only the arm, check-mpvconf-precedence.py
        // reports it as a key that "can never report being overridden" -- which
        // is exactly what it was before, silently editable while the file said
        // otherwise.
        [nameof(AppSettings.Speed)] = "speed",
        [nameof(AppSettings.ScreenshotFormat)] = "screenshot-format",
        [nameof(AppSettings.ScreenshotJpegQuality)] = "screenshot-jpeg-quality",
        [nameof(AppSettings.ScreenshotJpegSourceChroma)] = "screenshot-jpeg-source-chroma",
        [nameof(AppSettings.VideoSync)] = "video-sync",
        [nameof(AppSettings.VideoSyncMaxVideoChange)] = "video-sync-max-video-change",
        [nameof(AppSettings.Interpolation)] = "interpolation",
        [nameof(AppSettings.CorrectDownscaling)] = "correct-downscaling",
        [nameof(AppSettings.Scale)] = "scale",
        [nameof(AppSettings.DScale)] = "dscale",
        [nameof(AppSettings.VideoRotate)] = "video-rotate",
        [nameof(AppSettings.Deband)] = "deband",
        [nameof(AppSettings.LinearDownscaling)] = "linear-downscaling",
        [nameof(AppSettings.SigmoidUpscaling)] = "sigmoid-upscaling",
        [nameof(AppSettings.ToneMapping)] = "tone-mapping",
        [nameof(AppSettings.TargetGamut)] = "target-gamut",
        [nameof(AppSettings.ToneMappingMaxBoost)] = "tone-mapping-max-boost",
        [nameof(AppSettings.HdrComputePeak)] = "hdr-compute-peak",
        [nameof(AppSettings.HdrPeakDecayRate)] = "hdr-peak-decay-rate",
        [nameof(AppSettings.HdrSceneThresholdLow)] = "hdr-scene-threshold-low",
        [nameof(AppSettings.HdrSceneThresholdHigh)] = "hdr-scene-threshold-high",
        [nameof(AppSettings.HdrContrastRecovery)] = "hdr-contrast-recovery",
        [nameof(AppSettings.HdrContrastSmoothness)] = "hdr-contrast-smoothness",
        [nameof(AppSettings.InverseToneMapping)] = "inverse-tone-mapping",
        [nameof(AppSettings.ToneMappingVisualize)] = "tone-mapping-visualize",
        [nameof(AppSettings.VideoReversalBuffer)] = "video-reversal-buffer",
        [nameof(AppSettings.AudioReversalBuffer)] = "audio-reversal-buffer",
        [nameof(AppSettings.DitherDepth)] = "dither-depth",
        [nameof(AppSettings.HrSeek)] = "hr-seek",
        [nameof(AppSettings.HrSeekFramedrop)] = "hr-seek-framedrop",
        [nameof(AppSettings.CacheOnDisk)] = "cache-on-disk",
        [nameof(AppSettings.TargetColorspaceHint)] = "target-colorspace-hint",
        [nameof(AppSettings.TargetColorspaceHintMode)] = "target-colorspace-hint-mode",
        [nameof(AppSettings.TargetColorspaceHintStrict)] = "target-colorspace-hint-strict",
        [nameof(AppSettings.TargetPrim)] = "target-prim",
        [nameof(AppSettings.TargetTrc)] = "target-trc",
        [nameof(AppSettings.TargetPeak)] = "target-peak",
        [nameof(AppSettings.GamutMappingMode)] = "gamut-mapping-mode",
        [nameof(AppSettings.VideoOutputLevels)] = "video-output-levels",
        [nameof(AppSettings.VideoDecodeDirect)] = "vd-lavc-dr",
        [nameof(AppSettings.IccProfileAuto)] = "icc-profile-auto",
        [nameof(AppSettings.IccProfile)] = "icc-profile",
        [nameof(AppSettings.IccForceContrast)] = "icc-force-contrast",
        [nameof(AppSettings.IccCache)] = "icc-cache",
        [nameof(AppSettings.Icc3dlutSize)] = "icc-3dlut-size",
        [nameof(AppSettings.DemuxerMaxBytes)] = "demuxer-max-bytes",
        [nameof(AppSettings.DemuxerMaxBackBytes)] = "demuxer-max-back-bytes",
        [nameof(AppSettings.GpuShaderCache)] = "gpu-shader-cache",
        [nameof(AppSettings.GlslShadersAppend)] = "glsl-shaders-append",
        [nameof(AppSettings.GlslShaderOpts)] = "glsl-shader-opts",
        [nameof(AppSettings.AudioChannels)] = "audio-channels",
        [nameof(AppSettings.AudioFormat)] = "audio-format",
        [nameof(AppSettings.AudioSampleRate)] = "audio-samplerate",
        [nameof(AppSettings.AudioStreamSilence)] = "audio-stream-silence",
        [nameof(AppSettings.AudioWaitOpen)] = "audio-wait-open",
        [nameof(AppSettings.AudioBuffer)] = "audio-buffer",
        [nameof(AppSettings.AudioSpdif)] = "audio-spdif",
        [nameof(AppSettings.Replaygain)] = "replaygain",
        [nameof(AppSettings.OsdLevel)] = "osd-level",
        [nameof(AppSettings.ImageDisplayDuration)] = "image-display-duration",
        [nameof(AppSettings.CachePause)] = "cache-pause",
        [nameof(AppSettings.CachePauseInitial)] = "cache-pause-initial",
        [nameof(AppSettings.CachePauseWait)] = "cache-pause-wait",
        [nameof(AppSettings.PrefetchPlaylist)] = "prefetch-playlist",
        [nameof(AppSettings.SubBold)] = "sub-bold",
        [nameof(AppSettings.SubItalic)] = "sub-italic",
        [nameof(AppSettings.SubAlignX)] = "sub-align-x",
        [nameof(AppSettings.SubAlignY)] = "sub-align-y",
        [nameof(AppSettings.SubMarginX)] = "sub-margin-x",
        [nameof(AppSettings.SubMarginY)] = "sub-margin-y",
        [nameof(AppSettings.OsdAlignX)] = "osd-align-x",
        [nameof(AppSettings.OsdAlignY)] = "osd-align-y",
        [nameof(AppSettings.OsdMarginX)] = "osd-margin-x",
        [nameof(AppSettings.OsdMarginY)] = "osd-margin-y",
        [nameof(AppSettings.DemuxerHysteresisSecs)] = "demuxer-hysteresis-secs",
        [nameof(AppSettings.DemuxerCacheDir)] = "demuxer-cache-dir",
        [nameof(AppSettings.AudioExclusive)] = "audio-exclusive",
        [nameof(AppSettings.AudioPitchCorrection)] = "audio-pitch-correction",
        [nameof(AppSettings.AudioNormalizeDownmix)] = "audio-normalize-downmix",
        [nameof(AppSettings.AudioFileAuto)] = "audio-file-auto",
        [nameof(AppSettings.AudioFilePaths)] = "audio-file-paths",
        [nameof(AppSettings.AudioDisplay)] = "audio-display",
        [nameof(AppSettings.AudioDelay)] = "audio-delay",
        [nameof(AppSettings.SubAssOverride)] = "sub-ass-override",
        [nameof(AppSettings.SubBlur)] = "sub-blur",
        [nameof(AppSettings.SubAuto)] = "sub-auto",
        [nameof(AppSettings.SubFont)] = "sub-font",
        [nameof(AppSettings.SubFontProvider)] = "sub-font-provider",
        [nameof(AppSettings.SubFontFile)] = "sub-fonts-dir",
        [nameof(AppSettings.SubAssScaleWithWindow)] = "sub-ass-scale-with-window",
        [nameof(AppSettings.BlendSubtitles)] = "blend-subtitles",
        [nameof(AppSettings.SubFallback)] = "subs-fallback",
        [nameof(AppSettings.SubCodePage)] = "sub-codepage",
        [nameof(AppSettings.SubOutlineSize)] = "sub-outline-size",
        [nameof(AppSettings.SubShadowOffset)] = "sub-shadow-offset",
        [nameof(AppSettings.SubEmbeddedFonts)] = "embeddedfonts",
        [nameof(AppSettings.SubUseMargins)] = "sub-use-margins",
        [nameof(AppSettings.SubAssForceMargins)] = "sub-ass-force-margins",
        [nameof(AppSettings.OsdScale)] = "osd-scale",
        [nameof(AppSettings.OsdSpacing)] = "osd-spacing",
        [nameof(AppSettings.OsdBold)] = "osd-bold",
        [nameof(AppSettings.OsdItalic)] = "osd-italic",
        [nameof(AppSettings.OsdJustify)] = "osd-justify",
        [nameof(AppSettings.OsdShadowOffset)] = "osd-shadow-offset",
        [nameof(AppSettings.OsdFontsDir)] = "osd-fonts-dir",
        [nameof(AppSettings.OsdFontProvider)] = "osd-font-provider",
        [nameof(AppSettings.OsdShaper)] = "osd-shaper",
        [nameof(AppSettings.OsdBackColor)] = "osd-back-color",
        [nameof(AppSettings.OsdSelectedColor)] = "osd-selected-color",
        [nameof(AppSettings.OsdSelectedOutlineColor)] = "osd-selected-outline-color",
        [nameof(AppSettings.OsdScaleByWindow)] = "osd-scale-by-window",
        [nameof(AppSettings.OsdMarginYOffset)] = "osd-margin-y-offset",
        [nameof(AppSettings.SubVisibility)] = "sub-visibility",
        [nameof(AppSettings.SubFixTiming)] = "sub-fix-timing",
        [nameof(AppSettings.SubFixTimingThreshold)] = "sub-fix-timing-threshold",
        [nameof(AppSettings.SubsMatchOsLanguage)] = "subs-match-os-language",
        [nameof(AppSettings.SubsWithMatchingAudio)] = "subs-with-matching-audio",
        [nameof(AppSettings.SubsFallbackForced)] = "subs-fallback-forced",
        [nameof(AppSettings.SubForcedEventsOnly)] = "sub-forced-events-only",
        [nameof(AppSettings.SubShaper)] = "sub-shaper",
        [nameof(AppSettings.SubScale)] = "sub-scale",
        [nameof(AppSettings.SubSpacing)] = "sub-spacing",
        [nameof(AppSettings.SubOutlineColor)] = "sub-outline-color",
        [nameof(AppSettings.SubGauss)] = "sub-gauss",
        [nameof(AppSettings.SubMarginYOffset)] = "sub-margin-y-offset",
        [nameof(AppSettings.SubAssJustify)] = "sub-ass-justify",
        [nameof(AppSettings.SubAssPruneDelay)] = "sub-ass-prune-delay",
        [nameof(AppSettings.SubBitmapMaxSize)] = "sub-bitmap-max-size",
        [nameof(AppSettings.SecondarySubAssOverride)] = "secondary-sub-ass-override",
        [nameof(AppSettings.SubFilterSdh)] = "sub-filter-sdh",
        [nameof(AppSettings.SubFilterSdhEnclosures)] = "sub-filter-sdh-enclosures",
        [nameof(AppSettings.SubFilterSdhHarder)] = "sub-filter-sdh-harder",
        [nameof(AppSettings.AbLoopA)] = "ab-loop-a",
        [nameof(AppSettings.AbLoopB)] = "ab-loop-b",
        [nameof(AppSettings.AbLoopCount)] = "ab-loop-count",
        [nameof(AppSettings.CursorAutohide)] = "cursor-autohide",
        [nameof(AppSettings.CursorAutohideFsOnly)] = "cursor-autohide-fs-only",
        [nameof(AppSettings.WindowBorder)] = "border",
        [nameof(AppSettings.TitleBar)] = "title-bar",
        [nameof(AppSettings.VideoZoom)] = "video-zoom",
        [nameof(AppSettings.VideoPanX)] = "video-pan-x",
        [nameof(AppSettings.VideoPanY)] = "video-pan-y",
        [nameof(AppSettings.VideoAlignX)] = "video-align-x",
        [nameof(AppSettings.VideoAlignY)] = "video-align-y",
        [nameof(AppSettings.SecondarySubVisibility)] = "secondary-sub-visibility",
        [nameof(AppSettings.SecondarySubDelay)] = "secondary-sub-delay",
        [nameof(AppSettings.SecondarySubPos)] = "secondary-sub-pos",
        [nameof(AppSettings.SecondarySubScale)] = "secondary-sub-scale",
        [nameof(AppSettings.GpuApi)] = "gpu-api",
        [nameof(AppSettings.GpuContext)] = "gpu-context",
        [nameof(AppSettings.ScaleAntiring)] = "scale-antiring",
        [nameof(AppSettings.CscaleAntiring)] = "cscale-antiring",
        [nameof(AppSettings.StretchImageSubsToScreen)] = "stretch-image-subs-to-screen",
        [nameof(AppSettings.OsdFontSize)] = "osd-font-size",
        [nameof(AppSettings.OsdFont)] = "osd-font",
        [nameof(AppSettings.OsdColor)] = "osd-color",
        [nameof(AppSettings.OsdOutlineColor)] = "osd-outline-color",
        [nameof(AppSettings.OsdPlayingMsg)] = "osd-playing-msg",
        [nameof(AppSettings.OsdPlayingMsgDuration)] = "osd-playing-msg-duration",
        [nameof(AppSettings.OsdBarWidth)] = "osd-bar-w",
        [nameof(AppSettings.OsdBarHeight)] = "osd-bar-h",
        [nameof(AppSettings.OsdBlur)] = "osd-blur",
        [nameof(AppSettings.OsdOutlineSize)] = "osd-outline-size",
        [nameof(AppSettings.OsdFractions)] = "osd-fractions",
        [nameof(AppSettings.OsdOnSeek)] = "osd-on-seek",
        [nameof(AppSettings.OsdDuration)] = "osd-duration",
        [nameof(AppSettings.CacheSecs)] = "cache-secs",
        [nameof(AppSettings.ScreenshotPngCompression)] = "screenshot-png-compression",
        [nameof(AppSettings.ScreenshotPngFilter)] = "screenshot-png-filter",
        [nameof(AppSettings.ScreenshotWebpQuality)] = "screenshot-webp-quality",
        [nameof(AppSettings.ScreenshotWebpLossless)] = "screenshot-webp-lossless",
        [nameof(AppSettings.ScreenshotWebpCompression)] = "screenshot-webp-compression",
        [nameof(AppSettings.ScreenshotJxlDistance)] = "screenshot-jxl-distance",
        [nameof(AppSettings.ScreenshotJxlEffort)] = "screenshot-jxl-effort",
        [nameof(AppSettings.ScreenshotAvifEncoder)] = "screenshot-avif-encoder",
        [nameof(AppSettings.ScreenshotHighBitDepth)] = "screenshot-high-bit-depth",
        [nameof(AppSettings.ScreenshotTagColorspace)] = "screenshot-tag-colorspace",
        [nameof(AppSettings.ScreenshotSw)] = "screenshot-sw",
        [nameof(AppSettings.VsrAutoEnabled)] = "user-data/mpvw/vsr-auto",
        [nameof(AppSettings.SeekHoldEnabled)] = "user-data/mpvw/seek-hold",
    };

    /// <summary>AppSettings mapped to mpv options that only take effect at startup.</summary>
    private static readonly HashSet<string> ConfigOnlyKeys = new(StringComparer.Ordinal)
    {
        // Measured, not assumed (tools/mvpw-mcp/diag_writable.py): these cannot be
        // set on a running player.
        //
        //   init-only    player-operation-mode, demuxer-rawvideo-size
        //                readable, refused on `set`. This set is what they are
        //                for, and ConfigOnlyConfLine writes them to mpv.conf so
        //                the value still reaches mpv at startup.
        //
        // Six others used to be listed here -- openal-direct-channels,
        // openal-num-buffers, openal-num-samples, cuda-decode-device,
        // input-gamepad, demuxer-sbr-probesize. They are gone from this list
        // because they are gone from the schema: they do not exist in the bundled
        // mpv (v0.41.0-dev-ge8673660a), so there is no setting to apply and no row
        // to render. Listing them here would not compile, which is the right
        // outcome -- the drift is fixed at its source rather than suppressed.
        nameof(AppSettings.Mpv_player_operation_mode),
        nameof(AppSettings.Mpv_demuxer_rawvideo_size),
        nameof(AppSettings.InputIpcServer),
        nameof(AppSettings.IccCacheDir),
        nameof(AppSettings.GpuShaderCacheDir),
        nameof(AppSettings.DemuxerCacheDir),
        nameof(AppSettings.OverrideDisplayFps),
        // volume-max: no runtime command (see ToCommand), written by
        // ConfigOnlyConfLine instead. ConfigOnlyKeys is what keeps ApplyAll
        // from trying to send it, and what tells check-conf-lines that this
        // option is owned and formatted rather than silently dropped.
        nameof(AppSettings.VolumeMax),
    };

    /// <summary>AppSettings keys applied after the first file loads.</summary>
    private static readonly HashSet<string> StartupDeferredKeys = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.HdrAutoMode),
    };

    /// <summary>Settings that describe the live session, not a saved preference.
    ///
    /// They deliberately stay out of <see cref="ApplyAll"/> so that Reset All
    /// cannot clobber what the user is currently watching: speed would jump back
    /// to 1.0 and the volume slider would move on its own. Volume is not listed
    /// because it is not mapped at all -- it is passed to mpv at Initialize.
    ///
    /// Speed is applied once at startup instead, which it now gets for free:
    /// <see cref="ToCommand"/> has an arm for it, so it is part of the startup
    /// batch rather than the hand-written special case that used to sit in
    /// MpvPlayerPage next to the ApplyAll call.
    ///
    /// A separate set from <see cref="StartupDeferredKeys"/> on purpose. That
    /// one means "mpv cannot accept this yet"; this one means "applying it
    /// would be wrong". Merging them would make the next reader assume a timing
    /// constraint that does not exist.</summary>
    private static readonly HashSet<string> SessionStateKeys = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.Speed),
    };

    /// <summary>
    /// Options the embedding itself depends on. The settings window may show them and store a
    /// value, but the startup batch must never send them, because doing so replaces a decision
    /// <c>MpvPlayer::Initialize</c> made for a reason.
    ///
    /// **This is the root cause of "the video will not play", found by bisection.**
    ///
    /// The startup batch is 515 commands and runs before the first user open. Sending a prefix
    /// of it and then a plain `loadfile` gives, measured in the real app process:
    ///
    ///     cut=0    fileloaded=1   loads
    ///     cut=152  fileloaded=1   loads
    ///     cut=153  fileloaded=0   BLOCKED, and every later loadfile is dead too
    ///
    /// Command 152 is `no-osd set idle no`. `AppSettings.Mpv_idle` defaults to "no" because it
    /// mirrors mpv's own default, so every launch sent it without anyone asking. But
    /// <c>MpvPlayer::Initialize</c> sets <c>idle=yes</c> deliberately: with `idle=no` mpv exits
    /// its playloop when there is no file, and an embedded player is ALWAYS in that state at
    /// startup. After the batch runs, `mpv_command` still returns 0 for `loadfile` and mpv then
    /// does nothing at all -- no START_FILE, no FILE_LOADED, no further log line. That is the
    /// exact symptom this task started from.
    ///
    /// A separate set from its neighbours on purpose: <see cref="ConfigOnlyKeys"/> means "must
    /// be written to mpv.conf", <see cref="StartupDeferredKeys"/> means "mpv cannot accept this
    /// yet", <see cref="SessionStateKeys"/> means "applying it would be a reset". This one means
    /// "the app owns this option; the batch must not override it".
    /// </summary>
    private static readonly HashSet<string> EmbeddingOwnedKeys = new(StringComparer.Ordinal)
    {
        // Without idle=yes the playloop exits before any file is opened.
        nameof(AppSettings.Mpv_idle),

        // Force-window and the composition output are chosen by Initialize for the embedded
        // case; mpv.conf values for them are already documented as ignored (AGENTS.md), and
        // sending them through `set` after init has the same effect as leaving them alone.
        nameof(AppSettings.Mpv_force_window),
        nameof(AppSettings.Mpv_d3d11_output_mode),
    };

    // --- read-back: mpv.conf -> AppSettings ---------------------------------

    /// <summary>mpv option name -> the AppSettings property that writes it.</summary>
    private static readonly Dictionary<string, string> KeysByOptionName = BuildKeysByOptionName();

    private static Dictionary<string, string> BuildKeysByOptionName()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, name) in MpvOptionNames)
        {
            // First key wins, matching BuildConfigLines: when two settings share
            // an option the writer only ever emits the first, so reading back
            // into any other one would edit a value the file cannot express.
            map.TryAdd(name, key);
        }
        return map;
    }

    /// <summary>
    /// Loads every option the deployed mpv.conf carries into
    /// <see cref="AppContext.AppSetting"/>.
    ///
    /// <para>This is the read half of "mpv.conf is the source of truth": the
    /// window is a view of the file, so a value typed into mpv.conf by hand has
    /// to show up in the UI. Without it the file would only be an output, and a
    /// hand edit would silently disagree with what the window displays.</para>
    ///
    /// <para><b>What it does NOT overwrite.</b> A line the user owns for being
    /// somewhere the app does not manage - the user-overrides block, or a body
    /// line that differs from what we ship - IS applied: that is precisely the
    /// edit this exists to surface, and mpv itself is already running with that
    /// value. What is skipped is a line the app has no mapping for, since there
    /// is no property to load it into; those stay confined to the file, which is
    /// how an option the window does not expose keeps working.</para>
    ///
    /// <para>Called once at startup, before the player initialises, so the UI
    /// and the first apply pass agree with the file on disk.</para>
    /// </summary>
    public static void LoadFromConfig()
    {
        var path = ManagedMpvConfig.MpvConfPath;
        if (!File.Exists(path))
        {
            return;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "mpv.conf unreadable, settings not loaded from disk");
            return;
        }

        // Only the last active assignment for an option can win: mpv reads the
        // file top-down and a later line overwrites an earlier one. Scanning in
        // order and letting each hit replace the previous reproduces that, so the
        // UI ends up showing the value mpv actually runs with.
        var effective = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inProfile = false;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            // A profile section only applies when activated, so its lines are
            // not the file's effective values. Skipping them keeps the UI showing
            // the top-level configuration.
            if (trimmed.StartsWith('['))
            {
                inProfile = true;
                continue;
            }

            if (inProfile)
            {
                continue;
            }

            var name = AssignmentOptionName(raw);
            if (name is null || !KeysByOptionName.ContainsKey(name))
            {
                continue;
            }

            // The managed-block markers are comments and already skipped above;
            // any other non-assignment line in the block is somebody's note.
            if (trimmed.StartsWith(ManagedMpvConfig.BeginMarker, StringComparison.Ordinal)
                || trimmed.StartsWith(ManagedMpvConfig.EndMarker, StringComparison.Ordinal))
            {
                continue;
            }

            effective[name] = AssignmentValue(raw);
        }

        var applied = 0;
        foreach (var (name, text) in effective)
        {
            if (TryWriteSetting(KeysByOptionName[name], text))
            {
                applied++;
            }
        }

        // Logged unconditionally, not only when applied > 0: a silent zero is
        // indistinguishable from "the read never ran", and the call order in
        // AppContext.Init is exactly the kind of thing that can regress without
        // anything else noticing.
        AppContext.AppLogger.Info(
            "settings loaded from mpv.conf: {} of {} option(s) mapped", applied, effective.Count);
    }

    /// <summary>The option name of a top-level assignment, or null.</summary>
    private static string? AssignmentOptionName(string raw)
    {
        var line = StripComment(raw);
        if (line.Length == 0)
        {
            return null;
        }

        var equals = line.IndexOf('=');
        if (equals <= 0)
        {
            return null;
        }

        var name = line[..equals].Trim();
        return ConfigOptionName.IsMatch(name) ? name : null;
    }

    /// <summary>The value of an assignment, with any trailing comment removed
    /// and surrounding whitespace trimmed - the exact grammar
    /// <see cref="ToConfLine"/> emits.</summary>
    private static string AssignmentValue(string raw)
    {
        var line = StripComment(raw);
        var equals = line.IndexOf('=');
        return equals < 0 ? string.Empty : line[(equals + 1)..].Trim();
    }

    /// <summary>
    /// Drops a trailing "# ..." comment and rejects a fully commented line.
    ///
    /// Shared with the ownership reader, which used to carry a verbatim second
    /// copy of this rule. Both are now the one implementation in
    /// <see cref="MpvConfValueCodec"/>, which is also the file that owns the
    /// quoting this has to agree with, and which unit tests can reach.
    /// </summary>
    private static string StripComment(string raw) => MpvConfValueCodec.StripComment(raw);

    /// <summary>
    /// Writes one conf value into the AppSettings property that owns it, using
    /// the property's declared type - never a guess.
    ///
    /// <para>Reflection is the point: the mapping is declared once in
    /// <see cref="MpvOptionNames"/> and the conversion follows the property's
    /// own type, so a setting whose type changes (or a new one) cannot end up
    /// silently parsed as the wrong kind. Returns false when the value does not
    /// parse, so the caller can count real loads rather than attempts.</para>
    /// </summary>
    private static bool TryWriteSetting(string key, string text)
    {
        var prop = typeof(AppSettings).GetProperty(key,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanWrite)
        {
            return false;
        }

        object? value;
        try
        {
            value = prop.PropertyType switch
            {
                // Parsing is delegated to the codec so it can mirror the encodings
                // ToCommand applies, and so it is reachable from the test project.
                // The previous inline version used plain double/int TryParse, which
                // could not read back a single value the app itself had written for
                // the settings that encode a sentinel or a unit: `no` for a cleared
                // A/B loop point, `inf` for "loop forever", and the `MiB` suffix on
                // the demuxer limits. The log recorded 60 such failures across 6
                // settings, each silently reverting a user's value in the UI.
                _ => MpvConfValueCodec.ParseMpvValue(prop.PropertyType, text),
            };
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "mpv.conf value not readable for {}", key);
            return false;
        }

        if (value is null)
        {
            // A value mpv accepts but this property cannot represent (an empty
            // enum, an out-of-range number). Leaving the stored setting alone is
            // the safe reading: mpv still runs with the file's value, and the UI
            // keeps showing the last one it understood.
            AppContext.AppLogger.Warn(
                "mpv.conf value not convertible for {}: '{}'", key, text);
            return false;
        }

        try
        {
            // The codec already returns the property's own type, so this is a
            // no-op for everything it parses today. It stays as the second line
            // of defence: PropertyInfo.SetValue never converts, so a producer
            // that widens a value again - a conditional whose arms fall to a
            // common type is how it happens, and is exactly this round's bug in
            // MpvConfValueCodec - would reject the whole setting rather than
            // load it. Converting here means the loader survives that.
            prop.SetValue(AppContext.AppSetting, CoerceToPropertyType(value, prop.PropertyType));
            return true;
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "mpv.conf value rejected by AppSettings.{}", key);
            return false;
        }
    }

    /// <summary>
    /// Narrows a parsed value to the property's declared type before reflection.
    ///
    /// <para><c>PropertyInfo.SetValue</c> requires the exact type: a boxed
    /// <c>Int64</c> handed to an <c>int</c> property throws
    /// <c>ArgumentException</c> rather than converting, which is what the
    /// 2026-10-08..14 log recorded for every integer setting read from
    /// mpv.conf. Unwraps a <c>Nullable&lt;T&gt;</c>, maps an enum through
    /// <see cref="Enum.ToObject"/>, and otherwise converts with
    /// <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.</para>
    /// </summary>
    private static object CoerceToPropertyType(object value, Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, value);
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Serializes the shader-list editor value into an mpv <c>glsl-shaders</c>
    /// set command: entries are ';'-separated and disabled entries (prefixed
    /// with '!') are excluded from the applied list.
    /// </summary>
    private static string? BuildShaderListCommand(string value)
    {
        var enabled = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !part.StartsWith("!", StringComparison.Ordinal))
            .ToList();
        return enabled.Count == 0 ? null : $"set glsl-shaders {Q(string.Join(';', enabled))}";
    }
}
