using mpv_winui.Modules.Settings;

namespace mpv_conf_test;

/// <summary>A stand-in for the enum-typed settings the reader must stay total for.</summary>
public enum SampleChoice
{
    Off,
    Auto,
    On,
}

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
    // Newlines: mpv allows them in a double-quoted value only as \n, and a value
    // is written one per physical line, so an unescaped one would split the line
    // on the next read.
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    [TestCase("line one\nline two")]
    [TestCase("a\nb\r\nc\rd")]
    [TestCase("trailing newline\n")]
    [TestCase("\nleading newline")]
    [TestCase("mix\\nliteral\nreal")]
    public void RoundTripReturnsTheOriginalValue(string original)
    {
        Assert.That(MpvConfValueCodec.Unquote(MpvConfValueCodec.Quote(original)),
            Is.EqualTo(original));
    }

    [Test]
    public void QuoteEscapesNewlinesSoAValueStaysOnOneLine()
    {
        var quoted = MpvConfValueCodec.Quote("line one\nline two\r\nthree");

        Assert.That(quoted, Does.Not.Contain('\n'));
        Assert.That(quoted, Does.Not.Contain('\r'));
        Assert.That(quoted, Is.EqualTo("\"line one\\nline two\\r\\nthree\""));
    }

    [Test]
    public void UnquoteDecodesTheNewlineEscapesQuoteEmits()
    {
        Assert.That(MpvConfValueCodec.Unquote("\"a\\nb\""), Is.EqualTo("a\nb"));
        Assert.That(MpvConfValueCodec.Unquote("\"a\\rb\""), Is.EqualTo("a\rb"));
        Assert.That(MpvConfValueCodec.Unquote("\"a\\r\\nb\""), Is.EqualTo("a\r\nb"));
    }

    [Test]
    public void UnquoteLeavesUnrecognisedEscapesAlone()
    {
        // mpv's parser is the authority on the file. An escape this decoder does
        // not know must survive verbatim rather than be guessed at, or a
        // hand-written value would be corrupted on read.
        Assert.That(MpvConfValueCodec.Unquote("\"a\\tb\""), Is.EqualTo("a\\tb"));
        Assert.That(MpvConfValueCodec.Unquote("\"a\\qb\""), Is.EqualTo("a\\qb"));
    }

    [Test]
    public void RoundTripSurvivesRepeatedPassesOverHostileCharacters()
    {
        // Exhaustive over the characters that actually appear in the failure:
        // a backslash, a quote, the separators binding them together, and the
        // pair of line breaks the line-oriented writer must keep escaped.
        char[] alphabet = ['a', '\\', '"', ' ', '=', '#', '/', ';', '\n', '\r'];
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

    [TestCase("wasapi/device")]
    [TestCase("line one\nline two")]
    [TestCase("a\r\nb")]
    [TestCase("quote\"and\\slash\nand newline")]
    public void RepeatedWriteReadDoesNotGrowTheLineForHostileValues(string original)
    {
        // The newline escapes go through the same doubling risk as the
        // backslashes did: \n -> \\n -> \\\\n if the inverse is not symmetric.
        var value = original;
        var lengths = new List<int>();
        for (var launch = 0; launch < 12; launch++)
        {
            var line = MpvConfValueCodec.Quote(value);
            lengths.Add(line.Length);
            value = MpvConfValueCodec.Unquote(line);
        }

        Assert.That(lengths.Distinct().Count(), Is.EqualTo(1),
            $"line length must be constant across launches, got [{string.Join(", ", lengths)}]");
        Assert.That(value, Is.EqualTo(original));
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

    // The *type* of the parsed value, not just its number.
    //
    // Every assertion above uses Is.EqualTo, and NUnit compares numbers across
    // their types: a boxed Int64 130 is "equal to" the int 130, so a reader that
    // returned the wrong width passed every one of them. That is exactly the
    // hole an AppSettings.Int32 property fell through - TryWriteSetting hands
    // the parsed object to PropertyInfo.SetValue, which does NOT coerce, and
    // `Object of type 'System.Int64' cannot be converted to type
    // 'System.Int32'` is what the log recorded. These cases pin the runtime
    // type so a widening regression cannot hide behind value equality again.
    [TestCase(typeof(int), "130", typeof(int))]
    [TestCase(typeof(int), "  42  ", typeof(int))]
    [TestCase(typeof(int), "-1", typeof(int))]
    [TestCase(typeof(int), "inf", typeof(int))]
    [TestCase(typeof(int), "1024MiB", typeof(int))]
    [TestCase(typeof(long), "130", typeof(long))]
    [TestCase(typeof(long), "inf", typeof(long))]
    [TestCase(typeof(double), "1.5", typeof(double))]
    [TestCase(typeof(double), "no", typeof(double))]
    [TestCase(typeof(float), "1.5", typeof(float))]
    [TestCase(typeof(bool), "yes", typeof(bool))]
    [TestCase(typeof(string), "\"x\"", typeof(string))]
    [TestCase(typeof(SampleChoice), "Auto", typeof(SampleChoice))]
    [TestCase(typeof(SampleChoice), "on", typeof(SampleChoice))]
    public void ParsedValueCarriesTheRequestedTypeNotAWiderOne(Type type, string text, Type expected)
    {
        var value = MpvConfValueCodec.ParseMpvValue(type, text);

        Assert.That(value, Is.Not.Null, $"ParseMpvValue({type.Name}, \"{text}\") returned null");
        Assert.That(value!.GetType(), Is.EqualTo(expected),
            $"ParseMpvValue({type.Name}, \"{text}\") returned {value.GetType().Name}; SetValue would reject it");
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

/// <summary>
/// Where a value ends, as opposed to what it looks like. The counterpart to the
/// quoting pair above: <c>Quote</c> decides what goes into mpv.conf,
/// <c>StripComment</c> decides where the value stops.
///
/// <para>Regression guard for a defect that was not caught because this file did
/// not exist. <c>StripComment</c> scanned for the first <c>#</c> with no regard
/// for quotes, on the stated assumption that no written value is "a quoted
/// string containing '#'". That assumption is false for exactly the settings
/// users notice: colour values are quoted and start with a hash, and the default
/// <c>osd-back-color</c> is <c>#AF000000</c>. The line on disk reads
/// <c>osd-back-color="#AF000000"</c>, the scan cut it at the hash, the fragment
/// did not parse, and the setting reverted to its default on the next launch
/// with nothing logged.</para>
///
/// <para>The round-trip case is the one that generalises: whatever
/// <c>Quote</c> produces, <c>StripComment</c> must not shorten. That is the
/// assertion that fails on the old implementation for every value containing a
/// hash.</para>
/// </summary>
[TestFixture]
public class MpvConfValueCodecStripCommentTests
{
    [TestCase("volume=100", "volume=100")]
    [TestCase("volume=100  # loud", "volume=100")]
    [TestCase("volume=100# loud", "volume=100")]
    [TestCase("volume=100   ", "volume=100")]
    [TestCase("# whole line", "")]
    [TestCase("   # an indented comment", "")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    public void StripsOnlyRealComments(string input, string expected)
    {
        Assert.That(MpvConfValueCodec.StripComment(input), Is.EqualTo(expected));
    }

    [TestCase("osd-back-color=\"#AF000000\"", "osd-back-color=\"#AF000000\"")]
    [TestCase("osd-back-color=\"#AF000000\"  # OSD background", "osd-back-color=\"#AF000000\"")]
    [TestCase("screenshot-template=\"shot-%#n\"", "screenshot-template=\"shot-%#n\"")]
    [TestCase("sub-file=\"C:\\\\dir#1\\\\a.srt\"", "sub-file=\"C:\\\\dir#1\\\\a.srt\"")]
    [TestCase("-x=\"a#b\" # trailing", "-x=\"a#b\"")]
    public void KeepsHashInsideQuotes(string input, string expected)
    {
        Assert.That(MpvConfValueCodec.StripComment(input), Is.EqualTo(expected));
    }

    [TestCase("#AF000000")]
    [TestCase("#00000000")]
    [TestCase("a#b")]
    [TestCase("shot-%#n")]
    [TestCase("C:\\dir#1\\a.srt")]
    public void AQuotedValueSurvivesTheReadPath(string value)
    {
        var quoted = MpvConfValueCodec.Quote(value);

        foreach (var line in new[] { $"option={quoted}", $"option={quoted}   # note" })
        {
            var body = MpvConfValueCodec.StripComment(line);
            var equals = body.IndexOf('=');
            Assert.That(equals, Is.GreaterThanOrEqualTo(0), line);

            var raw = body[(equals + 1)..].Trim();
            Assert.That(MpvConfValueCodec.ParseMpvValue(typeof(string), raw),
                Is.EqualTo(value), $"round trip through '{line}'");
        }
    }

    [Test]
    public void EscapedQuoteDoesNotEndTheQuotedRun()
    {
        // Quote() escapes '"' as '\"', so a value that itself contains a quote
        // produces a line whose quoted run the scanner has to follow correctly.
        var value = "say \"#hi\"";
        var line = $"option={MpvConfValueCodec.Quote(value)}  # note";

        var body = MpvConfValueCodec.StripComment(line);
        var raw = body[(body.IndexOf('=') + 1)..].Trim();

        Assert.That(MpvConfValueCodec.ParseMpvValue(typeof(string), raw), Is.EqualTo(value));
    }
}
