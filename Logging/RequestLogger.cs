using ActivityLogger.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace ActivityLogger.Logging;

public interface IRequestLogger<TTraceId>
{
    //static in an interface method declaration indicates that the method is associated with the interface itself, not with instances of classes that implement the interface. It's a way to define behavior that is related to the interface as a whole.
    //abstract in this context means that the method doesn't provide an implementation in the interface. It's a declaration that classes implementing this interface must provide their own implementation of this static method.
    Task<Activity<TTraceId>> LogRequest(TTraceId traceId, HttpContext context, ILogger logger);
}

public class RequestLogger<TTraceId> : IRequestLogger<TTraceId>
{
    private static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new();

    public async Task<Activity<TTraceId>> LogRequest(TTraceId traceId, HttpContext context, ILogger logger)
    {
        try
        {
            var activity = new Activity<TTraceId>
            {
                TraceId = traceId,
                ClientIp = await GetClientIp(context),
                Path = await GetEndpoint(context),
                RequestAt = DateTime.UtcNow, // Consider using UTC time
                RequestBody = await GetRequestBody(context),
                Method = context.Request.Method,
                StatusCode = -1,
                IsCancelled = false
            };

            return activity;
        }
        catch (Exception ex)
        {
            // Simple log of the exception
            logger.LogError(ex, "[ActivityLogger] Failed to log request. TraceId: {TraceId}", traceId);

            // Return null to indicate that activity creation failed
            return new Activity<TTraceId>
            {
                TraceId = traceId,
                RequestAt = DateTime.Now,
                StatusCode = -1,
                Method = context.Request.Method,
                Path = context.Request.Path.ToString(),
                ClientIp = "Unknown",
                RequestBody = "Failed to capture request body due to an error",
                IsCancelled = false
            };
        }
    }

    private static Task<string> GetClientIp(HttpContext context)
    {
        return Task.FromResult(context.Request.Host.ToString());
    }

    private static Task<string> GetEndpoint(HttpContext context)
    {
        return Task.FromResult(context.Request.Path + context.Request.QueryString);
    }

    private static async Task<string> GetRequestBody(HttpContext context)
    {
        context.Request.EnableBuffering();
        await using var requestStream = RecyclableMemoryStreamManager.GetStream();
        await context.Request.Body.CopyToAsync(requestStream);
        var reqBody = ReadStreamInChunks(requestStream);
        context.Request.Body.Position = 0;
        return reqBody;
    }

    private static string ReadStreamInChunks(Stream stream)
    {
        const int readChunkBufferLength = 4096;
        stream.Seek(0, SeekOrigin.Begin);
        using var textWriter = new StringWriter();
        using var reader = new StreamReader(stream);
        var readChunk = new char[readChunkBufferLength];
        int readChunkLength;
        do
        {
            readChunkLength = reader.ReadBlock(readChunk, 0, readChunkBufferLength);
            textWriter.Write(readChunk, 0, readChunkLength);
        } while (readChunkLength > 0);

        return textWriter.ToString();
    }
}