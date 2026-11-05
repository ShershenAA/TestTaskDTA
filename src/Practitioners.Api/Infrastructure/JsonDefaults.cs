using System.Text.Json;

namespace Practitioners.Api.Infrastructure;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
