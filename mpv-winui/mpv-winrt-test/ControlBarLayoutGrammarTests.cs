using mpv_winui.Modules.Player;

namespace mpv_winrt_test
{
    /// <summary>
    /// Tests for the control-bar layout grammar. The class under test is the
    /// app's own source file, compiled into this assembly via a linked
    /// Compile item (see mpv-winrt-test.csproj) - no WinUI runtime involved.
    /// </summary>
    [TestFixture]
    public class ControlBarLayoutGrammarTests
    {
        // ----- Normalize -----

        [TestCase("modernx", "modernx")]
        [TestCase("center", "modernx")]
        [TestCase("right", "modernx")]
        [TestCase("classic", "classic")]
        [TestCase("", "classic")]
        [TestCase("ModernX", "classic")] // switch is ordinal: casing falls back
        public void NormalizeCollapsesLayoutAliases(string input, string expected)
        {
            Assert.That(ControlBarLayoutGrammar.Normalize(input), Is.EqualTo(expected));
        }

        [Test]
        public void NormalizeTreatsNullAsClassic()
        {
            Assert.That(ControlBarLayoutGrammar.Normalize(null), Is.EqualTo("classic"));
        }

        // ----- ParseZones -----

        [Test]
        public void ParseZonesReadsIdZonePairs()
        {
            var zones = ControlBarLayoutGrammar.ParseZones("volume:0,pip:2");
            Assert.That(zones, Does.ContainKey("volume").WithValue(0));
            Assert.That(zones, Does.ContainKey("pip").WithValue(2));
        }

        [Test]
        public void ParseZonesKeysAreCaseInsensitive()
        {
            var zones = ControlBarLayoutGrammar.ParseZones("VOLUME:2");
            Assert.That(zones.ContainsKey("volume"), Is.True);
            Assert.That(zones["volume"], Is.EqualTo(2));
        }

        [Test]
        public void ParseZonesRejectsTransportZoneOne()
        {
            // zone 1 is the fixed transport group and can never be assigned
            Assert.That(ControlBarLayoutGrammar.ParseZones("volume:1"), Is.Empty);
        }

        [Test]
        public void ParseZonesSkipsMalformedTokens()
        {
            var zones = ControlBarLayoutGrammar.ParseZones(",volume:,aspect:nope,:0,pip:2,x");
            Assert.That(zones.Keys, Is.EquivalentTo(new[] { "pip" }));
        }

        [Test]
        public void ParseZonesHandlesNullAndEmpty()
        {
            Assert.That(ControlBarLayoutGrammar.ParseZones(null), Is.Empty);
            Assert.That(ControlBarLayoutGrammar.ParseZones(string.Empty), Is.Empty);
        }

        [Test]
        public void ParseZonesLastAssignmentWins()
        {
            var zones = ControlBarLayoutGrammar.ParseZones("volume:0,volume:2");
            Assert.That(zones["volume"], Is.EqualTo(2));
        }

        // ----- ParseCustomOrder -----

        [Test]
        public void ParseCustomOrderKeepsAllowedIdsInOrder()
        {
            var order = ControlBarLayoutGrammar.ParseCustomOrder("random, aspect ,volume");
            Assert.That(order, Is.EqualTo(new[] { "random", "aspect", "volume" }));
        }

        [Test]
        public void ParseCustomOrderDropsUnknownAndDuplicateFreeEntries()
        {
            var order = ControlBarLayoutGrammar.ParseCustomOrder("bogus,,tracks,random");
            Assert.That(order, Is.EqualTo(new[] { "tracks", "random" }));
        }

        [Test]
        public void ParseCustomOrderMatchingIsCaseInsensitive()
        {
            var order = ControlBarLayoutGrammar.ParseCustomOrder("FullScreen,PiP");
            Assert.That(order, Is.EqualTo(new[] { "FullScreen", "PiP" }));
        }

        [Test]
        public void ParseCustomOrderHandlesNull()
        {
            Assert.That(ControlBarLayoutGrammar.ParseCustomOrder(null), Is.Empty);
        }

        // ----- ParseHiddenIcons -----

        [Test]
        public void ParseHiddenIconsAcceptsCommaAndSemicolonSeparators()
        {
            var hidden = ControlBarLayoutGrammar.ParseHiddenIcons("tracks , random;aspect");
            Assert.That(hidden, Does.Contain("tracks"));
            Assert.That(hidden, Does.Contain("random"));
            Assert.That(hidden, Does.Contain("aspect"));
            Assert.That(hidden.Count, Is.EqualTo(3));
        }

        [Test]
        public void ParseHiddenIconsIsCaseInsensitive()
        {
            Assert.That(ControlBarLayoutGrammar.ParseHiddenIcons("TRACKS").Contains("tracks"), Is.True);
        }

        [Test]
        public void ParseHiddenIconsHandlesNullAndEmpty()
        {
            Assert.That(ControlBarLayoutGrammar.ParseHiddenIcons(null), Is.Empty);
            Assert.That(ControlBarLayoutGrammar.ParseHiddenIcons("  "), Is.Empty);
        }
    }
}
