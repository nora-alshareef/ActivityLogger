using ActivityLogger.Data;
using ActivityLogger.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ActivityLogger.Middleware;

public class ActivityMiddleware<TTraceId>(
    RequestDelegate next,
    IActivityDb<TTraceId> activityDb,
    IRequestLogger<TTraceId> requestLogger,
    IResponseLogger<TTraceId> responseLogger,
    ILogger<ActivityMiddleware<TTraceId>> logger,
    Func<TTraceId> traceIdGenerator)
{


    private static async Task<TTraceId> SetTraceId(HttpContext context, Func<TTraceId> traceIdGeneratorFunc)
    {
        var traceId = traceIdGeneratorFunc();
        context.TraceIdentifier = traceId.ToString();
        return await Task.FromResult(traceId);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var traceId = await SetTraceId(context, traceIdGenerator);
            var activity = await requestLogger.LogRequest(traceId, context, logger);
            logger.LogInformation("[ActivityLogger] Request logged: {TraceId}", activity.TraceId);

            await activityDb.StoreActivity(activity);
            logger.LogInformation("[ActivityLogger] Activity stored: {TraceId}", activity.TraceId);

            await responseLogger.LogResponse(context, activity, logger, next);
            logger.LogInformation("[ActivityLogger] Response logged: {TraceId}", activity.TraceId);

            await activityDb.UpdateActivity(activity);
            logger.LogInformation("[ActivityLogger] Activity updated: {TraceId}", activity.TraceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ActivityLogger] Error in ActivityMiddleware,TraceId: {{TraceId}}");
            throw;
        }
    }
}