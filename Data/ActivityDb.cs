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
    void SetConnection(DbConnection connection);
}

public class ActivityDb<TTraceId>(
    IOptions<ActivityOptions> options,
    ILogger<ActivityDb<TTraceId>> logger) : IActivityDb<TTraceId>
{
    private readonly string _uspStoreActivity = options.Value.UspStoreActivity;
    private readonly string _uspUpdateActivity = options.Value.UspUpdateActivity;
    private DbConnection? _connection;

    public void SetConnection(DbConnection connection)
    {
        _connection = connection;
    }

    public async Task StoreActivity(Activity<TTraceId>? activity)
    {
        await ExecuteNonQueryAsync(_uspStoreActivity, command =>
        {
            if (activity == null) return;
            AddTraceIdParameter(command, activity.TraceId);
            AddParameter(command, "@ClientIP", activity.ClientIp ?? string.Empty);
            AddParameter(command, "@EndPoint", activity.Path ?? string.Empty);
            AddParameter(command, "@RequestAt", activity.RequestAt ?? DateTime.MinValue);
            AddParameter(command, "@ResponseAt", activity.ResponseAt ?? DateTime.MinValue);
            AddParameter(command, "@RequestBody", activity.RequestBody ?? string.Empty);
            AddParameter(command, "@ResponseBody", activity.ResponseBody ?? string.Empty);
            AddParameter(command, "@StatusCode", activity.StatusCode ?? -1);
            AddParameter(command, "@RequestMethod", activity.Method ?? string.Empty);
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
        });
    }

    private async Task ExecuteNonQueryAsync(string storedProcedure, Action<DbCommand> setParameters)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Database connection has not been set.");
        }

        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            using var command = _connection.CreateCommand();
            command.CommandText = storedProcedure;
            command.CommandType = CommandType.StoredProcedure;
            setParameters(command);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ActivityLogger] Error executing stored procedure {StoredProcedure}", storedProcedure);
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