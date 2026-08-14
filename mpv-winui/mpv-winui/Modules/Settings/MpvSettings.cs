using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// 设置项 → mpv 命令 的映射：设置页改动时即时下发，播放器初始化后批量应用。
/// </summary>
public static class MpvSettings
{
    /// <summary>
    /// Quote a value for mpv's command string parser. mpv_command_string uses
    /// C-style escapes inside quotes, so Windows paths must have their
    /// backslashes doubled, otherwise "C:\Users\..." fails to parse and the
    /// option silently keeps its previous value.
    /// </summary>
    private static string Q(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

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
            nameof(AppSettings.VolumeMax) => $"set volume-max {value}",
            nameof(AppSettings.AudioGapless) => $"set gapless-audio {(string)value}",
            nameof(AppSettings.KeepOpen) => $"set keep-open {value}",
            nameof(AppSettings.LoopPlaylist) => $"set loop-playlist {(string)value}",
            nameof(AppSettings.LoopFile) => $"set loop-file {(value is true ? "inf" : "no")}",
            nameof(AppSettings.CacheEnabled) => $"set cache {(string)value}",
            nameof(AppSettings.DemuxerReadahead) => $"set demuxer-readahead-secs {(double)value}",
            nameof(AppSettings.Ytdl) => $"set ytdl {(value is true ? "yes" : "no")}",
            nameof(AppSettings.YtdlRawOptionsAppend) => string.IsNullOrWhiteSpace((string)value) ? null : $"set ytdl-raw-options-append {Q((string)value)}",
            nameof(AppSettings.YtdlFormat) => string.IsNullOrWhiteSpace((string)value) ? null : $"set ytdl-format {Q((string)value)}",
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
            nameof(AppSettings.AutoCreatePlaylist) => $"set autocreate-playlist {(string)value}",
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
            nameof(AppSettings.OverrideDisplayFps) => $"set override-display-fps {((double)value).ToString(CultureInfo.InvariantCulture)}",
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
            _ => null,
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
    public static void ApplyAll(Action<string> run)
    {
        var settings = AppContext.AppSetting;
        var props = typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
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
                run(cmd);
            }
        }
    }

    /// <summary>AppSettings mapped to mpv options that only take effect at startup.</summary>
    private static readonly HashSet<string> ConfigOnlyKeys = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.InputIpcServer),
        nameof(AppSettings.IccCacheDir),
        nameof(AppSettings.GpuShaderCacheDir),
        nameof(AppSettings.DemuxerCacheDir),
    };

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
