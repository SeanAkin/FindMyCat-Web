using System.Text.Json;
using System.Text.Json.Serialization;

namespace FindMyCat.Api.Json;

public static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Default = Create();

    public static void Configure(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter());

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
