# Serilog.Sinks.YandexCloud
Flexible Serilog Sink for Yandex Cloud logging

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

## Examples

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