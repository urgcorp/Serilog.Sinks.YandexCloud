using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Yandex.Cloud;
using Yandex.Cloud.Credentials;
using Yandex.Cloud.Logging.V1;
using static Yandex.Cloud.Logging.V1.LogIngestionService;

namespace Serilog.Sinks.YandexCloud
{
    public class YandexCloudSink : IBatchedLogEventSink
    {
        #region Yandex Cloud defaults for PeriodicBatchingSinkOptions
        /// <summary>
        /// <see cref="PeriodicBatchingSinkOptions.BatchSizeLimit"/>
        /// </summary>
        public const int DefaultBatchSizeLimit = 100;

        /// <summary>
        /// <see cref="PeriodicBatchingSinkOptions.Period"/>
        /// </summary>
        public const int DefaultBatchPeriodMs = 2000;

        /// <summary>
        /// <see cref="PeriodicBatchingSinkOptions.QueueLimit"/>
        /// </summary>
        public const int DefaultBatchQueueLimit = 1000;

        /// <summary>
        /// <see cref="PeriodicBatchingSinkOptions.EagerlyEmitFirstEvent"/>
        /// </summary>
        public const bool DefaultBatchEagerlyEmitFirstEvent = true;    
        #endregion

        /// <summary>
        /// Name of <see cref="LogEvent"/> property key that represent <see cref="IncomingLogEntry.StreamName"/>
        /// </summary>
        public const string YC_STREAM_NAME_PROPERTY = "___YC_STREAM_NAME___";

        /// <summary>
        /// <para>Max length for Yandex Cloud log events properties: Resource Type, ResourceId and Stream Name</para>
        /// <remarks></remarks>
        /// </summary>
        public static int ResourcePropertyMaxLength = 63;

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
                FolderId = !string.IsNullOrWhiteSpace(_settings.FolderId) ? _settings.FolderId : string.Empty,
                LogGroupId = !string.IsNullOrWhiteSpace(_settings.LogGroupId) ? _settings.LogGroupId : string.Empty
            };

            // Type and Id can't be null
            _resource = new LogEntryResource()
            {
                Type = _settings.ResourceType ?? string.Empty,
                Id = _settings.ResourceId ?? string.Empty
            };
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

        /// <summary>
        /// Creates <see cref="PeriodicBatchingSinkOptions"/> using Yandex Cloud Sink defaults
        /// </summary>
        public static PeriodicBatchingSinkOptions CreateDefaultBatchOptions()
        {
            return new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = DefaultBatchSizeLimit,
                Period = TimeSpan.FromMilliseconds(DefaultBatchPeriodMs),
                QueueLimit = DefaultBatchQueueLimit,
                EagerlyEmitFirstEvent = DefaultBatchEagerlyEmitFirstEvent
            };
        }

        /// <summary>
        /// Creates <see cref="PeriodicBatchingSinkOptions"/> using Yandex Cloud Sink defaults
        /// </summary>
        /// <param name="configureBatching">Configure defaults</param>
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
            var batchingOptions = CreateBatchOptions(configureBatching: configureBatching);
            return CreateBatchingSink(credentialsProvider, sinkSettings, batchingOptions);
        }
    }
}