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
    }
}
