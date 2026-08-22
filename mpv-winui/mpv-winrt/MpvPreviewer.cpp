#include "pch.h"
#include "MpvPreviewer.h"
#include "MpvPreviewer.g.cpp"
#include <winrt/Microsoft.UI.Dispatching.h>
#include <wil/cppwinrt_helpers.h>

namespace
{
    struct __declspec(uuid("905a0fef-bc53-11df-8c49-001e4fc686da")) IBufferByteAccess : ::IUnknown
    {
        virtual HRESULT __stdcall Buffer(BYTE** value) = 0;
    };
}

namespace winrt::mpv_winrt::implementation
{
    MpvPreviewer::MpvPreviewer()
    {
    }

    MpvPreviewer::~MpvPreviewer()
    {
        Destroy();
    }

    void MpvPreviewer::CreateContext()
    {
        m_mpv = mpv_create();
        if (!m_mpv)
        {
            throw hresult_error(E_FAIL, L"Failed to create mpv context");
        }
    }

    void MpvPreviewer::SetOption(std::string const& name, std::string const& value)
    {
        if (mpv_set_option_string(m_mpv, name.c_str(), value.c_str()) < 0)
        {
            throw hresult_error(E_FAIL, L"Failed to set mpv option");
        }
    }

    winrt::Windows::Foundation::IAsyncAction MpvPreviewer::Initialize(winrt::Microsoft::UI::Xaml::Controls::Image const& image, uint32_t width, uint32_t height)
    {
        m_image = image;
        m_dispatcher = m_image.DispatcherQueue();
        m_width = width;
        m_height = height;
        m_stride = static_cast<size_t>(width) * 4;
        m_size = m_stride * height;

        co_await winrt::resume_background();

        {
            std::lock_guard lifecycleLock(m_lifecycleMutex);
            if (m_destroyed)
            {
                co_return;
            }

            try
            {
                CreateContext();

                SetOption("vo", "libmpv");
                SetOption("config", "no");
                SetOption("msg-level", "all=no");
                SetOption("profile", "fast");
                SetOption("osc", "no");
                SetOption("load-scripts", "no");
                SetOption("idle", "yes");
                SetOption("keep-open", "yes");
                SetOption("pause", "yes");
                SetOption("sub", "no");
                SetOption("hr-seek", "no");
                SetOption("audio", "no");
                SetOption("terminal", "no");
                // A demuxer cache is what keeps back-and-forth scrubbing off
                // the disk: with cache disabled every backward seek re-read
                // the file, which made hovering over the seek bar stutter.
                SetOption("cache", "yes");
                SetOption("demuxer-readahead-secs", "10");
                SetOption("demuxer-max-bytes", "33554432");
                SetOption("demuxer-max-back-bytes", "16777216");
                SetOption("hwdec", "auto");

                if (mpv_initialize(m_mpv) < 0)
                {
                    throw hresult_error(E_FAIL, L"Failed to initialize mpv for preview");
                }

                CreateRenderContext();
            }
            catch (...)
            {
                if (m_renderContext)
                {
                    mpv_render_context_free(m_renderContext);
                    m_renderContext = nullptr;
                }
                if (m_mpv)
                {
                    mpv_terminate_destroy(m_mpv);
                    m_mpv = nullptr;
                }
                throw;
            }
        }

        co_await wil::resume_foreground(m_dispatcher);

        {
            std::lock_guard lifecycleLock(m_lifecycleMutex);
            if (m_destroyed)
            {
                co_return;
            }

            m_bitmap = winrt::Microsoft::UI::Xaml::Media::Imaging::WriteableBitmap(width, height);
            m_image.Source(m_bitmap);

            auto pixelBuffer = m_bitmap.PixelBuffer();
            auto byteAccess = pixelBuffer.as<IBufferByteAccess>();
            byteAccess->Buffer(&m_bitmapData);

            {
                std::lock_guard lock(m_renderMutex);
                m_quit = false;
            }
            m_workerThread = std::thread([this]() { WorkerLoop(); });
            m_initialized = true;
        }
    }

    void MpvPreviewer::CreateRenderContext()
    {
        const char* api = "sw";
        mpv_render_param params[] = {
            {MPV_RENDER_PARAM_API_TYPE, const_cast<char*>(api)},
            {MPV_RENDER_PARAM_INVALID, nullptr},
        };

        if (mpv_render_context_create(&m_renderContext, m_mpv, params) < 0)
        {
            throw hresult_error(E_FAIL, L"Failed to create software render context");
        }

        mpv_render_context_set_update_callback(m_renderContext, &MpvPreviewer::SwRenderUpdateCallback, this);
    }

    void MpvPreviewer::SwRenderUpdateCallback(void* cb_ctx)
    {
        static_cast<MpvPreviewer*>(cb_ctx)->NotifyFrameReady();
    }

    void MpvPreviewer::NotifyFrameReady()
    {
        std::lock_guard lock(m_renderMutex);
        m_framePending = true;
        m_renderCv.notify_one();
    }

    // One worker for both concerns: draining the mpv event queue (so async
    // seek replies and FILE_LOADED never pile up unconsumed) and rendering
    // frames once a seek has settled. The wait timeout makes events drain
    // even when no frame is being produced (e.g. while a file loads).
    void MpvPreviewer::WorkerLoop()
    {
        while (true)
        {
            std::unique_lock lock(m_renderMutex);
            m_renderCv.wait_for(lock, std::chrono::milliseconds(50), [this]() { return m_framePending || m_quit; });
            m_framePending = false;
            if (m_quit)
            {
                break;
            }
            lock.unlock();

            DrainEvents();
            RenderFrame();
        }
    }

    void MpvPreviewer::DrainEvents()
    {
        if (!m_mpv)
        {
            return;
        }

        while (m_mpv)
        {
            mpv_event* event = mpv_wait_event(m_mpv, 0);
            if (event->event_id == MPV_EVENT_NONE || event->event_id == MPV_EVENT_IDLE)
            {
                break;
            }

            if (event->event_id == MPV_EVENT_FILE_LOADED)
            {
                OnFileLoaded();
            }
        }
    }

    // loadfile completes asynchronously; seeks issued before this point would
    // land on the previous file or fail outright, so any position requested
    // meanwhile is replayed here.
    void MpvPreviewer::OnFileLoaded()
    {
        double pending = -1;
        {
            std::lock_guard lock(m_renderMutex);
            m_mediaReady = true;
            pending = m_pendingPos;
            m_pendingPos = -1;
        }

        int paused = 1;
        mpv_set_property(m_mpv, "pause", MPV_FORMAT_FLAG, &paused);

        if (pending >= 0)
        {
            RequestSeek(pending);
        }
    }

    void MpvPreviewer::LoadFile(winrt::hstring const& url)
    {
        if (!m_mpv)
        {
            return;
        }

        {
            std::lock_guard lock(m_renderMutex);
            m_mediaReady = false;
            m_pendingPos = -1;
        }

        std::string urlStr = winrt::to_string(url);
        const char* cmd[] = {"loadfile", urlStr.c_str(), "replace", nullptr};
        mpv_command_async(m_mpv, 0, cmd);
    }

    // Thumbfast-style request path: a non-blocking keyframe seek. mpv itself
    // coalesces queued seeks, and the render gate below skips frames until
    // the seek settles, so hover storms never queue unbounded work.
    void MpvPreviewer::RequestSeek(double position)
    {
        char time[32];
        snprintf(time, sizeof(time), "%.3f", position);
        const char* cmd[] = {"seek", time, "absolute+keyframe", nullptr};
        mpv_command_async(m_mpv, 0, cmd);
    }

    void MpvPreviewer::SetPosition(double position)
    {
        if (!m_mpv)
        {
            return;
        }

        bool ready;
        {
            std::lock_guard lock(m_renderMutex);
            ready = m_mediaReady;
            if (!ready)
            {
                m_pendingPos = position;
            }
        }

        if (ready)
        {
            RequestSeek(position);
        }
    }

    void MpvPreviewer::Pause()
    {
        if (!m_mpv)
        {
            return;
        }
        int paused = 1;
        mpv_set_property(m_mpv, "pause", MPV_FORMAT_FLAG, &paused);
    }

    void MpvPreviewer::Destroy()
    {
        std::lock_guard lifecycleLock(m_lifecycleMutex);
        m_destroyed = true;
        if (m_initialized)
        {
            m_initialized = false;
            {
                std::lock_guard lock(m_renderMutex);
                m_quit = true;
            }
            m_renderCv.notify_all();
            if (m_workerThread.joinable())
            {
                m_workerThread.join();
            }
        }

        if (m_renderContext)
        {
            mpv_render_context_set_update_callback(m_renderContext, nullptr, nullptr);
            mpv_render_context_free(m_renderContext);
            m_renderContext = nullptr;
        }

        if (m_mpv)
        {
            mpv_terminate_destroy(m_mpv);
            m_mpv = nullptr;
        }

        m_bitmapData = nullptr;
        m_bitmap = nullptr;
        m_dispatcher = nullptr;
    }

    void MpvPreviewer::RenderFrame()
    {
        if (!m_renderContext || !m_bitmapData || !m_mpv)
        {
            return;
        }

        // Never render mid-seek: the update callback also fires when a seek
        // starts, and drawing then shows stale frames and wastes a full sw
        // render per hover tick. The callback fires again once the seek
        // lands, which is when the thumbnail actually gets drawn.
        int seeking = 0;
        if (mpv_get_property(m_mpv, "seeking", MPV_FORMAT_FLAG, &seeking) >= 0 && seeking)
        {
            return;
        }

        int swSize[2] = {static_cast<int>(m_width), static_cast<int>(m_height)};
        const char* format = "bgr0";

        mpv_render_param params[] = {
            {MPV_RENDER_PARAM_SW_SIZE, swSize},
            {MPV_RENDER_PARAM_SW_FORMAT, const_cast<char*>(format)},
            {MPV_RENDER_PARAM_SW_STRIDE, &m_stride},
            {MPV_RENDER_PARAM_SW_POINTER, m_bitmapData},
            {MPV_RENDER_PARAM_INVALID, nullptr},
        };

        mpv_render_context_update(m_renderContext);
        mpv_render_context_render(m_renderContext, params);

        for (size_t i = 3; i < m_size; i += 4)
        {
            m_bitmapData[i] = 0xFF;
        }

        auto weak_this{get_weak()};
        if (m_dispatcher)
        {
            m_dispatcher.TryEnqueue([weak_this]() {
                if (auto strong_this{weak_this.get()})
                {
                    if (strong_this->m_bitmap)
                    {
                        strong_this->m_bitmap.Invalidate();
                    }
                }
            });
        }
    }
}
