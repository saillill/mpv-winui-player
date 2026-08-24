namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Audio output: device, mixer levels, DSP and stream options.
    /// </summary>
    public partial class AppSettings
    {
        public int VolumeMax
        {
            get => _dataSetting.GetValue(nameof(VolumeMax), 100);
            set => _dataSetting.SetValue(nameof(VolumeMax), value);
        }

        public string AudioLanguage
        {
            get => _dataSetting.GetValue(nameof(AudioLanguage), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioLanguage), value);
        }

        public string AudioDevice
        {
            get => _dataSetting.GetValue(nameof(AudioDevice), "auto");
            set => _dataSetting.SetValue(nameof(AudioDevice), value);
        }

        public int AudioReversalBuffer
        {
            get => _dataSetting.GetValue(nameof(AudioReversalBuffer), 0);
            set => _dataSetting.SetValue(nameof(AudioReversalBuffer), value);
        }

        public string AudioChannels
        {
            get => _dataSetting.GetValue(nameof(AudioChannels), "auto-safe");
            set => _dataSetting.SetValue(nameof(AudioChannels), value);
        }

        public string AudioFormat
        {
            get => _dataSetting.GetValue(nameof(AudioFormat), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioFormat), value);
        }

        public int AudioSampleRate
        {
            get => _dataSetting.GetValue(nameof(AudioSampleRate), 0);
            set => _dataSetting.SetValue(nameof(AudioSampleRate), value);
        }

        public bool AudioStreamSilence
        {
            get => _dataSetting.GetValue(nameof(AudioStreamSilence), false);
            set => _dataSetting.SetValue(nameof(AudioStreamSilence), value);
        }

        public bool AudioExclusive
        {
            get => _dataSetting.GetValue(nameof(AudioExclusive), false);
            set => _dataSetting.SetValue(nameof(AudioExclusive), value);
        }

        public bool AudioPitchCorrection
        {
            get => _dataSetting.GetValue(nameof(AudioPitchCorrection), true);
            set => _dataSetting.SetValue(nameof(AudioPitchCorrection), value);
        }

        public bool AudioNormalizeDownmix
        {
            get => _dataSetting.GetValue(nameof(AudioNormalizeDownmix), false);
            set => _dataSetting.SetValue(nameof(AudioNormalizeDownmix), value);
        }

        public string AudioFileAuto
        {
            get => _dataSetting.GetValue(nameof(AudioFileAuto), "fuzzy");
            set => _dataSetting.SetValue(nameof(AudioFileAuto), value);
        }

        public string AudioDisplay
        {
            get => _dataSetting.GetValue(nameof(AudioDisplay), "embedded-first");
            set => _dataSetting.SetValue(nameof(AudioDisplay), value);
        }

        public double AudioDelay
        {
            get => _dataSetting.GetValue(nameof(AudioDelay), 0.0);
            set => _dataSetting.SetValue(nameof(AudioDelay), value);
        }

        public string AudioFilePaths
        {
            get => _dataSetting.GetValue(nameof(AudioFilePaths), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioFilePaths), value);
        }

        public string AudioGapless
        {
            get => _dataSetting.GetValue(nameof(AudioGapless), "weak");
            set => _dataSetting.SetValue(nameof(AudioGapless), value);
        }

        /// <summary>Seconds to wait for the audio output to open (mpv audio-wait-open).</summary>
        public double AudioWaitOpen
        {
            get => _dataSetting.GetValue(nameof(AudioWaitOpen), 0.0);
            set => _dataSetting.SetValue(nameof(AudioWaitOpen), value);
        }

        /// <summary>Decoder-level stereo downmix for multichannel audio.</summary>
        public bool AdLavcDownmix { get => _dataSetting.GetValue(nameof(AdLavcDownmix), false); set => _dataSetting.SetValue(nameof(AdLavcDownmix), value); }

        /// <summary>Audio output buffer size in seconds (mpv audio-buffer, 0..10).</summary>
        public double AudioBuffer
        {
            get => _dataSetting.GetValue(nameof(AudioBuffer), 0.2);
            set => _dataSetting.SetValue(nameof(AudioBuffer), value);
        }

        // ===== Options added with mpv 0.41 built-in defaults =====
        public string AudioSpdif
        {
            get => _dataSetting.GetValue(nameof(AudioSpdif), string.Empty);
            set => _dataSetting.SetValue(nameof(AudioSpdif), value);
        }

        public string Replaygain
        {
            get => _dataSetting.GetValue(nameof(Replaygain), "no");
            set => _dataSetting.SetValue(nameof(Replaygain), value);
        }
    }
}
