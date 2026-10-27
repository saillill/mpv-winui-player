using System;
using System.Threading;
using System.Threading.Tasks;
using mpv_winui.Domains.Playback.Audio;
using NAudio.Wave;

namespace mpv_winui.Domains.Playback.Audio;

/// <summary>
/// Captures what the machine is playing, so the rhythm strip shows the track
/// rather than a storyboard.
///
/// WASAPI loopback on the default render device, not a tap inside mpv. Two
/// reasons, in order of how much they mattered:
///
/// 1. libmpv does not publish samples. `mpv_render_context` is video-only (its
///    header exposes frame callbacks and nothing else), there is no
///    audio-frame callback, and no property carries PCM. The alternatives are a
///    `--ao=...` half of mpv's own output chain, which this build does not ship,
///    or writing our own ao - both far more invasive than reading the endpoint.
/// 2. Loopback is what the user sees the point of: it captures the app's audio
///    wherever it comes from, so the visualiser keeps working with exclusive-mode
///    passthrough and with any future audio source.
///
/// The trade-off, stated plainly: loopback hears everything on the device, not
/// just this app. With another program playing at the same time the bars show
/// both. That is the correct behaviour for a "what is coming out of the speakers"
/// display and the wrong one for a per-track scope, and there is no third option
/// available from here.
///
/// Event-driven rather than polling: WasapiLoopbackCapture raises DataAvailable on
/// its own thread at the device's buffer cadence, and this file never sleeps.
/// </summary>
internal sealed class AudioCapture : IDisposable
{
    private readonly Lock _gate = new();

    private WasapiLoopbackCapture? _capture;

    private SpectrumAnalyzer? _analyzer;

    /// <summary>Latest analysed frame, replaced wholesale. Null until the first block.</summary>
    private double[]? _latest;

    private bool _disposed;

    /// <summary>
    /// The band levels from the most recent block, or null if nothing has arrived.
    ///
    /// A copy, because the UI thread reads this while the capture thread replaces
    /// it; handing out the array itself would let a draw land on a half-written
    /// frame.
    /// </summary>
    internal double[]? LatestLevels()
    {
        lock (_gate)
        {
            return _latest is null ? null : (double[])_latest.Clone();
        }
    }

    /// <summary>True once a device is open and delivering blocks.</summary>
    internal bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _capture is not null;
            }
        }
    }

    /// <summary>
    /// Starts capturing, or does nothing if it is already running.
    ///
    /// Failure is not fatal and not reported to the user: a machine with no render
    /// device, or one where loopback is refused, simply has no visualiser, and the
    /// page falls back to the still bars it drew before. A dialog about a metre
    /// would be worse than the silence.
    /// </summary>
    internal bool Start(int bandCount)
    {
        lock (_gate)
        {
            if (_disposed || _capture is not null)
            {
                return _capture is not null;
            }

            try
            {
                var capture = new WasapiLoopbackCapture();

                // The device's own mix format: resampling here would cost a
                // converter and gain nothing, because the analyser is told the rate.
                var format = capture.WaveFormat;

                _analyzer = new SpectrumAnalyzer(bandCount, format.SampleRate);

                capture.DataAvailable += OnDataAvailable;

                capture.RecordingStopped += OnRecordingStopped;

                capture.StartRecording();

                _capture = capture;
                return true;
            }
            catch (Exception)
            {
                // No device, no permission, no loopback: all one outcome here.
                _capture = null;
                _analyzer = null;
                return false;
            }
        }
    }

    /// <summary>Stops capturing and drops the analyser's state.</summary>
    internal void Stop()
    {
        WasapiLoopbackCapture? capture;

        lock (_gate)
        {
            capture = _capture;
            _capture = null;
            _analyzer = null;
            _latest = null;
        }

        if (capture is null)
        {
            return;
        }

        try
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.StopRecording();
            capture.Dispose();
        }
        catch (Exception)
        {
            // A device that disappeared mid-stop throws; there is nothing to do
            // about it and nothing to report.
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        lock (_gate)
        {
            var analyzer = _analyzer;

            if (analyzer is null || args.BytesRecorded <= 0)
            {
                return;
            }

            // Loopback hands back the device's mix format, which is float32 in
            // shared mode on every Windows build this app supports. Guarded rather
            // than assumed: a 16-bit device would otherwise be read as garbage
            // floats and the bars would flicker at full height forever.
            var format = (sender as WasapiLoopbackCapture)?.WaveFormat;

            if (format is null || format.Encoding != WaveFormatEncoding.IeeeFloat
                || format.BitsPerSample != 32)
            {
                return;
            }

            _latest = analyzer.Analyze(ToMono(args.Buffer, args.BytesRecorded, format.Channels));
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        lock (_gate)
        {
            _latest = null;
        }
    }

    /// <summary>
    /// Interleaved float frames down to one channel.
    ///
    /// Averaged rather than taking the left channel: a mix panned hard right would
    /// read as silence, which is the one failure mode a visualiser must not have.
    /// </summary>
    private static float[] ToMono(byte[] buffer, int byteCount, int channels)
    {
        if (channels < 1)
        {
            channels = 1;
        }

        var frameCount = byteCount / (sizeof(float) * channels);

        var mono = new float[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0.0f;

            for (var channel = 0; channel < channels; channel++)
            {
                var offset = ((frame * channels) + channel) * sizeof(float);

                sum += BitConverter.ToSingle(buffer, offset);
            }

            mono[frame] = sum / channels;
        }

        return mono;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
    }
}
