using mpv_winui.Platform.Activation;

namespace mpv_conf_test;

/// <summary>
/// The <c>mpvw://</c> contract, asserted directly against the source the app
/// ships.
///
/// <para>This fixture exists because the scheme and the parser had drifted apart
/// and nothing could see it. Four places wrote the scheme - a constant in the
/// protocol association control, a constant in the registration service, and two
/// string literals in <c>SettingsPage.Actions</c> - and the parser accepted a
/// fifth value, <c>mpv-winui:</c>. The packaged activation branch compared
/// against yet another literal. The net effect was that a registration could
/// succeed and hand the app a URI it then ignored, silently.</para>
///
/// <para>The two tests that earn their keep are
/// <see cref="TheRetiredSchemeIsNotAccepted"/> - the exact regression, and the
/// reason the old behaviour cannot come back by accident - and
/// <see cref="BothEntryPointsAgree"/>, because the packaged and unpackaged paths
/// receive the same URI through different doors and must not disagree about
/// it.</para>
/// </summary>
[TestFixture]
public class MpvProtocolUriTests
{
    [Test]
    public void TheSchemeIsMpvw()
    {
        Assert.That(MpvProtocolUri.Scheme, Is.EqualTo("mpvw"));
    }

    // ----- the unpackaged path: a raw command-line argument -----

    [Test]
    public void ParsesAFilePathFromAnArgument()
    {
        Assert.That(MpvProtocolUri.TryParseArgument("mpvw://?file=C%3A%5Cvideo%5Ca.mkv", out var path), Is.True);
        Assert.That(path, Is.EqualTo(@"C:\video\a.mkv"));
    }

    [Test]
    public void ArgumentSchemeIsCaseInsensitive()
    {
        foreach (var arg in new[] { "MPVW://?file=x.mkv", "Mpvw://?file=x.mkv", "mpvw://?file=x.mkv" })
        {
            Assert.That(MpvProtocolUri.TryParseArgument(arg, out var path), Is.True, arg);
            Assert.That(path, Is.EqualTo("x.mkv"), arg);
        }
    }

    [Test]
    public void RetiredSchemeIsNotAccepted()
    {
        // mpv-winui was the scheme before the unification. It must not be
        // accepted: accepting it silently would let two schemes live on, which is
        // what the unification removed.
        Assert.That(MpvProtocolUri.TryParseArgument("mpv-winui://?file=x.mkv", out var path), Is.False);
        Assert.That(path, Is.Null);
    }

    [TestCase("http://example.com/?file=x.mkv")]
    [TestCase("mpv://?file=x.mkv")]
    [TestCase("mpvwx://?file=x.mkv")]
    [TestCase(@"C:\video\a.mkv")]
    [TestCase("--fullscreen")]
    [TestCase("mpvw://?dir=x")]
    [TestCase("mpvw://?file=")]
    [TestCase("mpvw://")]
    [TestCase("mpvw:")]
    public void RejectsArgumentsThatAreNotThisProtocolWithAFile(string arg)
    {
        Assert.That(MpvProtocolUri.TryParseArgument(arg, out var path), Is.False, arg);
        Assert.That(path, Is.Null, arg);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void RejectsMissingArguments(string? arg)
    {
        Assert.That(MpvProtocolUri.TryParseArgument(arg, out var path), Is.False);
        Assert.That(path, Is.Null);
    }

    [Test]
    public void DecodesPercentEscapes()
    {
        Assert.That(MpvProtocolUri.TryParseArgument("mpvw://?file=C%3A%5CMy%20Films%5E%26b.mkv", out var path), Is.True);
        Assert.That(path, Is.EqualTo(@"C:\My Films^&b.mkv"));
    }

    [Test]
    public void DecodesNonAsciiPaths()
    {
        Assert.That(MpvProtocolUri.TryParseArgument("mpvw://?file=%E8%A7%86%E9%A2%91.mp4", out var path), Is.True);
        Assert.That(path, Is.EqualTo("视频.mp4"));
    }

    // ----- the packaged path: scheme and query arrive separately -----

    [Test]
    public void ParsesFromSplitSchemeAndQuery()
    {
        Assert.That(MpvProtocolUri.TryParseActivation("mpvw", "?file=C%3A%5Ca.mkv", out var path), Is.True);
        Assert.That(path, Is.EqualTo(@"C:\a.mkv"));
    }

    [Test]
    public void ActivationSchemeIsCaseInsensitive()
    {
        Assert.That(MpvProtocolUri.TryParseActivation("MPVW", "?file=x", out var path), Is.True);
        Assert.That(path, Is.EqualTo("x"));
    }

    [TestCase("mpv-winui")]
    [TestCase("mpv")]
    [TestCase("mpvwx")]
    [TestCase(null)]
    [TestCase("")]
    public void RejectsOtherSchemes(string? scheme)
    {
        Assert.That(MpvProtocolUri.TryParseActivation(scheme, "?file=x.mkv", out var path), Is.False, scheme);
        Assert.That(path, Is.Null, scheme);
    }

    [Test]
    public void BothEntryPointsAgree()
    {
        // The packaged and unpackaged paths are handed the same URI and reach the
        // parser through different doors - one gets the raw argument, the other a
        // WinRT Uri split into Scheme and Query. Deciding them differently is what
        // shipped once: the unpackaged parser accepted mpv-winui: while the
        // packaged branch compared against it and the manifest declared mpvw.
        var arguments = new[]
        {
            "mpvw://?file=C%3A%5Ca.mkv",
            "MPVW://?file=x.mp4",
            "mpvw://?file=%E8%A7%86%E9%A2%91.mp4",
            "mpvw://?file=",
            "mpvw://?dir=x",
            "mpvw://",
            "mpv-winui://?file=x.mkv",
            "http://example.com/?file=x.mkv",
            @"C:\plain\path.mkv",
        };

        foreach (var arg in arguments)
        {
            var fromArgument = MpvProtocolUri.TryParseArgument(arg, out var pathA);

            // What the WinRT side would hand over for the same string. Uri
            // accepts odd inputs, so a string that is not an absolute URI simply
            // has no WinRT counterpart to compare against.
            if (!Uri.TryCreate(arg, UriKind.Absolute, out var uri))
            {
                Assert.That(fromArgument, Is.False, $"'{arg}' should not parse as an argument");
                continue;
            }

            var fromActivation = MpvProtocolUri.TryParseActivation(uri.Scheme, uri.Query, out var pathB);

            Assert.That(fromActivation, Is.EqualTo(fromArgument), $"entry points disagree for '{arg}'");
            Assert.That(pathB, Is.EqualTo(pathA), $"paths disagree for '{arg}'");
        }
    }

    // ----- the shared query decoder -----

    [Test]
    public void QueryDecoderTakesOnlyTheFileParameter()
    {
        Assert.That(MpvProtocolUri.TryParseQuery("?file=x", out var path), Is.True);
        Assert.That(path, Is.EqualTo("x"));

        Assert.That(MpvProtocolUri.TryParseQuery("?dir=x", out _), Is.False);
        Assert.That(MpvProtocolUri.TryParseQuery("?file", out _), Is.False);
        Assert.That(MpvProtocolUri.TryParseQuery("file=x", out _), Is.False);
        Assert.That(MpvProtocolUri.TryParseQuery("", out _), Is.False);
        Assert.That(MpvProtocolUri.TryParseQuery(null, out _), Is.False);
    }

    [Test]
    public void AFileParameterWithNoValueIsNotAFile()
    {
        Assert.That(MpvProtocolUri.TryParseQuery("?file=", out var path), Is.False);
        Assert.That(path, Is.Null);
    }
}
