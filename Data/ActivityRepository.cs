using ActivityLogger.Models;

namespace ActivityLogger.Data;

public interface IActivityRepository<TTraceId>
{
    Task<bool> StoreActivityOnRequest(Activity<TTraceId> activity);
    Task<bool> UpdateActivityOnResponse(Activity<TTraceId> activity);
}

public class ActivityRepository<TTraceId>(IActivityDb<TTraceId> activityDb) : IActivityRepository<TTraceId>
{
    public async Task<bool> StoreActivityOnRequest(Activity<TTraceId> activity)
    {
        try
        {
            await activityDb.StoreActivity(activity);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateActivityOnResponse(Activity<TTraceId> activity)
    {
        try
        {
            await activityDb.UpdateActivity(
                activity
            );

            return true;
        }
        catch
        {
            return false;
        }
    }
}