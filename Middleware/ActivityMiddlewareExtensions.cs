using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using ActivityLogger.Data;
using ActivityLogger.Logging;
using ActivityLogger.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityLogger.Middleware;

public static class ActivityMiddlewareExtensions
{
    public static IServiceCollection AddActivityServices<TTraceId>(
        this IServiceCollection services,
        Func<IServiceProvider, DbConnection> dbConnectionFactory,
        Action<ActivityOptions> configureOptions )
    {
        
        if (configureOptions != null)
            services.ConfigureOptions(
                new ConfigureNamedOptions<ActivityOptions>(Options.DefaultName, configureOptions));

        services.AddScoped<Activity<TTraceId>>();
        services.AddSingleton<IRequestLogger<TTraceId>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ActivityOptions>>().Value;
            return new RequestLogger<TTraceId>(options.EnableRequestBodyLogging);
        });
        services.AddSingleton<IResponseLogger<TTraceId>, ResponseLogger<TTraceId>>();
        
        services.AddSingleton<IResponseLogger<TTraceId>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ActivityOptions>>().Value;
            return new ResponseLogger<TTraceId>(options.EnableResponseBodyLogging);
        });
        
        services.AddSingleton<IActivityDb<TTraceId>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ActivityOptions>>();
            var logger = sp.GetRequiredService<ILogger<IActivityDb<TTraceId>>>();
            return new ActivityDb<TTraceId>(options, logger, () => dbConnectionFactory(sp));
        });

        return services;
    }

    public static IApplicationBuilder UseActivityMiddleware<TTraceId>(
        this IApplicationBuilder app,
        Func<TTraceId>? customTraceIdGenerator = null)
    {
        return app.UseMiddleware<ActivityMiddleware<TTraceId>>(customTraceIdGenerator ?? DefaultTraceIdGenerator<TTraceId>);
    }
    
    private static TTraceId DefaultTraceIdGenerator<TTraceId>()
    {
        if (typeof(TTraceId) == typeof(string))
            return (TTraceId)(object)Guid.NewGuid().ToString();
        if (typeof(TTraceId) == typeof(Guid))
            return (TTraceId)(object)Guid.NewGuid();
        if (typeof(TTraceId) == typeof(int)) 
            return (TTraceId)(object)new Random().Next();
        // Add more type checks as needed

        throw new InvalidOperationException($"[ActivityLogger] Unsupported trace ID type: {typeof(TTraceId).Name}");
    }
}

public class ActivityOptions
{
    public required string ConnectionString { get; set; } = string.Empty;
    public required int CommandTimeout { get; set; } = 2;// 30 seconds
    public required ActivityProcedures Procedures { get; set; }
    public bool EnableRequestBodyLogging { get; set; }
    public bool EnableResponseBodyLogging { get; set; }

    public class ActivityProcedures
    {
        public required string UspStoreActivity { get; set; }
        public required string UspUpdateActivity { get; set; }
    }
}