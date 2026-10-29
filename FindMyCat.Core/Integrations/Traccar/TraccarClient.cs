using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

using FindMyCat.Core.Errors;

namespace FindMyCat.Core.Integrations.Traccar;

public interface ITraccarClient
{
    Task<IReadOnlyList<TraccarDevice>> GetDevicesWithPositionsAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraccarPosition>> GetPositionsAsync(string token, long deviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);

    Task<TraccarDevice> CreateDeviceAsync(string token, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default);

    Task<TraccarDevice> UpdateDeviceAsync(string token, long deviceId, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default);

    Task DeleteDeviceAsync(string token, long deviceId, CancellationToken cancellationToken = default);
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

    public async Task<TraccarDevice> CreateDeviceAsync(
        string token, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        var device = new JsonObject
        {
            ["name"] = name,
            ["uniqueId"] = uniqueId,
            ["attributes"] = new JsonObject { ["emoji"] = emoji }
        };
        var created = await SendAsync<TraccarDeviceDto>(token, HttpMethod.Post, "api/devices", device, cancellationToken);
        return created!.ToDomain(latestPosition: null);
    }

    public async Task<TraccarDevice> UpdateDeviceAsync(
        string token, long deviceId, string name, string uniqueId, string emoji, CancellationToken cancellationToken = default)
    {
        // Traccar replaces the whole device on PUT, so start from what it has to keep
        // fields and attributes this API does not manage.
        var device = await GetAsync<JsonObject>(token, $"api/devices/{deviceId}", cancellationToken)
                     ?? throw new DeviceNotFoundException(deviceId);
        device["name"] = name;
        device["uniqueId"] = uniqueId;
        var attributes = device["attributes"] as JsonObject ?? new JsonObject();
        attributes["emoji"] = emoji;
        device["attributes"] = attributes;

        var updated = await SendAsync<TraccarDeviceDto>(
            token, HttpMethod.Put, $"api/devices/{deviceId}", device, cancellationToken);
        return updated!.ToDomain(latestPosition: null);
    }

    public Task DeleteDeviceAsync(string token, long deviceId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(token, HttpMethod.Delete, $"api/devices/{deviceId}", body: null, cancellationToken);

    private Task<T?> GetAsync<T>(string token, string relativeUri, CancellationToken cancellationToken) =>
        SendAsync<T>(token, HttpMethod.Get, relativeUri, body: null, cancellationToken);

    private async Task<T?> SendAsync<T>(
        string token, HttpMethod method, string relativeUri, JsonObject? body, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var request = new HttpRequestMessage(method, relativeUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: TraccarWireFormat.JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var credentialRejected = response.StatusCode == HttpStatusCode.Unauthorized;
            logger.LogWarning(
                "Traccar request to {Path} returned {StatusCode}. CredentialRejected={CredentialRejected}.",
                relativeUri, (int)response.StatusCode, credentialRejected);
            var detail = $"Traccar returned status {(int)response.StatusCode} for {relativeUri}.";
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new TraccarCredentialRejectedException(detail),
                // Traccar answers 400 for bad input such as a uniqueId another device already has.
                HttpStatusCode.BadRequest => new TraccarDeviceRejectedException(detail),
                _ => new TraccarUnavailableException(detail)
            };
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
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
