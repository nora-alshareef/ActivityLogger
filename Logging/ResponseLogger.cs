using ActivityLogger.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace ActivityLogger.Logging;

public interface IResponseLogger<TTraceId>
{
    Task LogResponse(HttpContext context, Activity<TTraceId> activity, ILogger logger, RequestDelegate next);
}

public class ResponseLogger<TTraceId> (bool enableResponseBodyLogging): IResponseLogger<TTraceId>
{
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager = new();

    public async Task LogResponse(HttpContext context, Activity<TTraceId> activity, ILogger logger,
        RequestDelegate next)
    {
        await SaveResponseBody(context, activity, logger, next); 
        LogResponseDetails(context, activity);
    }

    private async Task SaveResponseBody(HttpContext context, Activity<TTraceId> activity, ILogger logger,
        RequestDelegate next)
    {
        var originalBodyStream = context.Response.Body;
        var responseBody = SetupResponseCapture(context, activity, logger);
        var cts = CreateLinkedCancellationTokenSource(context, activity, logger);
        if (cts.IsCancellationRequested) 
        {
            logger.LogWarning("[ActivityLogger] Request was cancelled before processing by the client {TraceId}",activity.TraceId);
            activity.IsCancelled = true;
        }
        else
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[ActivityLogger] Request was cancelled by the client {TraceId}",
                    activity.TraceId);
                //I prefer to keep capturing the response even if it's not received, as the processing is done anyway.
                activity.IsCancelled = true;
            }
            finally
            {
                if (cts.IsCancellationRequested)
                {
                    activity.IsCancelled = true;
                    logger.LogWarning("[ActivityLogger] Request was cancelled after processing {TraceId}",
                        activity.TraceId);
                }

                if (enableResponseBodyLogging)
                {
                    await CaptureAndRestoreResponse(context, activity, logger, originalBodyStream, responseBody);
                }
                else
                {
                    activity.ResponseBody = null;
                }
                
            }
        }
    }

    private static CancellationTokenSource CreateLinkedCancellationTokenSource(HttpContext context,
        Activity<TTraceId> activity, ILogger logger)
    {
        try
        {
            return CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ActivityLogger] Failed to create linked CancellationTokenSource {TraceId}",
                activity.TraceId);
            return new CancellationTokenSource();
        }
    }

    private Stream SetupResponseCapture(HttpContext context, Activity<TTraceId> activity, ILogger logger)
    {
        try
        {
            var responseBody = _recyclableMemoryStreamManager.GetStream();
            context.Response.Body = responseBody;
            return responseBody;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ActivityLogger] Failed to set up response body capture. TraceId: {TraceId}",
                activity.TraceId);
            return context.Response.Body;
        }
    }

    private static async Task CaptureAndRestoreResponse(HttpContext context, Activity<TTraceId> activity,
        ILogger logger,
        Stream originalBodyStream, Stream responseBody)
    {
        try
        {
            if (responseBody != originalBodyStream)
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                activity.ResponseBody = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            else
            {
                activity.ResponseBody = "Failed to capture response body";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ActivityLogger] Failed to capture response body. TraceId: {TraceId}",
                activity.TraceId);
            activity.ResponseBody = "Failed to capture response body due to an error";
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            if (responseBody != originalBodyStream) await responseBody.DisposeAsync();
        }
    }

    private static void LogResponseDetails(HttpContext context, Activity<TTraceId> activity)
    {
        activity.StatusCode = context.Response.StatusCode;
        activity.ResponseAt = DateTime.Now; // Consider using UTC time
    }
}