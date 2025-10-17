using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud;
using Yandex.Cloud.Credentials;
using Yandex.Cloud.Logging.V1;
using static Yandex.Cloud.Logging.V1.LogIngestionService;

namespace Serilog.Sinks.YandexCloud;

public class YandexCloudSink : IBatchedLogEventSink
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

    public YandexCloudSink(Sdk sdk, YandexCloudSinkSettings settings) : this(sdk.Services.Logging.LogIngestionService, settings)
    { }

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
                request.Entries.Add(entry.ToIncomingLogEntry(_settings.WrapperExceptions));

            await _logIngestionService.WriteAsync(request)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("[YandexCloudSink] Error while sending log events:\n{0}", ex);
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    public static PeriodicBatchingSinkOptions CreateDefaultBatchOptions()
    {
        return new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = 100,
            Period = TimeSpan.FromSeconds(2),
            QueueLimit = 1000,
            EagerlyEmitFirstEvent = true
        };
    }

    public static PeriodicBatchingSinkOptions CreateBatchOptions(Action<PeriodicBatchingSinkOptions>? configureBatching)
    {
        var batchingOptions = CreateDefaultBatchOptions();
        configureBatching?.Invoke(batchingOptions);
        return batchingOptions;
    }

    public static PeriodicBatchingSink CreateBatchingSink(
        ICredentialsProvider credentialsProvider,
        YandexCloudSinkSettings sinkSettings,
        PeriodicBatchingSinkOptions? batchOptions = null)
    {
        var sdk = new Sdk(credentialsProvider);
        sinkSettings.Validate();

        var sink = new YandexCloudSink(sdk, sinkSettings);
        return new PeriodicBatchingSink(sink, batchOptions ?? CreateDefaultBatchOptions());
    }

    public static PeriodicBatchingSink CreateBatchingSink(
        ICredentialsProvider credentialsProvider,
        YandexCloudSinkSettings sinkSettings,
        Action<PeriodicBatchingSinkOptions>? configureBatching)
    {
        var batchingOptions = CreateBatchOptions(configureBatching);
        return CreateBatchingSink(credentialsProvider, sinkSettings, batchingOptions);
    }
}