using mpv_winui.Modules.Settings;

namespace mpv_conf_test;

/// <summary>
/// The escaping pair has to be a bijection, and this fixture is the assertion
/// that keeps it one.
///
/// <para>Regression guard for a defect that shipped: <c>MpvSettings</c> escaped
/// a value on the way into mpv.conf and stored the escaped text verbatim on the
/// way back, so every launch escaped it again. A single <c>audio-device</c>
/// value took a real <c>mpv.conf</c> to 896 MB with 884 newlines, and the read
/// path began throwing <c>System.OutOfMemoryException</c> at startup. Nothing in
/// the checker suite noticed, because every checker works on static source and
/// none of them wrote a value out and read it back.</para>
///
/// <para>The growth test below is the one that matters: it is written as
/// "length must be constant across repeated round trips", which fails on the
/// doubling behaviour directly.</para>
/// </summary>
[TestFixture]
public class MpvConfValueCodecTests
{
    [TestCase("auto")]
    [TestCase("wasapi/{0.0.0.00000000}.{e0f1a2b3-4567-89ab-cdef-0123456789ab}")]
    [TestCase(@"C:\Users\z2637\Music")]
    [TestCase(@"C:\Program Files\mpv")]
    [TestCase("a=b")]
    [TestCase("")]
    [TestCase("no")]
    [TestCase("inf")]
    [TestCase("1024MiB")]
    [TestCase("say \"hello\"")]
    [TestCase("trailing\\")]
    [TestCase("odd\\\"mix")]
    [TestCase("\\")]
    [TestCase("\"")]
    [TestCase("\\\\\"\\\"")]
    public void RoundTripReturnsTheOriginalValue(string original)
    {
        Assert.That(MpvConfValueCodec.Unquote(MpvConfValueCodec.Quote(original)),
            Is.EqualTo(original));
    }

    [Test]
    public void RoundTripSurvivesRepeatedPassesOverHostileCharacters()
    {
        // Exhaustive over the characters that actually appear in the failure:
        // a backslash, a quote, and the separators binding them together.
        char[] alphabet = ['a', '\\', '"', ' ', '=', '#', '/', ';'];
        var value = new System.Text.StringBuilder();
        var checkedCount = 0;

        void Recurse(int depth)
        {
            if (depth > 0)
            {
                var text = value.ToString();
                Assert.That(MpvConfValueCodec.Unquote(MpvConfValueCodec.Quote(text)),
                    Is.EqualTo(text), $"round trip failed for [{text}]");
                checkedCount++;
            }

            if (depth == 3)
            {
                return;
            }

            foreach (var c in alphabet)
            {
                value.Append(c);
                Recurse(depth + 1);
                value.Length--;
            }
        }

        Recurse(0);
        Assert.That(checkedCount, Is.GreaterThan(500), "the alphabet sweep did not run");
    }

    [Test]
    public void QuoteEscapesBothBackslashAndQuote()
    {
        Assert.That(MpvConfValueCodec.Quote(@"C:\a"), Is.EqualTo(@"""C:\\a"""));
        Assert.That(MpvConfValueCodec.Quote("a\"b"), Is.EqualTo(@"""a\""b"""));
    }

    [Test]
    public void UnquoteLeavesAnUnquotedValueAlone()
    {
        // The shipped mpv.conf body carries bare values; they must not be mangled
        // just because the decoder exists.
        Assert.That(MpvConfValueCodec.Unquote("auto"), Is.EqualTo("auto"));
        Assert.That(MpvConfValueCodec.Unquote("C:\\a"), Is.EqualTo("C:\\a"));
        Assert.That(MpvConfValueCodec.Unquote(""), Is.EqualTo(""));
        Assert.That(MpvConfValueCodec.Unquote("\""), Is.EqualTo("\""));
    }

    [Test]
    public void AnUnescapedBackslashIsNotEaten()
    {
        // `\a` is not an escape this grammar defines. Dropping the backslash
        // would corrupt a Windows path that arrived without escaping.
        Assert.That(MpvConfValueCodec.Unquote(@"C:\Users"), Is.EqualTo(@"C:\Users"));
        Assert.That(MpvConfValueCodec.Unquote(@"""C:\Users"""), Is.EqualTo(@"C:\Users"));
    }

    [Test]
    public void RepeatedWriteReadDoesNotGrowTheLine()
    {
        // The A-26 assertion. With the missing inverse this produced
        // 15, 30, 60, 120 ... until the file was hundreds of megabytes.
        var value = "wasapi/device";
        var lengths = new List<int>();
        for (var launch = 0; launch < 12; launch++)
        {
            var line = MpvConfValueCodec.Quote(value);
            lengths.Add(line.Length);
            value = MpvConfValueCodec.Unquote(line);
        }

        Assert.That(lengths.Distinct().Count(), Is.EqualTo(1),
            $"line length must be constant across launches, got [{string.Join(", ", lengths)}]");
        Assert.That(value, Is.EqualTo("wasapi/device"));
    }

    // --- reading a value back -------------------------------------------------
    //
    // These are the EXACT values the app wrote and then could not read. The live
    // log recorded 60 failures across these six settings; each one silently
    // reverted a user's value in the UI on the next launch, because the reader
    // used plain type parsing against text the writer had encoded.

    [TestCase(typeof(double), "no", 0.0d)]        // AbLoopA
    [TestCase(typeof(double), "no", 0.0d)]        // AbLoopB
    [TestCase(typeof(int), "inf", -1)]            // AbLoopCount
    [TestCase(typeof(bool), "inf", true)]         // LoopFile
    [TestCase(typeof(int), "1024MiB", 1024)]      // DemuxerMaxBytes
    [TestCase(typeof(int), "512MiB", 512)]        // DemuxerMaxBackBytes
    public void ReadsBackTheValuesTheAppItselfWrote(Type type, string text, object expected)
    {
        Assert.That(MpvConfValueCodec.ParseMpvValue(type, text), Is.EqualTo(expected),
            $"should read back from '{text}'");
    }

    // Values that already worked must keep working.
    [TestCase(typeof(string), "\"auto-safe\"", "auto-safe")]
    [TestCase(typeof(string), "C:\\\\path", "C:\\path")]
    [TestCase(typeof(int), "130", 130)]
    [TestCase(typeof(int), "  42  ", 42)]
    [TestCase(typeof(double), "1.5", 1.5)]
    [TestCase(typeof(bool), "yes", true)]
    [TestCase(typeof(bool), "no", false)]
    [TestCase(typeof(bool), "true", true)]
    public void ReadsBackValuesThatWereNeverBroken(Type type, string text, object expected)
    {
        Assert.That(MpvConfValueCodec.ParseMpvValue(type, text), Is.EqualTo(expected));
    }

    [Test]
    public void APlusPrefixIsMpvsRelativeFormNotPartOfTheNumber()
    {
        // `volume=+5` means "add five", not "the number +5". Stripping the sign
        // is the reading that does not corrupt a hand-edited file.
        Assert.That(MpvConfValueCodec.ParseMpvValue(typeof(double), "+0.5"), Is.EqualTo(0.5d));
    }

    [TestCase(typeof(int), "1GiB")]      // a unit the encoder never emits
    [TestCase(typeof(double), "maybe")]
    [TestCase(typeof(bool), "perhaps")]
    public void UnrepresentableValuesAreRefusedRatherThanGuessed(Type type, string text)
    {
        // Guessing a size the user wrote exactly would be worse than leaving the
        // stored setting alone, which is what null makes the caller do.
        Assert.That(MpvConfValueCodec.ParseMpvValue(type, text), Is.Null);
    }

    [Test]
    public void ParseIsSymmetricWithWhatTheWriterEmits()
    {
        // The general form of the defect: for every (type, value) pair, encoding
        // the value must produce text the reader can decode back to it. This is
        // the assertion that would have caught the sentinel and unit encodings.
        Assert.Multiple(() =>
        {
            foreach (var (type, stored, encoded) in new (Type, object, string)[]
                     {
                         (typeof(double), 0.0d, "no"),
                         (typeof(double), 42.5d, "42.5"),
                         (typeof(int), -1, "inf"),
                         (typeof(int), 7, "7"),
                         (typeof(int), 1024, "1024MiB"),
                         (typeof(bool), true, "inf"),
                         (typeof(bool), true, "yes"),
                         (typeof(bool), false, "no"),
                         (typeof(string), "auto", "\"auto\""),
                     })
            {
                Assert.That(MpvConfValueCodec.ParseMpvValue(type, encoded),
                    Is.EqualTo(stored), $"{type.Name} from '{encoded}'");
            }
        });
    }
}
