using System.Text.Json;
using Practitioners.Api.Infrastructure;
using StackExchange.Redis;

namespace Practitioners.Api.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan ttl);
    Task RemoveAsync(string key);

    /// <summary>Глобальная версия выдач списков. Любая запись её увеличивает —
    /// все закэшированные результаты поиска становятся недостижимыми (и истекают по TTL).</summary>
    Task<long> GetListVersionAsync();
    Task BumpListVersionAsync();
}

/// <summary>Redis-кэш. Fail-open: при недоступности Redis сервис продолжает работать с БД.</summary>
public class RedisCacheService(IConnectionMultiplexer mux, ILogger<RedisCacheService> log) : ICacheService
{
    private const string ListVersionKey = "prac:list:ver";

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var v = await mux.GetDatabase().StringGetAsync(key);
            return v.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(v.ToString(), JsonDefaults.Options);
        }
        catch (Exception ex)
        {
            log.LogWarning("Cache GET failed: {Message}", ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            await mux.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value, JsonDefaults.Options), ttl);
        }
        catch (Exception ex)
        {
            log.LogWarning("Cache SET failed: {Message}", ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try { await mux.GetDatabase().KeyDeleteAsync(key); }
        catch (Exception ex) { log.LogWarning("Cache DEL failed: {Message}", ex.Message); }
    }

    public async Task<long> GetListVersionAsync()
    {
        try
        {
            var v = await mux.GetDatabase().StringGetAsync(ListVersionKey);
            return v.IsNull ? 0 : (long)v;
        }
        catch (Exception ex)
        {
            log.LogWarning("Cache version read failed: {Message}", ex.Message);
            return 0;
        }
    }

    public async Task BumpListVersionAsync()
    {
        try { await mux.GetDatabase().StringIncrementAsync(ListVersionKey); }
        catch (Exception ex) { log.LogWarning("Cache version bump failed: {Message}", ex.Message); }
    }
}
