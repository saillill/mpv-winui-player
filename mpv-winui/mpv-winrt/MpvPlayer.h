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
        PausedForCache = 13,
        MenuData = 41,
        Playlist = 42,
        DiscMenuActive = 50,

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
        void InitializeForPreview(uint32_t width, uint32_t height);
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

        void PlaylistPlayIndex(int32_t index);
        void PlaylistMove(int32_t from, int32_t to);
        void PlaylistRemove(int32_t index);
        void PlaylistNext();
        void PlaylistPrevious();
        void PlaylistShuffle();

        winrt::hstring GetWatchHistoryPath();
        winrt::hstring GetWatchLaterFolderPath();
        winrt::hstring GetCurrentFilePath();
        bool SaveWatchHistory();
        winrt::hstring GetCurrentPath();

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
        int32_t Edition();
        void Edition(int32_t value);
        int32_t CurrentEdition();
        winrt::hstring GetDiscPath(winrt::mpv_winrt::DiscType type);
        void SetDiscPath(winrt::mpv_winrt::DiscType type, winrt::hstring const& path);

        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvMenuItem> GetMenu();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::hstring> GetSubtitleExtensions();
        winrt::hstring GetVersion();

        winrt::event_token FileStarted(winrt::mpv_winrt::FileStartedEventHandler const& handler);
        void FileStarted(winrt::event_token const& token) noexcept;
        winrt::event_token FileLoaded(winrt::mpv_winrt::FileLoadedEventHandler const& handler);
        void FileLoaded(winrt::event_token const& token) noexcept;
        winrt::event_token FileFailed(winrt::mpv_winrt::FileFailedEventHandler const& handler);
        void FileFailed(winrt::event_token const& token) noexcept;
        winrt::event_token FileEnded(winrt::mpv_winrt::FileEndedEventHandler const& handler);
        void FileEnded(winrt::event_token const& token) noexcept;
        winrt::event_token FileStopped(winrt::mpv_winrt::FileStoppedEventHandler const& handler);
        void FileStopped(winrt::event_token const& token) noexcept;
        winrt::event_token SeekStarted(winrt::mpv_winrt::SeekStartedEventHandler const& handler);
        void SeekStarted(winrt::event_token const& token) noexcept;
        winrt::event_token PlaybackRestarted(winrt::mpv_winrt::PlaybackRestartedEventHandler const& handler);
        void PlaybackRestarted(winrt::event_token const& token) noexcept;
        winrt::event_token SwapChainChanged(winrt::mpv_winrt::SwapChainChangedEventHandler const& handler);
        void SwapChainChanged(winrt::event_token const& token) noexcept;
        winrt::event_token TrackChanged(winrt::mpv_winrt::TrackChangedEventHandler const& handler);
        void TrackChanged(winrt::event_token const& token) noexcept;
        winrt::event_token BufferingChanged(winrt::mpv_winrt::BufferingChangedEventHandler const& handler);
        void BufferingChanged(winrt::event_token const& token) noexcept;
        MPVWINRT_EVENT(LogMessage, MpvLogEventHandler, logMessage)
        MPVWINRT_EVENT(MediaLoaded, MediaLoadedEventHandler, mediaLoaded)
        MPVWINRT_EVENT(PlaybackFailed, PlaybackFailedEventHandler, playbackFailed)
        MPVWINRT_EVENT(Seeked, SeekEventHandler, seeked)
        MPVWINRT_EVENT(VoConfigured, VoConfiguredEventHandler, voConfigured)

        winrt::event_token PlaybackStateChanged(winrt::mpv_winrt::PlaybackStateChangedEventHandler const& handler);
        void PlaybackStateChanged(winrt::event_token const& token) noexcept;
        winrt::event_token VolumeChanged(winrt::mpv_winrt::VolumeChangedEventHandler const& handler);
        void VolumeChanged(winrt::event_token const& token) noexcept;
        winrt::event_token PositionChanged(winrt::mpv_winrt::PositionChangedEventHandler const& handler);
        void PositionChanged(winrt::event_token const& token) noexcept;
        winrt::event_token SpeedChanged(winrt::mpv_winrt::SpeedChangedEventHandler const& handler);
        void SpeedChanged(winrt::event_token const& token) noexcept;
        winrt::event_token MediaInfoChanged(winrt::mpv_winrt::MediaInfoChangedEventHandler const& handler);
        void MediaInfoChanged(winrt::event_token const& token) noexcept;
        winrt::event_token NetworkInfoChanged(winrt::mpv_winrt::NetworkInfoChangedEventHandler const& handler);
        void NetworkInfoChanged(winrt::event_token const& token) noexcept;
        winrt::event_token TrackListChanged(winrt::mpv_winrt::TrackListChangedEventHandler const& handler);
        void TrackListChanged(winrt::event_token const& token) noexcept;
        winrt::event_token TrackListCountChanged(winrt::mpv_winrt::TrackListCountChangedEventHandler const& handler);
        void TrackListCountChanged(winrt::event_token const& token) noexcept;
        winrt::event_token WindowChanged(winrt::mpv_winrt::WindowChangedEventHandler const& handler);
        void WindowChanged(winrt::event_token const& token) noexcept;
        winrt::event_token DiscMenuActiveChanged(winrt::mpv_winrt::DiscMenuActiveChangedEventHandler const& handler);
        void DiscMenuActiveChanged(winrt::event_token const& token) noexcept;
        winrt::event_token LoopFileChanged(winrt::mpv_winrt::LoopFileChangedEventHandler const& handler);
        void LoopFileChanged(winrt::event_token const& token) noexcept;
        winrt::event_token LoopPlaylistChanged(winrt::mpv_winrt::LoopPlaylistChangedEventHandler const& handler);
        void LoopPlaylistChanged(winrt::event_token const& token) noexcept;
        winrt::event_token ShuffleChanged(winrt::mpv_winrt::ShuffleChangedEventHandler const& handler);
        void ShuffleChanged(winrt::event_token const& token) noexcept;
        winrt::event_token PlaylistChanged(winrt::mpv_winrt::PlaylistChangedEventHandler const& handler);
        void PlaylistChanged(winrt::event_token const& token) noexcept;

    // restored from upstream during the merge (declarations were lost with the conflict resolution)
        void LoadList(hstring const& url);
        void TogglePlayPause();
        void SetHoverSec(double sec);
        void SetDrawPreview(int32_t x, int32_t y, int32_t w, int32_t h);
        void ClearPreview();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvEdition> GetEditions();
        winrt::Windows::Foundation::Collections::IVectorView<winrt::mpv_winrt::MpvProfile> GetProfiles();

        

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
        bool GetFlagProperty(const char* name, bool defaultValue);
        bool IsStringPropertyEqual(const char* name, std::string_view expected);
        void SetDoubleProperty(const char* name, double value);
        void SetInt64Property(const char* name, int64_t value);
        void SetStringProperty(const char* name, const std::string& value);
        void SetUserData(const char* key, const std::string& value);

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

        winrt::event<winrt::mpv_winrt::FileStartedEventHandler> m_fileStartedEvent;
        winrt::event<winrt::mpv_winrt::FileLoadedEventHandler> m_fileLoadedEvent;
        winrt::event<winrt::mpv_winrt::FileFailedEventHandler> m_fileFailedEvent;
        winrt::event<winrt::mpv_winrt::FileEndedEventHandler> m_fileEndedEvent;
        winrt::event<winrt::mpv_winrt::FileStoppedEventHandler> m_fileStoppedEvent;
        winrt::event<winrt::mpv_winrt::SeekStartedEventHandler> m_seekStartedEvent;
        winrt::event<winrt::mpv_winrt::PlaybackRestartedEventHandler> m_playbackRestartedEvent;
        winrt::event<winrt::mpv_winrt::SwapChainChangedEventHandler> m_swapChainChangedEvent;
        winrt::event<winrt::mpv_winrt::TrackChangedEventHandler> m_trackChangedEvent;
        winrt::event<winrt::mpv_winrt::BufferingChangedEventHandler> m_bufferingChangedEvent;

        winrt::event<winrt::mpv_winrt::PlaybackStateChangedEventHandler> m_playbackStateChangedEvent;
        winrt::event<winrt::mpv_winrt::VolumeChangedEventHandler> m_volumeChangedEvent;
        winrt::event<winrt::mpv_winrt::PositionChangedEventHandler> m_positionChangedEvent;
        winrt::event<winrt::mpv_winrt::SpeedChangedEventHandler> m_speedChangedEvent;
        winrt::event<winrt::mpv_winrt::MediaInfoChangedEventHandler> m_mediaInfoChangedEvent;
        winrt::event<winrt::mpv_winrt::NetworkInfoChangedEventHandler> m_networkInfoChangedEvent;
        winrt::event<winrt::mpv_winrt::TrackListChangedEventHandler> m_trackListChangedEvent;
        winrt::event<winrt::mpv_winrt::TrackListCountChangedEventHandler> m_trackListCountChangedEvent;
        winrt::event<winrt::mpv_winrt::WindowChangedEventHandler> m_windowChangedEvent;
        winrt::event<winrt::mpv_winrt::DiscMenuActiveChangedEventHandler> m_discMenuActiveChangedEvent;
        winrt::event<winrt::mpv_winrt::LoopFileChangedEventHandler> m_loopFileChangedEvent;
        winrt::event<winrt::mpv_winrt::LoopPlaylistChangedEventHandler> m_loopPlaylistChangedEvent;
        winrt::event<winrt::mpv_winrt::ShuffleChangedEventHandler> m_shuffleChangedEvent;
        winrt::event<winrt::mpv_winrt::PlaylistChangedEventHandler> m_playlistChangedEvent;
        winrt::event<winrt::mpv_winrt::MpvLogEventHandler> m_logMessageEvent;
        winrt::event<winrt::mpv_winrt::MediaLoadedEventHandler> m_mediaLoadedEvent;
        winrt::event<winrt::mpv_winrt::PlaybackFailedEventHandler> m_playbackFailedEvent;
        winrt::event<winrt::mpv_winrt::SeekEventHandler> m_seekedEvent;
        winrt::event<winrt::mpv_winrt::VoConfiguredEventHandler> m_voConfiguredEvent;
    };

    #undef MPVWINRT_EVENT
}

namespace winrt::mpv_winrt::factory_implementation
{
    struct MpvPlayer: MpvPlayerT<MpvPlayer, implementation::MpvPlayer>
    {
    };
}