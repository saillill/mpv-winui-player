using System.Threading.Tasks;

namespace mpv_winui.Infrastructure.Common.View
{
    public interface IMpvOptionApplySupport
    {
        Task ApplyMpvOptionAsync(string key, string value);
    }
}
