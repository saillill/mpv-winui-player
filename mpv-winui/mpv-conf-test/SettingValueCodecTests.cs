using mpv_winui.Modules.Settings;
using NUnit.Framework;

namespace mpv_conf_test
{
    /// <summary>
    /// Pins <see cref="SettingValueCodec"/> — the single place a stored setting
    /// value becomes the typed value the app reads.
    ///
    /// <para>**The defect these tests exist for.** `EnableDebugLog` was stored as
    /// `REG_DWORD = 1`. `ApplicationDataContainer` surfaces that as an unsigned
    /// integral, but the bool arm of `GetValue` recognised `int` only, so the
    /// read fell through to `false`. The logger therefore stayed at WARN while
    /// the registry and the settings window both said "enabled": two days of app
    /// logs contained zero `|DEBUG|` and zero `|INFO|` lines. Writing the same
    /// value as the string `"true"` made DEBUG lines appear at once — that
    /// asymmetry is what identified the typed arm, not the storage.</para>
    ///
    /// <para>**Before the fix, the uint case below returns False.** Reverting the
    /// `uint` arm in `SettingValueCodec.ToBool` turns this suite red, which is
    /// the only thing that makes it evidence rather than decoration.</para>
    /// </summary>
    [TestFixture]
    public class SettingValueCodecTests
    {
        [Test]
        public void ADwordOneReadsBackAsTrue()
        {
            // The shape ApplicationDataContainer actually hands back for REG_DWORD 1.
            Assert.That(SettingValueCodec.Coerce((uint)1, false), Is.True);
        }

        [Test]
        public void ADwordZeroReadsBackAsFalse()
        {
            Assert.That(SettingValueCodec.Coerce((uint)0, true), Is.False);
        }

        [TestCase(typeof(sbyte))]
        [TestCase(typeof(byte))]
        [TestCase(typeof(short))]
        [TestCase(typeof(ushort))]
        [TestCase(typeof(int))]
        [TestCase(typeof(uint))]
        [TestCase(typeof(long))]
        [TestCase(typeof(ulong))]
        public void EveryIntegralWidthThatCouldHoldADwordWorks(System.Type type)
        {
            // Not decoration: each of these is a legal shape for an imported or
            // legacy value, and the original defect was exactly "one width was
            // handled and the others were not".
            foreach (var raw in new object[] { (sbyte)1, (byte)1, (short)1, (ushort)1,
                                              1, 1u, 1L, 1UL })
            {
                if (raw.GetType() != type)
                {
                    continue;
                }

                Assert.That(SettingValueCodec.Coerce(raw, false), Is.True,
                    "truthy " + type.Name + " must read as true");
                Assert.That(SettingValueCodec.Coerce(System.Convert.ChangeType(0, type),
                        true), Is.False, "zero " + type.Name + " must read as false");
            }
        }

        [Test]
        public void TheStringFormTheAppItselfWritesAlsoWorks()
        {
            // SetValue<bool> stores "true"/"false", so both spellings must round-trip.
            Assert.That(SettingValueCodec.Coerce("true", false), Is.True);
            Assert.That(SettingValueCodec.Coerce("false", true), Is.False);
        }

        [Test]
        public void AHandEditedOneOrZeroAlsoWorks()
        {
            Assert.That(SettingValueCodec.Coerce("1", false), Is.True);
            Assert.That(SettingValueCodec.Coerce("0", true), Is.False);
        }

        [Test]
        public void ARealBoolStillWorks()
        {
            Assert.That(SettingValueCodec.Coerce(true, false), Is.True);
            Assert.That(SettingValueCodec.Coerce(false, true), Is.False);
        }

        [Test]
        public void UnreadableValuesFallBackInsteadOfThrowing()
        {
            // A read must never write and must never fail the app.
            Assert.That(SettingValueCodec.Coerce(null, true), Is.True);
            Assert.That(SettingValueCodec.Coerce("not a bool", true), Is.True);
            Assert.That(SettingValueCodec.Coerce(new object(), false), Is.False);
        }

        [Test]
        public void NumericSettingsStillConvertFromTheirStringForm()
        {
            // The other half of the old converter, kept so the extraction did not
            // silently drop it: imported configs store every value as a string.
            Assert.That(SettingValueCodec.Coerce("42", 0), Is.EqualTo(42));
            Assert.That(SettingValueCodec.Coerce("1.5", 0.0), Is.EqualTo(1.5));
            Assert.That(SettingValueCodec.Coerce("2.5", 0f), Is.EqualTo(2.5f));
            Assert.That(SettingValueCodec.Coerce("9000000000", 0L), Is.EqualTo(9000000000L));
            Assert.That(SettingValueCodec.Coerce("4000000000", 0u), Is.EqualTo(4000000000u));
            Assert.That(SettingValueCodec.Coerce("18446744073709551615", 0UL),
                Is.EqualTo(18446744073709551615UL));
        }

        [Test]
        public void NumericSettingsKeepTheirDefaultWhenTheStringIsNotANumber()
        {
            Assert.That(SettingValueCodec.Coerce("abc", 7), Is.EqualTo(7));
            Assert.That(SettingValueCodec.Coerce("", 7), Is.EqualTo(7));
        }

        [Test]
        public void StringsPassThroughUnchanged()
        {
            Assert.That(SettingValueCodec.Coerce("zh-CN", "en-US"), Is.EqualTo("zh-CN"));
            Assert.That(SettingValueCodec.Coerce("", "en-US"), Is.EqualTo(""));
        }
    }
}
