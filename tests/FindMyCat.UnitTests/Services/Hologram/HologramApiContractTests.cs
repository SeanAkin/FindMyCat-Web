using System.Net;
using System.Text;
using System.Text.Json;
using FindMyCat.Core.Services.Hologram;
using FindMyCat.UnitTests.Infrastructure;
using Json.Schema;
using JsonPointer = Json.Pointer.JsonPointer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;

namespace FindMyCat.UnitTests.Services.Hologram;

public sealed class HologramOpenApiSpecFixture
{
    public const string SpecId = "https://dashboard.hologram.io/api/1/docs/openapi.json";

    private static readonly string SnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "Services", "Hologram", "hologram-openapi.json");

    private readonly BuildOptions _openApiKeywordTolerantOptions = new()
    {
        Dialect = new Dialect
        {
            AllowUnknownKeywords = true
        }
    };

    public OpenApiDocument Document { get; }

    public HologramOpenApiSpecFixture()
    {
        var json = File.ReadAllText(SnapshotPath);

        var readResult = OpenApiDocument.Parse(json, "json");
        Document = readResult.Document
            ?? throw new InvalidOperationException($"Hologram OpenAPI spec failed to parse: {readResult.Diagnostic}");

        RegisterRootSchemaSoRefsResolve(JsonSchema.FromText(json, buildOptions: _openApiKeywordTolerantOptions));
    }

    private static void RegisterRootSchemaSoRefsResolve(JsonSchema rootSchema) =>
        SchemaRegistry.Global.Register(new Uri(SpecId), rootSchema);

    public JsonSchema SchemaAt(JsonPointer pointerIntoSpec) =>
        new JsonSchemaBuilder()
            .Ref($"{SpecId}#{pointerIntoSpec}")
            .Build(_openApiKeywordTolerantOptions);
}

/// <summary>
/// Validates the slice of the committed Hologram OpenAPI spec snapshot that
/// <see cref="HologramClient"/> actually depends on
/// </summary>
public class HologramApiContractTests(HologramOpenApiSpecFixture fixture) : IClassFixture<HologramOpenApiSpecFixture>
{
    [Fact]
    public void DeviceObject_id_and_name_are_the_types_we_deserialize_into()
    {
        var deviceObject = fixture.Document.Components!.Schemas!["DeviceObject"];

        deviceObject.Properties!["id"].Type.ShouldBe(JsonSchemaType.Integer);
        deviceObject.Properties!["name"].Type.ShouldBe(JsonSchemaType.String);
    }

    [Fact]
    public void Devices_list_endpoint_still_has_the_name_filter_we_query_with()
    {
        var operation = fixture.Document.Paths["/devices"].Operations![HttpMethod.Get];
        var nameParam = operation.Parameters!.Single(p => p.Name == "name");

        nameParam.Schema!.Type.ShouldBe(JsonSchemaType.String);
    }

    [Fact]
    public async Task SendMessage_request_satisfies_the_documented_schema()
    {
        var capturedBody = await CaptureRealSendMessagePayloadAsync();

        AssertJsonSatisfiesSchema(RequestSchemaPointer("/devices/messages", HttpMethod.Post), capturedBody);
    }

    [Fact]
    public void ListDevices_response_dto_satisfies_the_documented_schema()
    {
        var sampleResponse = new HologramClient.DevicesListResponseDto(
            Success: true,
            Data: [new HologramClient.DeviceDto(4229782, "unique-1")],
            Error: null);

        AssertDtoSatisfiesSchema(ResponseSchemaPointer("/devices", HttpMethod.Get, "200"), sampleResponse);
    }

    [Fact]
    public void SendMessage_response_dto_satisfies_the_documented_schema()
    {
        var sampleResponse = new HologramClient.SendMessageResponseDto(Success: true, Error: null);

        AssertDtoSatisfiesSchema(ResponseSchemaPointer("/devices/messages", HttpMethod.Post, "200"), sampleResponse);
    }

    private static JsonPointer RequestSchemaPointer(string apiPath, HttpMethod method) =>
        JsonPointer.Create(
            "paths", apiPath, method.Method.ToLowerInvariant(), "requestBody", "content", "application/json", "schema");

    private static JsonPointer ResponseSchemaPointer(string apiPath, HttpMethod method, string statusCode) =>
        JsonPointer.Create(
            "paths", apiPath, method.Method.ToLowerInvariant(), "responses", statusCode, "content", "application/json", "schema");

    private void AssertDtoSatisfiesSchema<T>(JsonPointer schemaPointer, T dto) =>
        AssertJsonSatisfiesSchema(schemaPointer, JsonSerializer.Serialize(dto, HologramClient.JsonOptions));

    private void AssertJsonSatisfiesSchema(JsonPointer schemaPointer, string json)
    {
        var validationSchema = fixture.SchemaAt(schemaPointer);

        using var instance = JsonDocument.Parse(json);

        var result = validationSchema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        result.IsValid.ShouldBeTrue(DescribeErrors(result));
    }

    private static string DescribeErrors(EvaluationResults result)
    {
        var messages = new List<string>();
        if (result.Errors is not null)
        {
            messages.AddRange(result.Errors.Values);
        }

        if (result.Details is not null)
        {
            messages.AddRange(result.Details.SelectMany(d => d.Errors?.Values ?? Enumerable.Empty<string>()));
        }

        return messages.Count > 0 ? string.Join(", ", messages) : "(no error detail provided by evaluator)";
    }

    private static async Task<string> CaptureRealSendMessagePayloadAsync()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""", Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://hologram.test/") };
        var client = new HologramClient(httpClient, NullLogger<HologramClient>.Instance);

        await client.SendMessageAsync("key", 42, "ping");

        return capturedBody!;
    }
}