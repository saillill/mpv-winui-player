using System;
using mpv_winui.Modules.Player.Audio;

namespace mpv_winui_test;

/// <summary>
/// The analyser, tested by number.
///
/// This is the only part of the visualiser that can be checked without an audio
/// device, and it is the part that decides whether the bars mean anything - so it
/// is where the assertions go. A pure sine at a known frequency must light the
/// band that contains that frequency and leave the far ones dark; if that
/// invariant does not hold, the display is decoration with a plausible shape.
/// </summary>
[TestFixture]
public class SpectrumAnalyzerTests
{
    private const double SampleRate = 44100.0;
    private const int Bands = 16;

    /// <summary>
    /// Which band a frequency lands in, using the analyser's own log spacing.
    /// Recomputed here rather than read back, so a bug in the analyser's edge table
    /// cannot hide by agreeing with itself.
    /// </summary>
    private static int ExpectedBand(double hz)
    {
        const double low = 40.0;
        const double high = 16000.0;

        var t = Math.Log(hz / low) / Math.Log(high / low);

        return Math.Clamp((int)Math.Floor(t * Bands), 0, Bands - 1);
    }

    private static float[] Sine(double hz, double amplitude = 0.5, int count = 1024)
    {
        var samples = new float[count];

        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * hz * i / SampleRate));
        }

        return samples;
    }

    [Test]
    public void ASineAtAKnownFrequencyLightsItsOwnBand()
    {
        var analyser = new SpectrumAnalyzer(Bands, SampleRate);

        // No decay for this one: the assertion is about where the energy is, and
        // smoothing would carry energy between bands if the table were wrong.
        var levels = analyser.Analyze(Sine(1000.0), decay: 0.0);

        var loudest = 0;

        for (var i = 1; i < levels.Length; i++)
        {
            if (levels[i] > levels[loudest])
            {
                loudest = i;
            }
        }

        Assert.That(loudest, Is.EqualTo(ExpectedBand(1000.0)),
            $"1 kHz should land in band {ExpectedBand(1000.0)}; levels were "
            + string.Join(", ", Array.ConvertAll(levels, l => l.ToString("F1"))));
    }

    [Test]
    public void BandsFarFromTheToneStayQuiet()
    {
        var analyser = new SpectrumAnalyzer(Bands, SampleRate);

        var levels = analyser.Analyze(Sine(1000.0), decay: 0.0);

        var toneBand = ExpectedBand(1000.0);

        // Two bands away and beyond: a Hann window leaks into immediate neighbours,
        // which is expected, but nothing a couple of octaves off should be within
        // 20 dB of the tone.
        for (var i = 0; i < levels.Length; i++)
        {
            if (Math.Abs(i - toneBand) <= 2)
            {
                continue;
            }

            Assert.That(levels[i], Is.LessThan(levels[toneBand] - 20.0),
                $"band {i} ({levels[i]:F1} dB) is too close to the tone band "
                + $"({levels[toneBand]:F1} dB)");
        }
    }

    [Test]
    public void ALowerToneLandsInALowerBand()
    {
        var analyser = new SpectrumAnalyzer(Bands, SampleRate);

        var lowLevels = analyser.Analyze(Sine(80.0), decay: 0.0);
        var highLevels = analyser.Analyze(Sine(8000.0), decay: 0.0);

        var lowPeak = Array.IndexOf(lowLevels, Max(lowLevels));
        var highPeak = Array.IndexOf(highLevels, Max(highLevels));

        Assert.That(lowPeak, Is.LessThan(highPeak),
            "80 Hz must peak below 8 kHz; a flat or inverted mapping would not");
    }

    [Test]
    public void SilenceIsQuietAndRecoversRatherThanSticking()
    {
        var analyser = new SpectrumAnalyzer(Bands, SampleRate);

        // Loud first, then silence: the decay must not strand the bars.
        analyser.Analyze(Sine(1000.0, amplitude: 0.9), decay: 0.0);

        double[] levels = [];
        for (var i = 0; i < 40; i++)
        {
            levels = analyser.Analyze(new float[1024]);
        }

        foreach (var level in levels)
        {
            Assert.That(level, Is.LessThan(-30.0),
                "after 40 silent frames every band should have fallen away");
        }
    }

    [Test]
    public void EveryBandHasANonEmptyFrequencyRange()
    {
        // Two edges at the same bin give a bar a zero-width range, and then its
        // level is -inf forever. The analyser guards that; this proves the guard
        // holds at a sample rate where the bands are narrow (96 kHz puts the
        // bottom bands close together in bin terms).
        foreach (var rate in new[] { 8000.0, 22050.0, 44100.0, 48000.0, 96000.0, 192000.0 })
        {
            var analyser = new SpectrumAnalyzer(Bands, rate);

            var levels = analyser.Analyze(Sine(440.0), decay: 0.0);

            foreach (var level in levels)
            {
                Assert.That(double.IsFinite(level), Is.True,
                    $"a band produced a non-finite level at {rate} Hz");
                Assert.That(level, Is.LessThanOrEqualTo(0.0));
                Assert.That(level, Is.GreaterThanOrEqualTo(-60.0));
            }
        }
    }

    [Test]
    public void DifferentBandCountsAllProduceThatManyLevels()
    {
        foreach (var count in new[] { 4, 8, 16, 32, 64 })
        {
            var analyser = new SpectrumAnalyzer(count, SampleRate);

            Assert.That(analyser.Analyze(Sine(440.0)).Length, Is.EqualTo(count));
        }
    }

    private static double Max(double[] values)
    {
        var max = values[0];

        foreach (var value in values)
        {
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }
}
