#pragma once
#include "MpvPlayer.g.h"
#include <atomic>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <mpv/client.h>
#include <map>
#include <mutex>
#include <string>
#include <string_view>
#include <thread>
#include <winrt/Microsoft.UI.Dispatching.h>
#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::mpv_winrt::implementation
{

    // Generates the add/remove accessor pair C++/WinRT requires for an IDL
    // event backed by a winrt::event<HandlerType> member named m_<field>Event.
    // Expansion: (eventName, handlerType, eventNameField) — one line per event,
    // #undef'd right after the MpvPlayer struct so it cannot leak.
    #define MPVWINRT_EVENT(event, handlerType, field)                                         \
        winrt::event_token event(winrt::mpv_winrt::handlerType const& handler)                \
        {                                                                                     \
            return m_##field##Event.add(handler);                                             \
        }                                                                                     \
        void event(winrt::event_token const& token) noexcept                                  \
        {                                                                                     \
            m_##field##Event.remove(token);                                                   \
        }

    enum MpvObserveId: uint64_t
    {
        CoreIdle = 1,
        Pause = 2,
        Duration = 3,
        TimePos = 5,
        Speed = 7,
        Volume = 8,
        Mute = 9,
        MediaTitle = 21,
        LoopFile = 10,
        LoopPlaylist = 11,
        Shuffle = 12,
        Playlist = 42,
        VoConfigured = 50,

        // donot change
        Fullscreen = 201,
        Ontop = 202,
        WindowMinimized = 203,
        WindowMaximized = 204,
        TitleBar = 205,
        Border = 206,
    };

    struct MpvPlayer: MpvPlayerT<MpvPlayer>
    {
        MpvPlayer();
        ~MpvPlayer();

        void Initialize(hstring const& configPath, uint32_t width, uint32_t height, int32_t volume, winrt::mpv_winrt::DisplayColorKind colorKind, int32_t refreshRate);
        void Destroy();
        void AttachSwapChain(winrt::Microsoft::UI::Xaml::Controls::SwapChainPanel const& panel);
        void UpdateSwapChainScale(float scaleX, float scaleY);
        void UpdateSize(uint32_t width, uint32_t height);
        void UpdateDisplayColorInfo(winrt::mpv_winrt::DisplayColorKind colorKind);
        void UpdateDisplayRefreshRate(int32_t refreshRate);
        void LoadFile(hstring const& url, double position);

        void Play();
        void Pause();
        void Stop();
        bool IsPaused();

        void Command(winrt::Windows::Foundation::Collections::IVector<hstring> const& args);
        void CommandString(hstring const& cmd);
        void ApplyCommandStrings(winrt::Windows::Foundation::Collections::IVector<hstring> const& commands);
        void SetLogLevel(hstring const& level);

        // Generic property subscription: any mpv property can be observed by
        // name without touching the IDL again. The handler receives the
        // property name and mpv's string rendering of the value (empty when
        // the property is unavailable). Registered before Initialize are
        // attached once mpv comes up; handles start above every MpvObserveId
        // so the event switch can tell the two apart.
        uint64_t ObserveProperty(hstring const& name, winrt::mpv_winrt::MpvPropertyChangedEventHandler const& handler);
        void UnobserveProperty(uint64_t handle);

        winrt::hstring GetWatchHistoryPath();
        winrt::hstring GetWatchLaterFolderPath();
        winrt::hstring GetCurrentFilePath();
        bool SaveWatchHistory();

        double Volume();
        void Volume(double value);
        bool IsMuted();
        void IsMuted(bool value);

        double Position();
        void Position(double value);
        double Duration();

        double AbLoopA();
        double AbLoopB();

        int32_t CurrentVideoTrack();
        void CurrentVideoTrack(int32_t value);
        int32_t CurrentAudioTrack();
        void CurrentAudioTrack(int32_t value);
        int32_t CurrentSubtitleTrack();
        void CurrentSubtitleTrack(int32_t value);
        int32_t CurrentSecondSubtitleTrack();
        void CurrentSecondSubtitleTrack(int32_t value);
        void AddSubtitle(hstring const& url, bool const& selected, hstring const& title);

        double PlaybackSpeed();
        void PlaybackSpeed(double value);

        bool LoopFile();
        void LoopFile(bool enabled);
        void SetLoopPlaylist(bool enabled);
        bool LoopPlaylist();
        void SetShuffle(bool enabled);
        bool Shuffle();

        void SetAspectRatio(hstring const& ratio);

        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvPlaylistItem> GetPlaylist();

        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvTrack> GetAudioTracks();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvTrack> GetVideoTracks();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvTrack> GetSubtitleTracks();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvChapter> GetChapters();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvAudioDevice> GetAudioDevices();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvGpuAdapter> GetGpuAdapters();
        int32_t CurrentChapter();
        int32_t CurrentEdition();

        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvMenuItem> GetMenu();
        winrt::hstring GetSubtitleExtensions();

        MPVWINRT_EVENT(MediaLoaded, MediaLoadedEventHandler, mediaLoaded)
        MPVWINRT_EVENT(PlaybackFailed, PlaybackFailedEventHandler, playbackFailed)
        MPVWINRT_EVENT(Seeked, SeekEventHandler, seeked)
        MPVWINRT_EVENT(FileLoaded, FileLoadedEventHandler, fileLoaded)

        MPVWINRT_EVENT(PlaybackStateChanged, PlaybackStateChangedEventHandler, playbackStateChanged)
        MPVWINRT_EVENT(VolumeChanged, VolumeChangedEventHandler, volumeChanged)
        MPVWINRT_EVENT(PositionChanged, PositionChangedEventHandler, positionChanged)
        MPVWINRT_EVENT(SpeedChanged, SpeedChangedEventHandler, speedChanged)
        MPVWINRT_EVENT(MediaInfoChanged, MediaInfoChangedEventHandler, mediaInfoChanged)
        MPVWINRT_EVENT(VoConfigured, VoConfiguredEventHandler, voConfigured)
        MPVWINRT_EVENT(WindowChanged, WindowChangedEventHandler, windowChanged)
        MPVWINRT_EVENT(LoopFileChanged, LoopFileChangedEventHandler, loopFileChanged)
        MPVWINRT_EVENT(LoopPlaylistChanged, LoopPlaylistChangedEventHandler, loopPlaylistChanged)
        MPVWINRT_EVENT(ShuffleChanged, ShuffleChangedEventHandler, shuffleChanged)
        MPVWINRT_EVENT(PlaylistChanged, PlaylistChangedEventHandler, playlistChanged)
        MPVWINRT_EVENT(LogMessage, MpvLogEventHandler, logMessage)

    private:
        static mpv_node* FindMapField(mpv_node* map, const char* key);
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvTrack> GetTracks(const char* type);
        void CreateContext();
        void SetOption(std::string const& name, std::string const& value);
        void StartEventThread();
        void StopEventThread();
        void ProcessEvents();
        void HandleMpvEvent(mpv_event* event);

        void CompletePendingSwapChainAttach();
        void StartPropertyObservers();

        double GetDoubleProperty(const char* name);
        int64_t GetInt64Property(const char* name);
        winrt::hstring GetHStringProperty(const char* name);
        bool GetFlagProperty(const char* name);
        bool IsStringPropertyEqual(const char* name, std::string_view expected);
        void SetDoubleProperty(const char* name, double value);
        void SetInt64Property(const char* name, int64_t value);
        void SetStringProperty(const char* name, const std::string& value);

        mpv_handle* m_mpv{nullptr};
        std::atomic<IDXGISwapChain*> m_swapChain{nullptr};
        // Target panel recorded by AttachSwapChain, with the dispatcher it
        // was captured on. When the vo has no swap chain yet the attach is
        // deferred: the next VIDEO_RECONFIG re-drives it via
        // CompletePendingSwapChainAttach on the panel's UI thread, so
        // callers never have to re-drive AttachSwapChain around vo
        // readiness. Replaced on every AttachSwapChain call, cleared on
        // teardown.
        winrt::Microsoft::UI::Xaml::Controls::SwapChainPanel m_targetPanel{nullptr};
        winrt::Microsoft::UI::Dispatching::DispatcherQueue m_targetPanelDispatcher{nullptr};
        std::mutex m_targetPanelMutex;
        // Generic observers (ObserveProperty): written on the caller's thread,
        // read on the mpv event thread inside PROPERTY_CHANGE. Handles start
        // above every MpvObserveId value.
        static constexpr uint64_t kDynamicObserveBase = 0x100000000ULL;
        std::atomic<uint64_t> m_nextObserverHandle{kDynamicObserveBase};
        std::mutex m_observersMutex;
        std::map<uint64_t, std::pair<winrt::hstring, winrt::mpv_winrt::MpvPropertyChangedEventHandler>> m_propertyObservers;
        // True once mpv_initialize has succeeded; mpv_set_option_string is only
        // valid before that, runtime option changes must go through properties.
        std::atomic<bool> m_initialized{false};

        std::thread m_eventThread;
        std::atomic<bool> m_eventThreadRunning{false};
        double m_lastDuration{0.0};

        winrt::event<winrt::mpv_winrt::MediaLoadedEventHandler> m_mediaLoadedEvent;
        winrt::event<winrt::mpv_winrt::PlaybackFailedEventHandler> m_playbackFailedEvent;
        winrt::event<winrt::mpv_winrt::SeekEventHandler> m_seekedEvent;
        winrt::event<winrt::mpv_winrt::FileLoadedEventHandler> m_fileLoadedEvent;

        winrt::event<winrt::mpv_winrt::PlaybackStateChangedEventHandler> m_playbackStateChangedEvent;
        winrt::event<winrt::mpv_winrt::VolumeChangedEventHandler> m_volumeChangedEvent;
        winrt::event<winrt::mpv_winrt::PositionChangedEventHandler> m_positionChangedEvent;
        winrt::event<winrt::mpv_winrt::SpeedChangedEventHandler> m_speedChangedEvent;
        winrt::event<winrt::mpv_winrt::MediaInfoChangedEventHandler> m_mediaInfoChangedEvent;
        winrt::event<winrt::mpv_winrt::VoConfiguredEventHandler> m_voConfiguredEvent;
        winrt::event<winrt::mpv_winrt::WindowChangedEventHandler> m_windowChangedEvent;
        winrt::event<winrt::mpv_winrt::LoopFileChangedEventHandler> m_loopFileChangedEvent;
        winrt::event<winrt::mpv_winrt::LoopPlaylistChangedEventHandler> m_loopPlaylistChangedEvent;
        winrt::event<winrt::mpv_winrt::ShuffleChangedEventHandler> m_shuffleChangedEvent;
        winrt::event<winrt::mpv_winrt::PlaylistChangedEventHandler> m_playlistChangedEvent;
        winrt::event<winrt::mpv_winrt::MpvLogEventHandler> m_logMessageEvent;
    };

    #undef MPVWINRT_EVENT
}

namespace winrt::mpv_winrt::factory_implementation
{
    struct MpvPlayer: MpvPlayerT<MpvPlayer, implementation::MpvPlayer>
    {
    };
}
