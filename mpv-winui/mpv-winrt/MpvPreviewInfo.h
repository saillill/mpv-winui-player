#pragma once
#include "MpvPreviewInfo.g.h"

namespace winrt::mpv_winrt::implementation
{
    struct MpvPreviewInfo : MpvPreviewInfoT<MpvPreviewInfo>
    {
        MpvPreviewInfo(int32_t width, int32_t height, hstring const& path)
            : m_width(width), m_height(height), m_path(path)
        {
        }

        int32_t Width()
        {
            return m_width;
        }
        int32_t Height()
        {
            return m_height;
        }
        hstring Path()
        {
            return m_path;
        }

    private:
        int32_t m_width;
        int32_t m_height;
        hstring m_path;
    };
}
