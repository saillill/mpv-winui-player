using System;
using System.Collections.Generic;

namespace mpv_winui.Domains.Playback.Audio;

/// <summary>
/// Turns a block of mono samples into frequency-band levels.
///
/// The rhythm strip used to be sixteen Storyboards with hand-picked periods and
/// phases - a synthetic pulse whose own comment admitted it was not an analyser.
/// This is the part that makes it real: a radix-2 FFT over a window of samples,
/// reduced to the band levels a bar chart wants.
///
/// Deliberately free of WinRT, NAudio and any other dependency, down to plain
/// arrays: it is the one piece here that can be tested by number rather than by
/// eye, and it is linked into mpv-conf-test for exactly that reason. Everything
/// that touches a device lives in AudioCapture, on the other side of this file.
///
/// Scale is dB, not linear amplitude. A linear spectrum spends most of its height
/// on the bass and shows nothing above 2 kHz, because musical energy falls off
/// roughly as 1/f; decibels are what make a bar chart look like the track sounds.
/// </summary>
internal sealed class SpectrumAnalyzer
{
    /// <summary>
    /// Window size, in samples. 1024 at 44.1 kHz is 23 ms - about one video frame
    /// at 44 fps, which is as fast as a bar can usefully move and short enough
    /// that a drum hit lands on the frame it happened in.
    /// </summary>
    internal const int WindowSize = 1024;

    private readonly int _bandCount;

    /// <summary>Bit-reversal permutation for <see cref="WindowSize"/>, built once.</summary>
    private readonly int[] _reversal = new int[WindowSize];

    /// <summary>Hann window, precomputed: a rectangular window smears a tone across the whole spectrum.</summary>
    private readonly double[] _window = new double[WindowSize];

    private readonly double[] _real = new double[WindowSize];
    private readonly double[] _imaginary = new double[WindowSize];

    /// <summary>The band edges, as FFT bin indices.</summary>
    private readonly int[] _bandEdges;

    /// <summary>Smoothed levels, in dB, carried between calls.</summary>
    private readonly double[] _levels;

    private readonly double _sampleRate;

    internal SpectrumAnalyzer(int bandCount, double sampleRate)
    {
        if (bandCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bandCount));
        }

        _bandCount = bandCount;
        _sampleRate = sampleRate > 0 ? sampleRate : 44100.0;
        _levels = new double[bandCount];
        _bandEdges = BuildBandEdges(bandCount, _sampleRate);

        // Bit reversal, computed the slow way once rather than per frame.
        // Silence, not zero dB. The array is allocated zeroed, and 0 dB is the
        // loudest the scale goes - so without this the first frame of the first
        // track drew every bar at full height and then fell to the real level,
        // which reads as a peak that never happened.
        Reset();

        var bits = (int)Math.Log2(WindowSize);
        for (var i = 0; i < WindowSize; i++)
        {
            var reversed = 0;
            for (var b = 0; b < bits; b++)
            {
                reversed = (reversed << 1) | ((i >> b) & 1);
            }

            _reversal[i] = reversed;
        }

        for (var i = 0; i < WindowSize; i++)
        {
            // Hann.
            _window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (WindowSize - 1)));
        }
    }

    /// <summary>Number of bars this analyser produces.</summary>
    internal int BandCount => _bandCount;

    /// <summary>
    /// Band edges as FFT bins, spaced logarithmically.
    ///
    /// Linear bands put eleven of sixteen bars in the top octave and leave the bass
    /// - which is what a listener is looking at - in one bar. Log spacing is what
    /// makes the chart track what the ear hears.
    /// </summary>
    private static int[] BuildBandEdges(int bandCount, double sampleRate)
    {
        // 40 Hz to 16 kHz: below 40 is rumble, above 16k is mostly nothing in
        // lossy sources and the filter would show a permanently dead bar.
        const double lowHz = 40.0;
        const double highHz = 16000.0;

        var binHz = sampleRate / WindowSize;

        var edges = new int[bandCount + 1];

        var lowest = Math.Max(1, (int)Math.Floor(lowHz / binHz));
        var highest = Math.Min(WindowSize / 2 - 1, (int)Math.Ceiling(highHz / binHz));

        // 1/N octave spacing.
        for (var i = 0; i <= bandCount; i++)
        {
            var t = (double)i / bandCount;

            var hz = lowHz * Math.Pow(highHz / lowHz, t);

            var bin = (int)Math.Round(hz / binHz);

            edges[i] = Math.Clamp(bin, lowest, highest);

            // Guarantee forward progress: two edges at the same bin would give a
            // bar a zero-width range and, with it, a level of -inf.
            if (i > 0 && edges[i] <= edges[i - 1])
            {
                edges[i] = Math.Min(edges[i - 1] + 1, highest);
            }
        }

        return edges;
    }

    /// <summary>
    /// Analysed levels for one block, in dB, one per band, roughly -60..0.
    ///
    /// <paramref name="samples"/> is read from the start; anything past
    /// <see cref="WindowSize"/> is ignored and a short block is zero-padded.
    /// </summary>
    /// <param name="decay">How much of the previous frame survives, 0..1.</param>
    /// <returns>
    /// A fresh array per call, not the smoothing buffer. The buffer is reused
    /// across frames by design, so returning it would mean a caller that kept one
    /// frame to compare against the next would find its copy silently rewritten -
    /// which is exactly how the 80 Hz and 8 kHz assertions in the tests collided
    /// before this returned a copy.
    /// </returns>
    /// <remarks>
    /// The decay is what makes the display legible. An unsmoothed FFT flickers at
    /// frame rate - adjacent frames of the same kick drum differ enough that the
    /// bars look like noise rather than like the beat.
    /// </remarks>
    internal double[] Analyze(ReadOnlySpan<float> samples, double decay = 0.55)
    {
        // Hann-windowed copy, zero-padded to the FFT size.
        var count = Math.Min(samples.Length, WindowSize);

        for (var i = 0; i < WindowSize; i++)
        {
            var sample = i < count ? samples[i] : 0.0;

            _real[i] = sample * _window[i];
            _imaginary[i] = 0.0;
        }

        Fft();

        for (var band = 0; band < _bandCount; band++)
        {
            var from = _bandEdges[band];
            var to = Math.Max(from, _bandEdges[band + 1]);

            var peak = 0.0;

            for (var bin = from; bin < to; bin++)
            {
                var magnitude = Math.Sqrt(
                    _real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin]);

                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            // Normalised against the window's own gain, then dB, then clamped: a
            // silent band would otherwise be -inf and the bar would never recover
            // because decay * -inf is -inf.
            var normalised = peak / (WindowSize * 0.25);

            var db = normalised > 1e-7 ? 20.0 * Math.Log10(normalised) : -60.0;

            db = Math.Clamp(db, -60.0, 0.0);

            _levels[band] = (_levels[band] * decay) + (db * (1.0 - decay));
        }

        return (double[])_levels.Clone();
    }

    /// <summary>In-place iterative radix-2 Cooley-Tukey, real input in <see cref="_real"/>.</summary>
    private void Fft()
    {
        for (var i = 0; i < WindowSize; i++)
        {
            var j = _reversal[i];

            if (j <= i)
            {
                continue;
            }

            (_real[i], _real[j]) = (_real[j], _real[i]);
            (_imaginary[i], _imaginary[j]) = (_imaginary[j], _imaginary[i]);
        }

        for (var size = 2; size <= WindowSize; size <<= 1)
        {
            var halfSize = size >> 1;

            var angleStep = -2.0 * Math.PI / size;

            for (var start = 0; start < WindowSize; start += size)
            {
                for (var k = 0; k < halfSize; k++)
                {
                    var angle = angleStep * k;

                    var cos = Math.Cos(angle);
                    var sin = Math.Sin(angle);

                    var evenIndex = start + k;
                    var oddIndex = start + k + halfSize;

                    var oddReal = (_real[oddIndex] * cos) - (_imaginary[oddIndex] * sin);
                    var oddImaginary = (_real[oddIndex] * sin) + (_imaginary[oddIndex] * cos);

                    _real[oddIndex] = _real[evenIndex] - oddReal;
                    _imaginary[oddIndex] = _imaginary[evenIndex] - oddImaginary;

                    _real[evenIndex] += oddReal;
                    _imaginary[evenIndex] += oddImaginary;
                }
            }
        }
    }

    /// <summary>Forgets the smoothed state, for a track change.</summary>
    internal void Reset()
    {
        Array.Clear(_levels);

        for (var i = 0; i < _bandCount; i++)
        {
            _levels[i] = -60.0;
        }
    }
}
