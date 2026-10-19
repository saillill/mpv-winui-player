using mpv_winui.Modules.Language;

namespace mpv_conf_test;

/// <summary>
/// The language-code guard, asserted directly against the source the app ships.
///
/// <para>The code reaches <c>ResolveFilePath</c> from the persisted
/// <c>CurrentLanguage</c> setting and used to be concatenated into a file path
/// bare, so a value with a separator or <c>..</c> picked the file the app would
/// read. These cases are the ones that would have been accepted before.</para>
/// </summary>
[TestFixture]
public class LanguageCodeTests
{
    [TestCase("en-US")]
    [TestCase("zh-CN")]
    [TestCase("zh-Hans-CN")]
    [TestCase("en")]
    [TestCase("pt-BR")]
    [TestCase("sr-Latn-RS")]
    public void PlainLanguageTagsAreSafe(string code)
    {
        Assert.That(LanguageCode.IsSafe(code), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("..")]
    [TestCase("../evil")]
    [TestCase("..\\evil")]
    [TestCase("en/../../evil")]
    [TestCase("/etc/passwd")]
    [TestCase(@"C:\Windows\win")]
    [TestCase("en.json")]
    [TestCase("en US")]
    [TestCase("en:US")]
    [TestCase("en\0US")]
    [TestCase("en\tUS")]
    [TestCase("en\nUS")]
    public void AnythingThatIsNotALanguageTagIsRejected(string? code)
    {
        Assert.That(LanguageCode.IsSafe(code), Is.False, $"should have been rejected: [{code}]");
    }

    [Test]
    public void OverlongCodeIsRejected()
    {
        Assert.That(LanguageCode.IsSafe(new string('a', 65)), Is.False);
    }

    [Test]
    public void EveryAcceptedCodeStaysInsideItsDirectory()
    {
        // The property that actually matters: accepted codes must not be able to
        // name a path component that escapes the directory they are combined
        // with. Checking the resulting file name is the honest form of that.
        string[] probes =
        [
            "en-US", "zh-CN", "en", "zh-Hans-CN", "..", "../x", @"..\x", "a/b", @"a\b",
            "en.", ".en", "-en", "en-", "e n", "e\u0000n",
        ];

        foreach (var probe in probes)
        {
            if (!LanguageCode.IsSafe(probe))
            {
                continue;
            }

            var combined = System.IO.Path.Combine(System.IO.Path.GetTempPath(), probe + ".json");
            var root = System.IO.Path.GetTempPath();
            Assert.That(
                System.IO.Path.GetFullPath(combined).StartsWith(root, StringComparison.OrdinalIgnoreCase),
                Is.True,
                $"[{probe}] escaped the root");
        }
    }
}
