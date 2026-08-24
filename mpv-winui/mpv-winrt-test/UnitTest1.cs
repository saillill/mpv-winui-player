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

        [Test]
        public async Task SpeedChangedFiresOnPropertySet()
        {
            MpvPlayer mpvPlayer = new();
            mpvPlayer.Initialize("", 1, 1, 30, DisplayColorKind.SDR, 60);

            var tcs = new TaskCompletionSource<double>();
            mpvPlayer.SpeedChanged += args => tcs.TrySetResult(args.Speed);

            mpvPlayer.PlaybackSpeed(1.5);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.That(completed, Is.SameAs(tcs.Task), "SpeedChanged never fired for speed=1.5");
            Assert.That(await tcs.Task, Is.EqualTo(1.5).Within(0.01));
        }
    }
}
