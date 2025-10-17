using Serilog.Configuration;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud.Credentials;
using YandexCloud.IamJwtCredentials;
using IamJwtCredentialsProvider = YandexCloud.IamJwtCredentials.IamJwtCredentialsProvider;

namespace Serilog.Sinks.YandexCloud;

public static class YandexCloudLoggerConfigurationExtensions
{
    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        ICredentialsProvider credentialsProvider,
        YandexCloudSinkSettings sinkSettings,
        PeriodicBatchingSinkOptions? batchOptions = null)
    {
        var batchingSink = YandexCloudSink.CreateBatchingSink(credentialsProvider, sinkSettings, batchOptions);
        return sinkConfiguration.Sink(batchingSink);
    }

    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        ICredentialsProvider credentialsProvider,
        YandexCloudSinkSettings sinkSettings,
        Action<PeriodicBatchingSinkOptions>? configureBatching)
    {
        var batchingSink = YandexCloudSink.CreateBatchingSink(credentialsProvider, sinkSettings, configureBatching);
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
        PeriodicBatchingSinkOptions? batchOptions = null)
    {
        var credentials = new IamJwtCredentialsConfiguration 
        {
            Id = keyId,
            ServiceAccountId = serviceAccountId,
            PrivateKey = privateKey
        };
        var credentialsProvider = new IamJwtCredentialsProvider(credentials);

        var settings = new YandexCloudSinkSettings()
        {
            FolderId = folderId,
            LogGroupId = logGroupId,
            ResourceId = resourceId,
            ResourceType = resourceType,
        };
        return sinkConfiguration.YandexCloud(credentialsProvider, settings, batchOptions);
    }

    public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
        string iamKeyFilePath,
        YandexCloudSinkSettings sinkSettings,
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
        return sinkConfiguration.YandexCloud(credentialsProvider, sinkSettings, configureBatching);
    }
}