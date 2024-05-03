using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace UkrChatBot.Utils;

public class RedisCacheManager
{
    private readonly IConfiguration _configuration;

    public RedisCacheManager(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public async Task<T?> GetFromCacheAsync<T>(string key)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(
            _configuration.GetConnectionString("Redis") ??
            throw new InvalidOperationException());
        var db = connection.GetDatabase();
        var cachedData = await db.StringGetAsync(key);
        return !cachedData.IsNull ? JsonConvert.DeserializeObject<T>(cachedData) : default;
    }

    public async Task SetInCacheAsync<T>(string key, T data, TimeSpan expiry)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(
            _configuration.GetConnectionString("Redis") ??
            throw new InvalidOperationException());
        var db = connection.GetDatabase();
        await db.StringSetAsync(key, JsonConvert.SerializeObject(data), expiry: expiry);
    }
}