using Serilog.Configuration;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud;
using Yandex.Cloud.Credentials;
using YandexCloud.IamJwtCredentials;
using IamJwtCredentialsProvider = YandexCloud.IamJwtCredentials.IamJwtCredentialsProvider;

namespace Serilog.Sinks.YandexCloud;

public static class YandexCloudLoggerConfigurationExtensions
{
    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        ICredentialsProvider credentialsProvider,
        Action<YandexCloudSinkSettings> configureSink,
        Action<PeriodicBatchingSinkOptions>? configureBatching = null)
    {
        var sdk = new Sdk(credentialsProvider);

        var settings = new YandexCloudSinkSettings();
        configureSink.Invoke(settings);

        settings.Validate();

        var sink = new YandexCloudSink(sdk.Services.Logging.LogIngestionService, settings);

        var batchingOptions = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = 100,
            Period = TimeSpan.FromSeconds(2),
            QueueLimit = 1000,
            EagerlyEmitFirstEvent = true
        };
        configureBatching?.Invoke(batchingOptions);

        var batchingSink = new PeriodicBatchingSink(sink, batchingOptions);

        return sinkConfiguration.Sink(batchingSink);
    }

    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        string keyId,
        string serviceAccountId,
        string privateKey,
        string? folderId = null,
        string? logGroupId = null,
        string? resourceId = null,
        string? resourceType = null,
        int batchSizeLimit = 100,
        int periodMs = 2000,
        int queueLimit = 1000,
        bool eagerlyEmitFirstEvent = true)
    {
        var credentials = new IamJwtCredentialsConfiguration 
        {
            Id = keyId,
            ServiceAccountId = serviceAccountId,
            PrivateKey = privateKey
        };
        var credentialsProvider = new IamJwtCredentialsProvider(credentials);

        return sinkConfiguration.YandexCloud(
            credentialsProvider, settings =>
            {
                settings.FolderId = folderId;
                settings.LogGroupId = logGroupId;
                settings.ResourceId = resourceId;
                settings.ResourceType = resourceType;
            },
            batching =>
            {
                batching.BatchSizeLimit = batchSizeLimit;
                batching.Period = TimeSpan.FromMilliseconds(periodMs);
                batching.QueueLimit = queueLimit;
                batching.EagerlyEmitFirstEvent = eagerlyEmitFirstEvent;
            });
    }

    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        string iamKeyFilePath,
        Action<YandexCloudSinkSettings> configureSink,
        Action<PeriodicBatchingSinkOptions>? configureBatching = null)
    {
        string keyPath = iamKeyFilePath;
        bool keyExists = File.Exists(keyPath);
        if (!keyExists && !Path.IsPathFullyQualified(keyPath))
        {
            keyPath = Path.Combine(AppContext.BaseDirectory, keyPath);
            keyExists = File.Exists(keyPath);
        }
        if (!keyExists)
            throw new FileNotFoundException("Logging IAM token file not found", iamKeyFilePath);

        var keyJson = File.ReadAllText(iamKeyFilePath);
        var credentials = System.Text.Json.JsonSerializer.Deserialize<IamJwtCredentialsConfiguration>(keyJson);
        if (credentials == null)
            throw new ApplicationException("IAM token file format error");

        var credentialsProvider = new IamJwtCredentialsProvider(credentials);
        return sinkConfiguration.YandexCloud(credentialsProvider, configureSink, configureBatching);
    }
}