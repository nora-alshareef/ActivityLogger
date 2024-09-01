using System.Data;
using System.Data.Common;
using ActivityLogger.Middleware;
using ActivityLogger.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityLogger.Data;

public interface IActivityDb<TTraceId>
{
    Task StoreActivity(Activity<TTraceId> activity);
    Task UpdateActivity(Activity<TTraceId> activity);
}

public class ActivityDb<TTraceId> : IActivityDb<TTraceId>
{
    private readonly string _uspStoreActivity;
    private readonly string _uspUpdateActivity;
    private readonly ILogger<IActivityDb<TTraceId>> _logger;
    private readonly Func<DbConnection> _connectionFactory;

    public ActivityDb(
        IOptions<ActivityOptions> options,
        ILogger<IActivityDb<TTraceId>> logger,
        Func<DbConnection> connectionFactory)
    {
        _uspStoreActivity = options.Value.UspStoreActivity;
        _uspUpdateActivity = options.Value.UspUpdateActivity;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public async Task StoreActivity(Activity<TTraceId>? activity)
    {
        //TraceId = traceId,
        // ClientIp = await GetClientIp(context),
        // Path = await GetEndpoint(context),
        // RequestAt = DateTime.UtcNow, // Consider using UTC time
        // RequestBody = await GetRequestBody(context),
        // Method = context.Request.Method,
        // StatusCode = -1,
        // IsCancelled = false
        await ExecuteNonQueryAsync(_uspStoreActivity, command =>
        {
            if (activity == null) return;
            AddTraceIdParameter(command, activity.TraceId);
            AddParameter(command, "@ClientIP", activity.ClientIp ?? string.Empty);
            AddParameter(command, "@EndPoint", activity.EndPoint ?? string.Empty);
            AddParameter(command, "@RequestAt", activity.RequestAt ?? DateTime.MinValue);
            AddParameter(command, "@RequestBody", activity.RequestBody ?? string.Empty);
            AddParameter(command, "@StatusCode", activity.StatusCode ?? -1);
            AddParameter(command, "@RequestMethod", activity.RequestMethod ?? string.Empty);
            AddParameter(command, "@IsCancelled", activity.IsCancelled ?? false);
        });
    }

    public async Task UpdateActivity(Activity<TTraceId> activity)
    {
        await ExecuteNonQueryAsync(_uspUpdateActivity, command =>
        {
            AddTraceIdParameter(command, activity.TraceId);
            AddParameter(command, "@ResponseBody", activity.ResponseBody ?? string.Empty);
            AddParameter(command, "@StatusCode", activity.StatusCode);
            AddParameter(command, "@ResponseAt", activity.ResponseAt ?? DateTime.MinValue);
            AddParameter(command, "@IsCancelled", activity.IsCancelled ?? false);
        });
    }

    private async Task ExecuteNonQueryAsync(string storedProcedure, Action<DbCommand> setParameters)
    {
        using var connection = _connectionFactory();
        try
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = storedProcedure;
            command.CommandType = CommandType.StoredProcedure;
            setParameters(command);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ActivityLogger] Error executing stored procedure {StoredProcedure}", storedProcedure);
        }
    }

    private static void AddTraceIdParameter(DbCommand command, TTraceId traceId)
    {
        if (traceId != null)
        {
            if (typeof(TTraceId) == typeof(Guid))
                AddParameter(command, "@TraceID", (Guid)(object)traceId);
            else if (typeof(TTraceId) == typeof(long))
                AddParameter(command, "@TraceID", (long)(object)traceId);
            else if (typeof(TTraceId) == typeof(int))
                AddParameter(command, "@TraceID", (int)(object)traceId);
            else if (typeof(TTraceId) == typeof(string))
                AddParameter(command, "@TraceID", (string)(object)traceId);
            else
                throw new ArgumentException($"[ActivityLogger]Unsupported trace ID type: {typeof(TTraceId).Name}");
        }
        else throw new ArgumentException($"[ActivityLogger] traceId is null");
    }

    private static void AddParameter(DbCommand command, string parameterName, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}