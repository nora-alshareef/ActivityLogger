# Activity Logging Library

This library provides a middleware for logging API activity in ASP.NET Core applications.
It captures request and response details and stores them in a database using configurable stored procedures.

# Configuration

### 1. appsettings.json

Add the following configuration to your `appsettings.json`:

```json
{
  "Activity": {
    "ConnectionString": "your_connection_string_here",
    "UspStoreActivity": "dbo.uspStoreActivity",
    "UspUpdateActivity": "dbo.uspUpdateActivity"
  }
}
```

ConnectionString: Your database connection string.
UspStoreActivity: The name of the stored procedure for storing activity (default: "dbo.uspStoreActivity").
UspUpdateActivity: The name of the stored procedure for updating activity (default: "dbo.uspUpdateActivity").

### 2. Program.cs

```csharp
using idart.shared.Activity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddActivityServices<Guid>(
    Configuration,
    sp => 
    {
        var options = sp.GetRequiredService<IOptions<ActivityOptions>>();
        return new SqlConnection(options.Value.ConnectionString);
    },
    options => 
    {
        // Additional configuration if needed
        // options.UspStoreActivity = "custom.uspStoreActivity";
        // options.UspUpdateActivity = "custom.uspUpdateActivity";
    }
);

// ... other services

var app = builder.Build();

// ... other middleware

app.UseActivityMiddleware<Guid>(); //it should be last middleware

// ... other app configurations

app.Run();
```

### 3. Usage

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

### Stored Procedures
Ensure you have the following stored procedures in your database:

dbo.uspStoreActivity: For inserting new activity records.
dbo.uspUpdateActivity: For updating existing activity records (e.g., adding response information).
Example dbo.uspStoreActivity:

```sql
CREATE PROCEDURE [dbo].[uspStoreActivity]
    @TraceId NVARCHAR(100),
    @ClientIP NVARCHAR(50),
    @EndPoint NVARCHAR(500),
    @RequestAt DATETIME2(7),
    @ResponseAt DATETIME2(7),
    @RequestBody NVARCHAR(MAX),
    @ResponseBody NVARCHAR(MAX),
    @StatusCode INT,
    @RequestMethod NVARCHAR(10),
    @IsCancelled BIT
AS
BEGIN
INSERT INTO [dbo].[Activity] (
    TraceId, ClientIP, EndPoint, RequestAt, ResponseAt,
    RequestBody, ResponseBody, StatusCode, RequestMethod, IsCancelled
) VALUES (
    @TraceId, @ClientIP, @EndPoint, @RequestAt, @ResponseAt,
    @RequestBody, @ResponseBody, @StatusCode, @RequestMethod, @IsCancelled
    )
END
```

Example dbo.uspUpdateActivity:

```sql
CREATE PROCEDURE [dbo].[uspUpdateActivity]
    @TraceId NVARCHAR(100),
    @ResponseBody NVARCHAR(MAX),
    @StatusCode INT,
    @ResponseAt DATETIME2(7),
    @IsCancelled BIT
AS
BEGIN
UPDATE [dbo].[Activity]
SET ResponseBody = @ResponseBody,
    StatusCode = @StatusCode,
    ResponseAt = @ResponseAt,
    IsCancelled = @IsCancelled
WHERE TraceId = @TraceId
END
```
Using the IsCancelled Flag
The IsCancelled flag is used to indicate whether an operation was cancelled. This could be due to various reasons such as a timeout, a manual cancellation, or an OperationCanceledException being thrown.