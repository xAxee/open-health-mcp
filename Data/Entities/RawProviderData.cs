using System.Text.Json;

namespace OpenHealthMCP.Data.Entities;

public sealed class RawProviderData
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string DataType { get; set; }
    public required string ExternalId { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public required JsonDocument Payload { get; set; }
}