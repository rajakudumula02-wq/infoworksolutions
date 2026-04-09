// Feature: healthcare-fhir-api
using HealthcareFhirApi.Core.Exceptions;
using HealthcareFhirApi.Core.Models;
using StackExchange.Redis;

namespace HealthcareFhirApi.Api.Middleware;

public class RateLimitingMiddleware(RequestDelegate next)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, TenantContext tenantContext, IDatabase redis)
    {
        // Skip if tenant not resolved yet (anonymous endpoints)
        if (string.IsNullOrEmpty(tenantContext.TenantId))
        {
            await next(context);
            return;
        }

        try
        {
            var key = $"ratelimit:{tenantContext.TenantId}";
            var limit = tenantContext.RateLimitRequestsPerSecond;

            var current = await redis.StringIncrementAsync(key);
            if (current == 1)
                await redis.KeyExpireAsync(key, TimeSpan.FromSeconds(1));

            if (current > limit)
            {
                context.Response.Headers["Retry-After"] = "1";
                throw new RateLimitExceededException();
            }
        }
        catch (RateLimitExceededException) { throw; }
        catch (RedisConnectionException) { /* Redis unavailable — skip rate limiting */ }
        catch (RedisTimeoutException) { /* Redis timeout — skip rate limiting */ }

        await next(context);
    }
}
