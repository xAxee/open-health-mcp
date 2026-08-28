namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminRawCaptureHandler(
    GarminRawPayloadCollector collector,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode && response.Content is not null)
        {
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            collector.Capture(
                request.RequestUri,
                payload,
                response.Content.Headers.ContentType?.MediaType);
        }

        return response;
    }
}