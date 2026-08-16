using mpv_winrt;

namespace mpv_winrt_test
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class Tests
    {
        [Test]
        public void MpvInitialize()
        {
            var volume = 30;
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, volume, DisplayColorKind.SDR, 60);

            Assert.That(mpvPlayer.Volume(), Is.EqualTo(volume));
        }

        [Test]
        public async Task ObservePropertyFiresOnChange()
        {
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

            var tcs = new TaskCompletionSource<(string Name, string Value)>();
            mpvPlayer.PropertyChanged += (name, value) =>
            {
                // Filter out the initial observation snapshot (30) and wait for
                // the value we set below.
                if (name == "volume" && value.Contains("55"))
                {
                    tcs.TrySetResult((name, value));
                }
            };

            mpvPlayer.ObserveProperty("volume");
            // Give the observation time to register before changing the value.
            await Task.Delay(200);
            mpvPlayer.Volume(55);

            var timeout = Task.Delay(5000);
            var completed = await Task.WhenAny(tcs.Task, timeout);
            Assert.That(completed, Is.SameAs(tcs.Task), "PropertyChanged never fired for volume=55");

            var (name, value) = await tcs.Task;
            Assert.That(name, Is.EqualTo("volume"));
            Assert.That(value, Does.Contain("55"));
        }

        [Test]
        public async Task ApplyCommandStringsRunsInOrderAndToleratesErrors()
        {
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

            // An invalid option in the middle must not abort the batch; the
            // final volume proves ordering was preserved.
            mpvPlayer.ApplyCommandStrings(
                new List<string> { "set volume 42", "set no-such-option x", "set volume 43" });
            await Task.Delay(500);

            Assert.That(mpvPlayer.Volume(), Is.EqualTo(43.0).Within(0.5));
        }

        [Test]
        public async Task DisplayRefreshRateIsAppliedAtStartupOnly()
        {
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

            var tcs = new TaskCompletionSource<string>();
            mpvPlayer.PropertyChanged += (name, value) =>
            {
                if (name == "override-display-fps")
                {
                    tcs.TrySetResult(value);
                }
            };

            // override-display-fps is startup-only in this mpv build; the
            // initial snapshot must reflect the value passed to Initialize.
            mpvPlayer.ObserveProperty("override-display-fps");
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.That(completed, Is.SameAs(tcs.Task), "override-display-fps initial snapshot not observed");
            Assert.That(await tcs.Task, Does.Contain("60"));

            // Runtime updates must not attempt the non-settable option; they
            // only refresh the user-data property and must not throw.
            mpvPlayer.UpdateDisplayRefreshRate(120);
            await Task.Delay(300);
            mpvPlayer.SetLogLevel("no");
        }

        [Test]
        public async Task TimePosObservationRaisesPositionChanged()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "mpv-winrt-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // A tiny PCM WAV keeps the test self-contained (no network,
                // no lavfi protocol in this build); ao=null avoids needing an
                // audio device in the test environment.
                File.WriteAllText(Path.Combine(dir, "mpv.conf"), "ao=null\n");
                var wav = CreateToneWav(Path.Combine(dir, "tone.wav"));

                MpvPlayer mpvPlayer = new();
                mpvPlayer.Initialize(dir, 1, 1, 30, DisplayColorKind.SDR, 60);

                var tcs = new TaskCompletionSource<double>();
                mpvPlayer.PositionChanged += args =>
                {
                    if (args.Position > 0.01)
                    {
                        tcs.TrySetResult(args.Position);
                    }
                };

                mpvPlayer.LoadFile(wav, 0);
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));
                Assert.That(completed, Is.SameAs(tcs.Task), "PositionChanged never fired for a playing WAV");
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                }
            }
        }

        private static string CreateToneWav(string path)
        {
            const int sampleRate = 8000;
            const int seconds = 2;
            var sampleCount = sampleRate * seconds;
            var dataSize = sampleCount * 2;

            using var fs = File.Create(path);
            using var writer = new BinaryWriter(fs);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 5000);
                writer.Write(sample);
            }
            return path;
        }

        [Test]
        public void SetLogLevelDoesNotThrow()
        {
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

            mpvPlayer.SetLogLevel("warn");
            mpvPlayer.SetLogLevel("no");
        }
    }
}
