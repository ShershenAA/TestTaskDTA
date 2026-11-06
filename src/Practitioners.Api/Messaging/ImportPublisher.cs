using System.Text.Json;
using Practitioners.Api.Infrastructure;
using StackExchange.Redis;

namespace Practitioners.Api.Messaging;

/// <summary>Публикует события импорта в Redis Stream.</summary>
public class ImportPublisher(IConnectionMultiplexer mux)
{
    public async Task PublishAsync(ImportEvent evt)
    {
        var json = JsonSerializer.Serialize(evt, JsonDefaults.Options);
        await mux.GetDatabase().StreamAddAsync(StreamNames.Import, "payload", json);
    }
}
