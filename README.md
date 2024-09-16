# Activity Logging Library

This library provides a middleware for logging API activity in ASP.NET Core applications.
It captures request and response details and stores them in a database using configurable stored procedures.

## Installation
### Build and Use the Library

1. Clone and build the repository
2. git clone https://github.com/nora-alshareef/ActivityLogger.git
3. Build and pack:
```shell
   cd ActivityLogger
   dotnet build 
   dotnet pack
```
### Add Reference to the Activity Logger DLL

To add a reference to the Activity Logger DLL in your project, follow these steps:

1. Right-click on your project in the Solution Explorer.
2. Select "Add" > "Reference".
3. In the Reference Manager dialog, click on "Browse".
4. Navigate to the location of the Activity Logger DLL file.
5. Select the DLL file (e.g., "ActivityLogger.dll") and click "Add".
6. Click "OK" in the Reference Manager dialog to confirm.

Alternatively, if you're using the command line or prefer editing the .csproj file directly, you can add the following line within an `<ItemGroup>` in your project file:

```xml
<Reference Include="ActivityLogger">
    <HintPath>path\to\ActivityLogger.dll</HintPath>
</Reference>
```
### Required Libraries
In your project's `.csproj` file, add the RecyclableMemoryStream library:
```xml
<ItemGroup>
        <PackageReference Include="Microsoft.IO.RecyclableMemoryStream" Version="3.0.1" />
...
</ItemGroup>
```


## Configuration

### appsettings.json

Add the following configuration to your `appsettings.json`, it should be direct section:

```json
{
  "ActivityConfigurations": {
    "ConnectionString": "...",
    "CommandTimeout": 10,
    "EnableRequestBodyLogging": false,
    "EnableResponseBodyLogging": false,
    "Procedures": {
      "UspStoreActivity": "usp_StoreActivity",
      "UspUpdateActivity": "usp_UpdateActivity"
    }
  }
}
```

### Configuration Definitions:
- ConnectionString: Your database connection string.
    - Example: "Server=myserver;Database=mydb;User Id=myuser;Password=mypassword;"
    - Note: Consider including Connect Timeout=X; in your connection string to set the connection timeout.

- CommandTimeout: The time in seconds to wait for a database store/update command to execute. Default is 30 seconds.
  - Example: 10 (for 10 seconds)
  - EnableRequestBodyLogging: Determines whether to log request bodies. Default is true.
  Set to false to disable request body logging.
    - Recommendation: Disable if request bodies are consistently long and logging them provides little value.
  - EnableResponseBodyLogging: Determines whether to log response bodies. Default is true.
  Set to false to disable response body logging.
    - Recommendation: Disable if response bodies are consistently long and logging them provides little value.
- Procedures:
   - UspStoreActivity: The name of the stored procedure for storing activity.
       - Default: "dbo.uspStoreActivity"
   - UspUpdateActivity: The name of the stored procedure for updating activity.
       - Default: "dbo.uspUpdateActivity"

### Program.cs

```csharp
using ActivityLogger.Middleware;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Configure ActivityOptions
builder.Services.Configure<ActivityOptions>(
    builder.Configuration.GetSection("ActivityConfigurations")
);

// Register ActivityLogger services
builder.Services.AddActivityServices<Guid>(
    sp => 
    {
        var activityOptions = sp.GetRequiredService<IOptions<ActivityOptions>>().Value;
        return new SqlConnection(activityOptions.ConnectionString);
    }
);

// ... other services

var app = builder.Build();

// ... other middleware

app.UseActivityMiddleware<Guid>(); //it should be last middleware , you can assign it a function to generate customize traceId

// ... other app configurations

app.Run();
```

### Usage

The middleware will automatically log all API requests and responses once it's set up as shown above.

### 4. Generic Type Parameter TTraceId

The AddActivityServices<TTraceId> and UseActivityMiddleware<TTraceId> methods use a generic type parameter TTraceId.
This allows you to specify the type of your trace ID.

We provide built-in support for string, int, long, and Guid trace ID types.
The example above uses string as the trace ID type.
Choose the type that best fits your application's needs.

Custom Trace ID Generation
By default, the middleware generates trace IDs based on the specified type. However, you can provide a custom trace ID
generator:

```csharp
app.UseActivityMiddleware<string>(() => GenerateCustomTraceId());
```
Replace GenerateCustomTraceId() with your custom logic to generate trace IDs.

## Database Setup

### Activity Table Structure

Create an `Activity` table in your database with the following structure:

```sql
CREATE TABLE [dbo].[Activity] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [TraceId] NVARCHAR(100) NOT NULL,
    [ClientIP] NVARCHAR(50) NULL,
    [EndPoint] NVARCHAR(500) NOT NULL,
    [RequestAt] DATETIME2(7) NOT NULL,
    [ResponseAt] DATETIME2(7) NULL,
    [RequestBody] NVARCHAR(MAX) NULL,
    [ResponseBody] NVARCHAR(MAX) NULL,
    [StatusCode] INT NOT NULL,
    [RequestMethod] NVARCHAR(10) NOT NULL,
    [IsCancelled] BIT NOT NULL DEFAULT 0
)
```
### Using the IsCancelled Flag
The IsCancelled flag is used to indicate whether an operation was cancelled. This could be due to various reasons such as a timeout, a manual cancellation, or an OperationCanceledException being thrown.

### Stored Procedures
Ensure you have the following stored procedures in your database:

dbo.uspStoreActivity: For inserting new activity records.
dbo.uspUpdateActivity: For updating existing activity records (e.g., adding response information).
Example dbo.uspStoreActivity:

```sql
CREATE PROCEDURE uspStoreActivity
    @TraceId NVARCHAR(100),
    @ClientIP NVARCHAR(50),
    @EndPoint NVARCHAR(500),
    @RequestAt DATETIME2(7),
    @RequestBody NVARCHAR(MAX),
    @StatusCode INT,
    @RequestMethod NVARCHAR(10),
    @IsCancelled BIT
AS
BEGIN
    SET NOCOUNT ON;

INSERT INTO [dbo].[Activity] (
    TraceId,
    ClientIP,
    EndPoint,
    RequestAt,
    RequestBody,
    StatusCode,
    RequestMethod,
    IsCancelled
) VALUES (
    @TraceId,
    @ClientIP,
    @EndPoint,
    CASE WHEN @RequestAt = '0001-01-01' THEN GETUTCDATE() ELSE @RequestAt END,
    @RequestBody,
    @StatusCode,
    @RequestMethod,
    @IsCancelled
    )
END
```

Example dbo.uspUpdateActivity:

```sql
CREATE PROCEDURE uspUpdateActivity
    @TraceId NVARCHAR(100),
    @ResponseBody NVARCHAR(MAX),
    @StatusCode INT,
    @ResponseAt DATETIME2(7),
    @IsCancelled BIT
AS
BEGIN
    SET NOCOUNT ON

UPDATE [dbo].[Activity]
SET ResponseBody = @ResponseBody,
    StatusCode = @StatusCode,
    ResponseAt = CASE WHEN @ResponseAt = '0001-01-01' THEN GETUTCDATE() ELSE @ResponseAt END,
        IsCancelled = @IsCancelled
    WHERE TraceId = @TraceId
END
```

### Read Activities directly : 

versatile stored procedure that allows filtering by RequestAt, ResponseAt, StatusCode, and TraceId:
```sql
CREATE PROCEDURE uspGetActivities
    @StartRequestAt DATETIME2(7) = NULL,
    @EndRequestAt DATETIME2(7) = NULL,
    @StartResponseAt DATETIME2(7) = NULL,
    @EndResponseAt DATETIME2(7) = NULL,
    @StatusCode INT = NULL,
    @TraceId NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        TraceId,
        ClientIP,
        EndPoint,
        RequestAt,
        ResponseAt,
        RequestBody,
        ResponseBody,
        StatusCode,
        RequestMethod,
        IsCancelled
    FROM 
        [dbo].[Activity]
    WHERE 
        (@StartRequestAt IS NULL OR RequestAt >= @StartRequestAt)
        AND (@EndRequestAt IS NULL OR RequestAt <= @EndRequestAt)
        AND (@StartResponseAt IS NULL OR ResponseAt >= @StartResponseAt)
        AND (@EndResponseAt IS NULL OR ResponseAt <= @EndResponseAt)
        AND (@StatusCode IS NULL OR StatusCode = @StatusCode)
        AND (@TraceId IS NULL OR TraceId = @TraceId)
    ORDER BY 
        RequestAt ASC;
END

```
This stored procedure:
Takes optional parameters for filtering:

@StartRequestAt and @EndRequestAt for filtering by RequestAt range
@StartResponseAt and @EndResponseAt for filtering by ResponseAt range
@StatusCode for filtering by status code
@TraceId for filtering by a specific TraceId

Uses these parameters in the WHERE clause, but only applies the filter if the parameter is not NULL.
Orders the results by RequestAt in ascending order.
You can use this stored procedure directly in SQL Server Management Studio (SSMS) or any other SQL client. Here are some example usages:

Get all activities within a RequestAt range:

```sql
EXEC uspGetActivities 
    @StartRequestAt = '2023-01-01', 
    @EndRequestAt = '2023-12-31'
Get activities with a specific StatusCode:
```

```sql
EXEC uspGetActivities @StatusCode = 200
Get activities for a specific TraceId:
```

```sql
EXEC uspGetActivities @TraceId = 'your-trace-id-here'
```
Combine multiple filters:
```sql
EXEC uspGetActivities 
    @StartRequestAt = '2023-01-01', 
    @EndRequestAt = '2023-12-31',
    @StatusCode = 500
 ```  
Get activities within a ResponseAt range:
```sql
EXEC uspGetActivities 
    @StartResponseAt = '2023-06-01', 
    @EndResponseAt = '2023-06-30'
```
