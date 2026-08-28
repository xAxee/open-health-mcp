using System.Threading;
using System.Net;

namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminRawPayloadCollector
{
    private readonly AsyncLocal<CaptureState?> _activeCapture = new();

    public CaptureScope BeginCapture()
    {
        if (_activeCapture.Value is not null)
        {
            throw new InvalidOperationException("Nested Garmin payload capture is not supported.");
        }

        var state = new CaptureState();
        _activeCapture.Value = state;
        return new CaptureScope(this, state);
    }

    public void Capture(Uri? requestUri, HttpStatusCode statusCode, byte[] payload)
    {
        var state = _activeCapture.Value;
        if (state is null || requestUri is null)
        {
            return;
        }

        if (!requestUri.Host.Equals("connect.garmin.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        state.Payloads.Add(new CapturedGarminPayload(requestUri, statusCode, payload));
    }

    private void EndCapture(CaptureState state)
    {
        if (ReferenceEquals(_activeCapture.Value, state))
        {
            _activeCapture.Value = null;
        }
    }

    internal sealed class CaptureState
    {
        public List<CapturedGarminPayload> Payloads { get; } = [];
    }

    public sealed class CaptureScope : IDisposable
    {
        private readonly GarminRawPayloadCollector _owner;
        private readonly CaptureState _state;
        private bool _disposed;

        internal CaptureScope(GarminRawPayloadCollector owner, CaptureState state)
        {
            _owner = owner;
            _state = state;
        }

        public IReadOnlyList<CapturedGarminPayload> Payloads => _state.Payloads;

        public CapturedGarminPayload? Last => _state.Payloads.LastOrDefault();

        public CapturedGarminPayload? FindLast(string pathFragment) => _state.Payloads
            .LastOrDefault(item => item.RequestUri.AbsolutePath.Contains(pathFragment, StringComparison.Ordinal));

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.EndCapture(_state);
        }
    }
}

internal sealed record CapturedGarminPayload(Uri RequestUri, HttpStatusCode StatusCode, byte[] Payload)
{
    public bool HasContent => Payload.Length > 0 && StatusCode != HttpStatusCode.NoContent;
}