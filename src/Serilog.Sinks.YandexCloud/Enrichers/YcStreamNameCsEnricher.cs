using System;
using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.YandexCloud.Enrichers
{
    /// <summary>
    /// Enriches log events with a valid Yandex Cloud Logging "Stream Name" property.
    /// </summary>
    /// <remarks>
    /// In .NET, the logging category name (typically the full class name) is passed 
    /// via the "SourceContext" property. Yandex Cloud Logging has a character limit (<see cref="YandexCloudSink.StreamNameMaxLength"/>)
    /// for the StreamName field and rejects longer values with an InvalidArgument error.
    /// <br/>
    /// This enricher extracts the short class name from the "SourceContext" 
    /// (e.g., <c>Namespace.SubNamespace.MyClass</c> -> <c>MyClass</c>), ensures it doesn't 
    /// exceed characters limit, and caches the result for high performance.
    /// </remarks>
    public class YcStreamNameCsEnricher : ILogEventEnricher
    {
        public const string LoggerCategoryPropertyName = "SourceContext";

        // Кэшируем результат парсинга для каждого SourceContext
        private static readonly ConcurrentDictionary<string, string> _nameCache = new ConcurrentDictionary<string, string>();

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // StreamName уже задан вручную (например, через LogContext)
            if (logEvent.Properties.ContainsKey(YandexCloudSink.YC_STREAM_NAME_PROPERTY))
                return;

            if (logEvent.Properties.TryGetValue(LoggerCategoryPropertyName, out var value) && 
                value is ScalarValue { Value: string fullName })
            {
                var shortName = _nameCache.GetOrAdd(fullName, _shorten);
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(YandexCloudSink.YC_STREAM_NAME_PROPERTY, shortName));
            }
        }

        private static string _shorten(string fullName)
        {
            var span = fullName.AsSpan();
            var lastDot = fullName.LastIndexOf('.');
            if (lastDot >= 0)
                span = span[(lastDot + 1)..];

            if (span.Length > YandexCloudSink.StreamNameMaxLength)
            {
                span = span[..YandexCloudSink.StreamNameMaxLength];
            }

            return span.ToString();
        }
    }
}