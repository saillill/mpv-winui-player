#pragma once
#include "MediaInfoChangedEventArgs.g.h"

namespace winrt::mpv_winrt::implementation
{
    struct MediaInfoChangedEventArgs : MediaInfoChangedEventArgsT<MediaInfoChangedEventArgs>
    {
        MediaInfoChangedEventArgs(hstring const& filename, hstring const& title, float videoWidth, float videoHeight)
            : m_filename(filename), m_title(title), m_videoWidth(videoWidth), m_videoHeight(videoHeight)
        {
        }

        hstring Filename()
        {
            return m_filename;
        }
        hstring MediaTitle()
        {
            return m_title;
        }
        float VideoWidth()
        {
            return m_videoWidth;
        }
        float VideoHeight()
        {
            return m_videoHeight;
        }

    private:
        hstring m_filename;
        hstring m_title;
        float m_videoWidth;
        float m_videoHeight;
    };
}
