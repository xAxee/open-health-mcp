using Garmin.Connect;
using Garmin.Connect.Auth;

namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminClientSession : IDisposable
{
    private readonly GarminOptions _options;
    private readonly GarminRawPayloadCollector _collector;
    private readonly object _gate = new();
    private HttpClient? _httpClient;
    private IGarminConnectClient? _client;

    public GarminClientSession(GarminOptions options, GarminRawPayloadCollector collector)
    {
        _options = options;
        _collector = collector;
    }

    public IGarminConnectClient Client
    {
        get
        {
            if (!_options.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Garmin is not configured. Set GARMIN_EMAIL and GARMIN_PASSWORD.");
            }

            if (_client is not null)
            {
                return _client;
            }

            lock (_gate)
            {
                if (_client is not null)
                {
                    return _client;
                }

                var handler = new GarminRawCaptureHandler(_collector, new HttpClientHandler());
                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(90)
                };

                var sessionPath = Path.GetFullPath(_options.SessionPath);
                var auth = new BasicAuthParameters(_options.Email, _options.Password);
                var context = new GarminConnectContext(
                    _httpClient,
                    auth,
                    new GarminMfaCodeProvider(_options),
                    new FileTokenCache(sessionPath));

                _client = new GarminConnectClient(context);
                return _client;
            }
        }
    }

    public void Dispose() => _httpClient?.Dispose();
}