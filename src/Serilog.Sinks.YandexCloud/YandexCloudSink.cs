using Google.Protobuf.WellKnownTypes;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud.Logging.V1;
using static Yandex.Cloud.Logging.V1.LogIngestionService;

namespace Serilog.Sinks.YandexCloud;

internal class YandexCloudSink : IBatchedLogEventSink
{
    public const string YC_STREAM_NAME_PROPERTY = "SourceContext";

    private readonly LogIngestionServiceClient _logIngestionService;
    private readonly YandexCloudSinkSettings _settings;

    private readonly Destination _destination;
    private readonly LogEntryResource? _resource;

    public YandexCloudSink(LogIngestionServiceClient logIngestionService, YandexCloudSinkSettings settings)
    {
        _logIngestionService = logIngestionService ?? throw new ArgumentNullException(nameof(logIngestionService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // This values doesn't change until the configuration changes
        _destination = new Destination()
        {
            FolderId = !string.IsNullOrWhiteSpace(_settings.FolderId) ? _settings.FolderId : "",
            LogGroupId = !string.IsNullOrWhiteSpace(_settings.LogGroupId) ? _settings.LogGroupId : ""
        };
        if (!string.IsNullOrWhiteSpace(_settings.ResourceId) || !string.IsNullOrWhiteSpace(_settings.ResourceType))
        {
            _resource = new LogEntryResource()
            {
                Type = !string.IsNullOrWhiteSpace(_settings.ResourceType) ? _settings.ResourceType : null,
                Id = !string.IsNullOrWhiteSpace(_settings.ResourceId) ? _settings.ResourceId : null,
            };
        }
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        try
        {
            var request = new WriteRequest
            {
                Destination = _destination,
                Resource = _resource
            };

            foreach (var entry in batch)
                request.Entries.Add(ToIncomingLogEntry(entry));

            await _logIngestionService.WriteAsync(request)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("[YandexCloudSink] Error while sending log events:\n{0}", ex);
        }
    }

    private IncomingLogEntry ToIncomingLogEntry(LogEvent entry)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        var ycEntry = new IncomingLogEntry
        {
            Level = entry.ToLevel(),
            Timestamp = entry.Timestamp.ToTimestamp(),
            Message = entry.RenderMessage()
        };

        if (entry.Properties.Count == 0 && entry.Exception is null)
            return ycEntry;

        var payload = new Struct();
        foreach (var kvp in entry.Properties)
        {
            if (kvp.Key == YC_STREAM_NAME_PROPERTY)
            {
                ycEntry.StreamName = kvp.Value is ScalarValue sv
                    ? sv.Value?.ToString()
                    : kvp.Value.ToString();
                continue;
            }
            payload.Fields.Add(kvp.Key, kvp.Value.ToValue());
        }

        // Add exception
        if (entry.Exception is not null)
        {
            foreach (var ex in entry.Exception.StripWrapperExceptions(_settings.WrapperExceptions))
            {
                if (ex is not OperationCanceledException && ex.GetBaseException() is not OperationCanceledException)
                    AddExceptionDetails(payload, ex);
            }
        }

        if (payload.Fields.Count > 0)
            ycEntry.JsonPayload = payload;
        return ycEntry;
    }

    private static readonly StringSplitOptions StackTraceSplitOptions =
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
    private void AddExceptionDetails(Struct payload, Exception exception)
    {
        var exValues = new List<Value>();
        var ex = exception;
        while (ex != null)
        {
            Struct exPayload = new()
            {
                Fields =
                {
                    ["type"] = Value.ForString(ex.GetType().FullName),
                    ["message"] = Value.ForString(ex.Message)
                }
            };
            if (ex.StackTrace is { } stackTrace)
            {
                exPayload.Fields["stack_trace"] = Value.ForList(stackTrace
                    .Split('\n', StackTraceSplitOptions)
                    .Select(Value.ForString)
                    .ToArray()
                );
            }

            exValues.Add(Value.ForStruct(exPayload));
            ex = ex.InnerException;
        }
        payload.Fields["exceptions"] = Value.ForList(exValues.ToArray());
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}