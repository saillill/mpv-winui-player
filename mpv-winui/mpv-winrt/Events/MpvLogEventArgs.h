#pragma once
#include "MpvLogEventArgs.g.h"

namespace winrt::mpv_winrt::implementation
{
    struct MpvLogEventArgs : MpvLogEventArgsT<MpvLogEventArgs>
    {
        MpvLogEventArgs(hstring const& level, hstring const& prefix, hstring const& text)
            : m_level(level), m_prefix(prefix), m_text(text)
        {
        }

        hstring Level()
        {
            return m_level;
        }

        hstring Prefix()
        {
            return m_prefix;
        }

        hstring Text()
        {
            return m_text;
        }

    private:
        hstring m_level;
        hstring m_prefix;
        hstring m_text;
    };
}
