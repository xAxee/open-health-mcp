using System.Text.Json;

namespace OpenHealthMCP.Data.Entities;

public sealed class RawProviderData
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string DataType { get; set; }
    public required string ExternalId { get; set; }
    public string? Endpoint { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public string? PayloadHash { get; set; }
    public required string ParserVersion { get; set; }
    public required JsonDocument Payload { get; set; }
}