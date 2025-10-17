# Serilog.Sinks.YandexCloud
Flexible Serilog Sink for Yandex Cloud logging

Generate structured exception details for logged exceptions and unwrap nested exceptions.

## Setup

### Using IAM token file
You can download token file from your Cloud Console and use it directly when configuring

```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    /* ... */
    .WriteTo.YandexCloud("Secrets/key.json", new YandexCloudSinkSettings(), batching =>
    {
        batching.BatchSizeLimit = 200;
        batching.Period = TimeSpan.FromSeconds(10);
    }));
```

### Using appsettings.json:
```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.YandexCloud" ],
    "WriteTo": [
      {
        "Name": "YandexCloud",
        "Args": {
          "Formatter": "Serilog.Formatting.Json.JsonFormatter",
          "FolderId": "<...>",
          "LogGroupId": "<...>",
          "ResourceId": "<...>",
          "ResourceType": "<...>",
          "KeyId": "<...>",
          "ServiceAccountId": "<...>",
          "PrivateKey": "<PRIVATE KEY>"
        }
      }
    ]
  }
}
```

### Separate logger without scoped data
To prevent log context leaking to other parts of the application,
you can have different loggers without using scoped data using the same sink.

```csharp
public interface IAppLogger<out T> : ILogger<T>
{ }

public class AppLoggerAdapter<T> : IAppLogger<T>
{
    private readonly ILogger _logger;

    public AppLoggerAdapter(Serilog.ILogger rootLogger)
    {
        _logger = new SerilogLoggerFactory(rootLogger, dispose: false)
            .CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        using (Serilog.Context.LogContext.Suspend())
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
```

Then register separate configuration

```csharp
var yandexSink = YandexCloudSink.CreateBatchingSink(credentialsProvider, sinkSettings);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.WithProperty("InstanceId", services.GetRequiredService<IInstanceIdProvider>().InstanceId)
        .Enrich.FromLogContext()
        .WriteTo.Sink(yandexSink);
});

builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLoggerAdapter<>));
builder.Services.AddSingleton(sp =>
{
    // Second logger without FromLogContext, using same sink
    var appLoggerConfig = new LoggerConfiguration()
        .Enrich.WithProperty("InstanceId", sp.GetRequiredService<IInstanceIdProvider>().InstanceId)
        .WriteTo.Sink(yandexSink);

    return appLoggerConfig.CreateLogger();
});
```

## Preview

#### Simple
```csharp
Logger.LogInformation($"Test request handle delay is set to {rndDelay} ms.");
```
_Test request handle delay is set to 95 ms._
```json
{
  "ConnectionId": "0HNGC2KE59T58",
  "RequestId": "0HNGC2KE59T58:00000001",
  "RequestPath": "/test"
}
```

#### Exception
```csharp
var ex = new HttpRequestException("Requested Test Exception");
_logger.LogError(exception, "Error processing request after {RequestDurationMs} ms.",
    (long)elapsed.TotalMilliseconds);
/* Handler in Middleware */
```
_Error processing request after 274 ms._
```json
{
  "ConnectionId": "0HNGC2KE59T58",
  "RequestDurationMs": 274,
  "RequestId": "0HNGC2KE59T58:00000001",
  "RequestPath": "/test",
  "exceptions": [
    {
      "message": "Requested Test Exception",
      "stack_trace": [
        "at LoggerApp.TEST.TestRequestHandler.Handle(TestRequest e, CancellationToken cancellationToken) in .../LoggerApp/TEST/TestRequestHandler.cs:line 25",
        "at LoggerApp.TEST.TestMiddleware.InvokeAsync(HttpContext context) in .../LoggerApp/TEST/TestMiddleware.cs:line 49"
      ],
      "type": "System.Net.Http.HttpRequestException"
    }
  ]
}
```