using System;
using mpv_winui.Domains.Playback.Audio;

namespace mpv_winui_test;

/// <summary>
/// The analyser's decay, which is the other number that decides whether a display
/// looks like music or like noise.
/// </summary>
[TestFixture]
public class SpectrumDecayTests
{
    private const double SampleRate = 44100.0;

    private static double PeakOf(double[] levels)
    {
        var peak = levels[0];

        foreach (var value in levels)
        {
            if (value > peak)
            {
                peak = value;
            }
        }

        return peak;
    }

    private static float[] Sine(double hz, int count = 1024)
    {
        var samples = new float[count];

        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * hz * i / SampleRate));
        }

        return samples;
    }

    [Test]
    public void TheFirstFrameStartsQuietAndRisesTowardsTheTone()
    {
        var analyser = new SpectrumAnalyzer(16, SampleRate);

        // Smoothing means the first frame is part-way to the tone, not at it. The
        // direction matters: a falling first frame is what a display looks like
        // when its initial state is the loudest value on the scale, which is the
        // bug this test found.
        var first = PeakOf(analyser.Analyze(Sine(1000.0), decay: 0.55));

        var settled = PeakOf(analyser.Analyze(Sine(1000.0), decay: 0.0));

        Assert.That(first, Is.GreaterThan(settled - 30.0),
            "the first frame must already be most of the way to the tone, "
            + "or the bars lag the beat by several frames");

        Assert.That(first, Is.LessThan(settled),
            "and it must not arrive instantly, which is what flickers");

        Assert.That(first, Is.GreaterThan(-60.0),
            "the first frame must not start at silence either - that is a bar "
            + "climbing from the floor on every track change");
    }

    [Test]
    public void AFreshAnalyserReportsSilenceBeforeItsFirstBlock()
    {
        // Reset() and the constructor must agree. They did not: the array was
        // allocated zeroed (0 dB, full scale) while Reset() set -60, so the two
        // starting points differed by the whole range.
        var analyser = new SpectrumAnalyzer(16, SampleRate);

        var silence = analyser.Analyze(new float[1024], decay: 0.0);

        foreach (var level in silence)
        {
            Assert.That(level, Is.EqualTo(-60.0).Within(0.5),
                "a fresh analyser fed silence must report the floor, not full scale");
        }
    }

    [Test]
    public void TheSameInputTwiceIsStable()
    {
        // Steady tone, steady bars: no drift, which is what a decaying average gives
        // and a max() would not.
        var analyser = new SpectrumAnalyzer(16, SampleRate);

        for (var i = 0; i < 30; i++)
        {
            analyser.Analyze(Sine(1000.0));
        }

        var a = analyser.Analyze(Sine(1000.0));
        var b = analyser.Analyze(Sine(1000.0));

        for (var i = 0; i < a.Length; i++)
        {
            Assert.That(b[i], Is.EqualTo(a[i]).Within(0.5));
        }
    }
}
