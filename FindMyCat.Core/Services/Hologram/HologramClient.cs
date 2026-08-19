using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace FindMyCat.Core.Services.Hologram;

public interface IHologramClient
{
    Task<int?> FindDeviceIdByImeiAsync(string apiKey, string imei, CancellationToken cancellationToken = default);

    Task SendMessageAsync(string apiKey, int hologramDeviceId, string command, CancellationToken cancellationToken = default);
}

public sealed class HologramClient(HttpClient httpClient, ILogger<HologramClient> logger) : IHologramClient
{
    // Outdoor location engine (NewUdpListener.c) binds this port and expects "UDP" delivery
    private const int CollarUdpPort = 12345;
    private const string CollarProtocol = "UDP";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int?> FindDeviceIdByImeiAsync(string apiKey, string imei, CancellationToken cancellationToken = default)
    {
        httpClient.DefaultRequestHeaders.Authorization = BasicAuth(apiKey);

        using var response = await httpClient.GetAsync($"api/1/devices/?name={Uri.EscapeDataString(imei)}", cancellationToken);
        var payload = await ReadResponseAsync<DevicesListResponseDto>(response, cancellationToken);
        return payload.Data?.FirstOrDefault()?.Id;
    }

    public async Task SendMessageAsync(string apiKey, int hologramDeviceId, string command, CancellationToken cancellationToken = default)
    {
        httpClient.DefaultRequestHeaders.Authorization = BasicAuth(apiKey);

        using var response = await httpClient.PostAsJsonAsync(
            "api/1/devices/messages",
            new SendMessageRequestDto([hologramDeviceId], command, CollarUdpPort, CollarProtocol),
            JsonOptions,
            cancellationToken);
        await ReadResponseAsync<SendMessageResponseDto>(response, cancellationToken);
    }

    private static AuthenticationHeaderValue BasicAuth(string apiKey) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}")));

    private async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : IHologramEnvelope
    {
        T? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Hologram response from {Path} could not be parsed.", response.RequestMessage?.RequestUri);
            throw new HologramUpstreamException("Hologram returned an unexpected response.", credentialRejected: false, ex);
        }

        if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
        {
            var credentialRejected = response.StatusCode == HttpStatusCode.Forbidden;
            logger.LogWarning(
                "Hologram request to {Path} returned {StatusCode}. CredentialRejected={CredentialRejected}.",
                response.RequestMessage?.RequestUri, (int)response.StatusCode, credentialRejected);
            throw new HologramUpstreamException(
                payload?.Error ?? $"Hologram returned status {(int)response.StatusCode}.", credentialRejected);
        }

        return payload;
    }

    private interface IHologramEnvelope
    {
        bool Success { get; }
        string? Error { get; }
    }

    internal sealed record DevicesListResponseDto(bool Success, List<DeviceDto>? Data, string? Error) : IHologramEnvelope;

    internal sealed record DeviceDto(int Id, string Name);

    // Hologram expects the contract in this format for sending messages to our sim (device)
    internal sealed record SendMessageRequestDto([property: JsonPropertyName("deviceids")] int[] DeviceIds, string Data, int Port, string Protocol);

    internal sealed record SendMessageResponseDto(bool Success, string? Error) : IHologramEnvelope;
}
