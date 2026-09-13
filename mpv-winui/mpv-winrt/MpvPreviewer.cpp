#include "pch.h"
#include "MpvPreviewer.h"
#include "MpvPreviewer.g.cpp"
#include <winrt/Microsoft.UI.Dispatching.h>
#include <wil/cppwinrt_helpers.h>

namespace
{
    // reply_userdata for the "seeking" observation. Non-zero so property-change
    // events cannot be confused with the fire-and-forget async commands below,
    // which all pass 0.
    constexpr uint64_t SeekObserveId = 1;

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
                // Tuned after thumbfast (po5/thumbfast), which is the reference
                // implementation for seek-bar thumbnails. The previous settings
                // here (readahead 10s, 32MiB demuxer cache) were backwards: the
                // preview only ever wants the single frame at the seek target,
                // and mpv will not report the seek as settled until it has
                // refilled the readahead window. That refill was a constant
                // ~200ms per hover, independent of the target, and it competed
                // for the disk with the main player instance reading the same
                // file. thumbfast spawns its helper with
                // --demuxer-readahead-secs=0 --demuxer-max-bytes=128KiB for
                // exactly this reason.
                SetOption("cache", "no");
                SetOption("demuxer-readahead-secs", "0");
                SetOption("demuxer-max-bytes", "131072");
                SetOption("demuxer-max-back-bytes", "0");
                // Decode as little as possible for one frame.
                SetOption("vd-lavc-skiploopfilter", "all");
                SetOption("vd-lavc-fast", "yes");
                SetOption("vd-lavc-threads", "2");
                // The software render path scales through libswscale.
                SetOption("sws-scaler", "fast-bilinear");
                SetOption("sws-allow-zimg", "no");
                SetOption("hwdec", "auto");
                // Without this mpv_render_context_render() blocks until the
                // frame's supposed display time (render.h: "this will limit
                // your rendering to video FPS. You can prevent this by setting
                // the video-timing-offset global option to 0"). The preview
                // instance is paused, so that wait can stall the worker thread
                // - which is also the thread draining mpv events.
                SetOption("video-timing-offset", "0");

                if (mpv_initialize(m_mpv) < 0)
                {
                    throw hresult_error(E_FAIL, L"Failed to initialize mpv for preview");
                }

                CreateRenderContext();

                // Watch "seeking" so the worker is woken when a seek settles.
                // libmpv raises the render update callback only once per queued
                // frame, so this is the reliable signal that the frame for a
                // requested position is ready to be drawn.
                mpv_observe_property(m_mpv, SeekObserveId, "seeking", MPV_FORMAT_FLAG);
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
                // Force one pass so the first frame is drawn even if libmpv's
                // update callback arrived before the worker thread started.
                m_renderNeeded = true;
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
    //
    // Frames are never gated on this timeout: the update callback and
    // RequestRender() notify the condition variable directly, so the wait
    // returns at once. The periodic wakeup is only the safety net for event
    // draining, so it runs at 10 Hz rather than a 20 Hz busy tick that would
    // keep the thread hot for the whole session even with no preview shown.
    void MpvPreviewer::WorkerLoop()
    {
        while (true)
        {
            std::unique_lock lock(m_renderMutex);
            m_renderCv.wait_for(lock, std::chrono::milliseconds(100), [this]() { return m_framePending || m_quit; });
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
            else if (event->event_id == MPV_EVENT_SEEK || event->event_id == MPV_EVENT_PLAYBACK_RESTART)
            {
                // A seek settled (or the video chain was reconfigured). Force a
                // render pass: libmpv raises the update callback only once per
                // queued frame, so relying on it alone can leave the thumbnail
                // stuck on a stale frame.
                RequestRender();
            }
            else if (event->event_id == MPV_EVENT_PROPERTY_CHANGE && event->reply_userdata == SeekObserveId)
            {
                // The "seeking" flag changed. Force a render pass so the frame
                // for the requested position is drawn as soon as it exists.
                //
                // The value is deliberately NOT read: the pass must never be
                // gated on it (see RenderFrame), and reading it with
                // mpv_get_property() here would be a synchronous round-trip to
                // the core that blocks for as long as the seek takes.
                RequestRender();
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

        // A freshly loaded file always needs one draw; without this the
        // thumbnail stays empty until the first seek happens to land.
        m_renderNeeded = true;

        int paused = 1;
        mpv_set_property(m_mpv, "pause", MPV_FORMAT_FLAG, &paused);

        if (pending >= 0)
        {
            RequestSeek(pending);
        }
    }

    // Wakes the worker for a render pass that was deferred (or must be redone)
    // without relying on libmpv's update callback.
    void MpvPreviewer::RequestRender()
    {
        std::lock_guard lock(m_renderMutex);
        m_renderNeeded = true;
        m_framePending = true;
        m_renderCv.notify_one();
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

        // NOTE: the flag must stay "keyframes" (plural). "keyframe" is NOT a
        // valid mpv seek flag: mpv_command_async() rejects the command outright
        // with MPV_ERROR_INVALID_PARAMETER (-4) and - per client.h, "the only
        // case when you do not receive an event is when the function call
        // itself fails" - emits no reply, no MPV_EVENT_SEEK and no frame. The
        // seek then simply never happens, which is what once left the thumbnail
        // frozen on the frame from load time.
        const char* cmd[] = {"seek", time, "absolute+keyframes", nullptr};
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

        // Render even while a seek is in flight. libmpv's render API hands a
        // frame to the client and waits for it to be consumed, so refusing the
        // pass because mpv reports "seeking" makes the core wait for us before
        // the seek can settle. Measured against the shipped mpv-2.dll on a
        // 320x240 AVI: seek settles in ~5ms when this pass runs, and in
        // ~200ms when it is gated on "seeking" - that gate was the whole
        // "thumbnail lags the cursor" cost. The update callback fires once per
        // queued frame, so this is one extra pass per seek, not per tick.
        const uint64_t flags = mpv_render_context_update(m_renderContext);
        if ((flags & MPV_RENDER_UPDATE_FRAME) == 0 && !m_renderNeeded)
        {
            return;
        }
        m_renderNeeded = false;

        int swSize[2] = {static_cast<int>(m_width), static_cast<int>(m_height)};
        const char* format = "bgr0";

        mpv_render_param params[] = {
            {MPV_RENDER_PARAM_SW_SIZE, swSize},
            {MPV_RENDER_PARAM_SW_FORMAT, const_cast<char*>(format)},
            {MPV_RENDER_PARAM_SW_STRIDE, &m_stride},
            {MPV_RENDER_PARAM_SW_POINTER, m_bitmapData},
            {MPV_RENDER_PARAM_INVALID, nullptr},
        };

        mpv_render_context_render(m_renderContext, params);

        // The sw render context does not fill alpha; bgr0 leaves it undefined
        // and a WriteableBitmap shows it as transparency.
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
