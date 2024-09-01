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
        Action<ActivityOptions>? configureOptions = null)
    {
        
        if (configureOptions != null)
            services.ConfigureOptions(
                new ConfigureNamedOptions<ActivityOptions>(Options.DefaultName, configureOptions));

        services.AddScoped<Activity<TTraceId>>();
        services.AddSingleton<IRequestLogger<TTraceId>, RequestLogger<TTraceId>>();
        services.AddSingleton<IResponseLogger<TTraceId>, ResponseLogger<TTraceId>>();
        
        services.AddSingleton<IActivityDb<TTraceId>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ActivityOptions>>();
            var logger = sp.GetRequiredService<ILogger<IActivityDb<TTraceId>>>();
            return new ActivityDb<TTraceId>(options, logger, () => dbConnectionFactory(sp));
        });
        
        // // Register IActivityDb<TTraceId> as scoped
        // services.AddScoped<IActivityDb<TTraceId>>(sp =>
        // {
        //     var options = sp.GetRequiredService<IOptions<ActivityOptions>>();
        //     var logger = sp.GetRequiredService<ILogger<IActivityDb<TTraceId>>>();
        //     var db = new ActivityDb<TTraceId>(options, logger);
        //     db.ConnectionFactory = () => dbConnectionFactory(sp);
        //     return db;
        // });
        

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
    [Required] public string ConnectionString { get; set; } = string.Empty;
    public string UspStoreActivity { get; set; } = "dbo.uspStoreActivity";
    public string UspUpdateActivity { get; set; } = "dbo.uspUpdateActivity";
}