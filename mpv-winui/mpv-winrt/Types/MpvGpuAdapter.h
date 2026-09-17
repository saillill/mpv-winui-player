#pragma once
#include "MpvGpuAdapter.g.h"

namespace winrt::mpv_winrt::implementation
{
    struct MpvGpuAdapter : MpvGpuAdapterT<MpvGpuAdapter>
    {
        MpvGpuAdapter(hstring const& name, hstring const& description)
            : m_name(name), m_description(description)
        {
        }

        hstring Name()
        {
            return m_name;
        }
        hstring Description()
        {
            return m_description;
        }

    private:
        hstring m_name;
        hstring m_description;
    };
}
