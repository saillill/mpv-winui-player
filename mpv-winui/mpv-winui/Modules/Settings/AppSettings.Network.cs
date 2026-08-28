namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Network/stream access: ytdl, proxy, TLS, timeouts.
    /// </summary>
    public partial class AppSettings
    {
        public bool Ytdl
        {
            get => _dataSetting.GetValue(nameof(Ytdl), true);
            set => _dataSetting.SetValue(nameof(Ytdl), value);
        }

        public string YtdlRawOptionsAppend
        {
            get => _dataSetting.GetValue(nameof(YtdlRawOptionsAppend), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlRawOptionsAppend), value);
        }

        public string YtdlFormat
        {
            get => _dataSetting.GetValue(nameof(YtdlFormat), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlFormat), value);
        }

        public string YtdlPath
        {
            get => _dataSetting.GetValue(nameof(YtdlPath), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlPath), value);
        }

        public bool YtdlTryFirst
        {
            get => _dataSetting.GetValue(nameof(YtdlTryFirst), false);
            set => _dataSetting.SetValue(nameof(YtdlTryFirst), value);
        }

        public bool YtdlAllFormats
        {
            get => _dataSetting.GetValue(nameof(YtdlAllFormats), true);
            set => _dataSetting.SetValue(nameof(YtdlAllFormats), value);
        }

        public bool YtdlUseManifests
        {
            get => _dataSetting.GetValue(nameof(YtdlUseManifests), false);
            set => _dataSetting.SetValue(nameof(YtdlUseManifests), value);
        }

        public string YtdlThumbnails
        {
            get => _dataSetting.GetValue(nameof(YtdlThumbnails), "none");
            set => _dataSetting.SetValue(nameof(YtdlThumbnails), value);
        }

        public string YtdlExclude
        {
            get => _dataSetting.GetValue(nameof(YtdlExclude), string.Empty);
            set => _dataSetting.SetValue(nameof(YtdlExclude), value);
        }

        public string UserAgent
        {
            get => _dataSetting.GetValue(nameof(UserAgent), string.Empty);
            set => _dataSetting.SetValue(nameof(UserAgent), value);
        }

        public string Referrer
        {
            get => _dataSetting.GetValue(nameof(Referrer), string.Empty);
            set => _dataSetting.SetValue(nameof(Referrer), value);
        }

        public string HttpHeaderFields
        {
            get => _dataSetting.GetValue(nameof(HttpHeaderFields), string.Empty);
            set => _dataSetting.SetValue(nameof(HttpHeaderFields), value);
        }

        public string HttpProxy
        {
            get => _dataSetting.GetValue(nameof(HttpProxy), string.Empty);
            set => _dataSetting.SetValue(nameof(HttpProxy), value);
        }

        public string CookiesFile
        {
            get => _dataSetting.GetValue(nameof(CookiesFile), string.Empty);
            set => _dataSetting.SetValue(nameof(CookiesFile), value);
        }

        public bool TlsVerify
        {
            get => _dataSetting.GetValue(nameof(TlsVerify), true);
            set => _dataSetting.SetValue(nameof(TlsVerify), value);
        }

        public int NetworkTimeout
        {
            get => _dataSetting.GetValue(nameof(NetworkTimeout), 60);
            set => _dataSetting.SetValue(nameof(NetworkTimeout), value);
        }

        public int CurlMaxRedirects
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRedirects), 16);
            set => _dataSetting.SetValue(nameof(CurlMaxRedirects), value);
        }

        public int CurlMaxRetries
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRetries), 5);
            set => _dataSetting.SetValue(nameof(CurlMaxRetries), value);
        }

        public int CurlConnectTimeout
        {
            get => _dataSetting.GetValue(nameof(CurlConnectTimeout), 30);
            set => _dataSetting.SetValue(nameof(CurlConnectTimeout), value);
        }

        public int CurlBufferSize
        {
            get => _dataSetting.GetValue(nameof(CurlBufferSize), 4 * 1024 * 1024);
            set => _dataSetting.SetValue(nameof(CurlBufferSize), value);
        }

        public int CurlMaxRequestSize
        {
            get => _dataSetting.GetValue(nameof(CurlMaxRequestSize), 0);
            set => _dataSetting.SetValue(nameof(CurlMaxRequestSize), value);
        }
    }
}
