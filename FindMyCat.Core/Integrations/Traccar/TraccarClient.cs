using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using FindMyCat.Core.Errors;

namespace FindMyCat.Core.Integrations.Traccar;

public interface ITraccarClient
{
    Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraccarPosition>> GetPositionsAsync(string token, long deviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
}


public sealed class TraccarClient(HttpClient httpClient, ILogger<TraccarClient> logger) : ITraccarClient
{
    public async Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default)
    {
        var devices = await GetAsync<List<TraccarDeviceDto>>(token, "api/devices", cancellationToken) ?? [];
        var latestPositions = await GetAsync<List<TraccarPositionDto>>(token, "api/positions", cancellationToken) ?? [];

        var latestByDevice = latestPositions
            .GroupBy(p => p.DeviceId)
            .ToDictionary(g => g.Key, g => g.First().ToDomain());

        return devices
            .Select(d => d.ToDomain(latestByDevice.GetValueOrDefault(d.Id)))
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

        var positions = await GetAsync<List<TraccarPositionDto>>(token, query, cancellationToken) ?? [];
        return positions.Select(p => p.ToDomain()).ToList();
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
            var detail = $"Traccar returned status {(int)response.StatusCode} for {relativeUri}.";
            throw credentialRejected
                ? new TraccarCredentialRejectedException(detail)
                : new TraccarUnavailableException(detail);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(TraccarWireFormat.JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Traccar response from {Path} could not be parsed.", relativeUri);
            throw new TraccarUnavailableException($"Traccar returned an unparseable response for {relativeUri}.", ex);
        }
    }

    private static string FormatInstant(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
}
