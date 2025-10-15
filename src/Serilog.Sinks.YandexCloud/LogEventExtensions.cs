﻿using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Serilog.Events;
using Yandex.Cloud.Logging.V1;

namespace Serilog.Sinks.YandexCloud;

public static class LogEventExtensions
{
    internal static Yandex.Cloud.Logging.V1.LogLevel.Types.Level ToLevel(this LogEvent entry)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        return entry.Level switch
        {
            LogEventLevel.Fatal => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Fatal,
            LogEventLevel.Error => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Error,
            LogEventLevel.Warning => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Warn,
            LogEventLevel.Information => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Info,
            LogEventLevel.Debug => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Debug,
            LogEventLevel.Verbose => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Trace,

            _ => Yandex.Cloud.Logging.V1.LogLevel.Types.Level.Debug
        };

    }

    internal static Value ToValue(this LogEventPropertyValue property)
    {
        switch (property)
        {
            case ScalarValue scalar:
                return scalar.Value switch
                {
                    null => Value.ForNull(),
                    bool b => Value.ForBool(b),
                    byte or sbyte or short or ushort or int or uint or long or ulong => Value.ForNumber(Convert.ToDouble(scalar.Value)),
                    float f => Value.ForNumber(f),
                    double d => Value.ForNumber(d),
                    decimal m => Value.ForNumber((double)m),
                    _ => Value.ForString(scalar.Value.ToString()!)
                };
            case StructureValue structure:
            {
                var @struct = new Struct();
                foreach (var item in structure.Properties)
                    @struct.Fields.Add(item.Name, ToValue(item.Value));
                return Value.ForStruct(@struct);
            }
            case SequenceValue list:
            {
                var listValue = new Value[list.Elements.Count];
                for (int i = 0; i < list.Elements.Count; i++)
                    listValue[i] = ToValue(list.Elements[i]);
                return Value.ForList(listValue);
            }
            case DictionaryValue dv:
            {
                var @struct = new Struct();
                foreach (var item in dv.Elements)
                    @struct.Fields.Add(item.Key.Value?.ToString() ?? "", ToValue(item.Value));
                return Value.ForStruct(@struct);
            }
            default:
                return Value.ForString("");
        }
    }

    internal static IncomingLogEntry ToIncomingLogEntry(this LogEvent entry, IEnumerable<System.Type>? wrapperExceptions = null)
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
            if (kvp.Key == YandexCloudSink.YC_STREAM_NAME_PROPERTY)
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
            foreach (var ex in entry.Exception.StripWrapperExceptions(wrapperExceptions))
            {
                if (ex is not OperationCanceledException && ex.GetBaseException() is not OperationCanceledException)
                    payload.ApplyExceptionDetails(ex);
            }
        }

        if (payload.Fields.Count > 0)
            ycEntry.JsonPayload = payload;
        return ycEntry;
    }

    private static readonly StringSplitOptions StackTraceSplitOptions =
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
    internal static void ApplyExceptionDetails(this Struct payload, Exception exception)
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

    /// <summary>
    /// Returns inner exceptions if <paramref name="exception"/> is any of <paramref name="wrapperExceptionTypes"/>.
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <param name="wrapperExceptionTypes">Exception types to strip.</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static IEnumerable<Exception> StripWrapperExceptions(this Exception exception, IEnumerable<System.Type>? wrapperExceptionTypes)
    {
        if (exception.InnerException != null && wrapperExceptionTypes != null &&
            wrapperExceptionTypes.Any() &&
            wrapperExceptionTypes.Contains(exception.GetType()))
        {
            if (exception is AggregateException ae)
            {
                foreach (var inner in ae.InnerExceptions)
                    foreach (var ex in StripWrapperExceptions(inner, wrapperExceptionTypes))
                        yield return ex;
            }
            else
            {
                foreach (var ex in StripWrapperExceptions(exception.InnerException, wrapperExceptionTypes))
                    yield return ex;
            }
        }
        else
            yield return exception;
    }
}