using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services.Traccar;

public interface ITraccarClient
{
    Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraccarPosition>> GetPositionsAsync(string token, long deviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
}


public sealed class TraccarClient(HttpClient httpClient, ILogger<TraccarClient> logger) : ITraccarClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default)
    {
        var devices = await GetAsync<List<DeviceDto>>(token, "api/devices", cancellationToken) ?? [];
        var latestPositions = await GetAsync<List<PositionDto>>(token, "api/positions", cancellationToken) ?? [];

        var latestByDevice = latestPositions
            .GroupBy(p => p.DeviceId)
            .ToDictionary(g => g.Key, g => MapPosition(g.First()));

        return devices
            .Select(d => new TraccarDevice(
                d.Id,
                d.Name ?? string.Empty,
                d.UniqueId ?? string.Empty,
                d.Status ?? "unknown",
                d.LastUpdate,
                d.Disabled,
                latestByDevice.GetValueOrDefault(d.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<TraccarPosition>> GetPositionsAsync(
        string token,
        long deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/positions?deviceId={deviceId}";
        if (from is not null && to is not null)
        {
            query += $"&from={FormatInstant(from.Value)}&to={FormatInstant(to.Value)}";
        }

        var positions = await GetAsync<List<PositionDto>>(token, query, cancellationToken) ?? [];
        return positions.Select(MapPosition).ToList();
    }

    private async Task<T?> GetAsync<T>(string token, string relativeUri, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.GetAsync(relativeUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var credentialRejected = response.StatusCode == HttpStatusCode.Unauthorized;
            logger.LogWarning(
                "Traccar request to {Path} returned {StatusCode}. CredentialRejected={CredentialRejected}.",
                relativeUri, (int)response.StatusCode, credentialRejected);
            throw new TraccarUpstreamException(
                $"Traccar returned status {(int)response.StatusCode}.", credentialRejected);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Traccar response from {Path} could not be parsed.", relativeUri);
            throw new TraccarUpstreamException("Traccar returned an unexpected response.", credentialRejected: false, ex);
        }
    }

    private static TraccarPosition MapPosition(PositionDto p) => new(
        p.DeviceId,
        p.FixTime,
        p.DeviceTime,
        p.ServerTime,
        p.Latitude,
        p.Longitude,
        p.Altitude,
        p.Speed,
        p.Course,
        p.Accuracy,
        p.Valid,
        p.Attributes?.BatteryLevel,
        p.Attributes?.Sat);

    private static string FormatInstant(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

    private sealed record DeviceDto(
        long Id,
        string? Name,
        string? UniqueId,
        string? Status,
        DateTimeOffset? LastUpdate,
        bool Disabled);

    private sealed record PositionDto(
        long DeviceId,
        DateTimeOffset FixTime,
        DateTimeOffset DeviceTime,
        DateTimeOffset ServerTime,
        double Latitude,
        double Longitude,
        double Altitude,
        double Speed,
        double Course,
        double Accuracy,
        bool Valid,
        PositionAttributesDto? Attributes);

    private sealed record PositionAttributesDto(
        [property: JsonPropertyName("batteryLevel")] double? BatteryLevel,
        [property: JsonPropertyName("sat")] double? Sat);
}
