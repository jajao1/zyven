using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
namespace Zyven.Infrastructure;

public sealed class AuthRateGate(IConnectionMultiplexer redis)
{
    public async Task<bool> Allow(string kind, string key, int limit)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var result = await redis.GetDatabase().ScriptEvaluateAsync("local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('EXPIRE',KEYS[1],ARGV[1]); end; return n", [$"zyven:auth:{kind}:{hash}"], [900]);
        return (long)result <= limit;
    }
}
