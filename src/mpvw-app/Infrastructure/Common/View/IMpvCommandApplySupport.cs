using System.Threading.Tasks;

namespace mpv_winui.Infrastructure.Common.View
{
    public interface IMpvCommandApplySupport
    {
        Task ApplyMpvCommandAsync(string command);
    }
}