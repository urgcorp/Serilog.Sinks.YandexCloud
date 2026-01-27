using System;
using System.IO;
using Serilog.Configuration;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud.Credentials;
using YandexCloud.IamJwtCredentials;
using IamJwtCredentialsProvider = YandexCloud.IamJwtCredentials.IamJwtCredentialsProvider;

namespace Serilog.Sinks.YandexCloud
{
    public static class YandexCloudLoggerConfigurationExtensions
    {
        /// <summary></summary>
        /// <param name="sinkSettings"></param>
        /// <param name="iamKeyFilePath"></param>
        /// <param name="configureBatching"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">Failed to deserialize IAM token file</exception>
        public static PeriodicBatchingSink CreateYandexCloudSink(this YandexCloudSinkSettings sinkSettings,
            string iamKeyFilePath,
            Action<PeriodicBatchingSinkOptions>? configureBatching = null)
        {
            if (string.IsNullOrWhiteSpace(iamKeyFilePath))
                throw new ArgumentException("Yandex Cloud IAM token path required");

            if (!EnsureIamKeyFileExists(iamKeyFilePath, out var keyPath))
                throw new FileNotFoundException("Yandex Cloud IAM token file not found", iamKeyFilePath);

            var keyJson = File.ReadAllText(keyPath);
            var iamToken = System.Text.Json.JsonSerializer.Deserialize<IamJwtCredentialsConfiguration>(keyJson);
            if (iamToken == null)
                throw new InvalidDataException("IAM token file deserialization failed");

            var credentialsProvider = new IamJwtCredentialsProvider(iamToken);
            return YandexCloudSink.CreateBatchingSink(credentialsProvider, sinkSettings, configureBatching);
        }

        public static LoggerConfiguration YandexCloud(this LoggerSinkConfiguration sinkConfiguration,
            string iamKeyFilePath,
            YandexCloudSinkSettings sinkSettings,
            Action<PeriodicBatchingSinkOptions>? configureBatching = null)
        {
            var sink = sinkSettings.CreateYandexCloudSink(iamKeyFilePath, configureBatching);
            return sinkConfiguration.Sink(sink);
        }

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

        private static bool EnsureIamKeyFileExists(string iamKeyFilePath, out string keyPath)
        {
            if (File.Exists(iamKeyFilePath))
            {
                keyPath = iamKeyFilePath;
                return true;
            }

            if (!Path.IsPathFullyQualified(iamKeyFilePath))
            {
                var fullPath = Path.Combine(AppContext.BaseDirectory, iamKeyFilePath);
                if (File.Exists(fullPath))
                {
                    keyPath = fullPath;
                    return true;
                }
            }

            keyPath = iamKeyFilePath;
            return false;
        }
    }
}