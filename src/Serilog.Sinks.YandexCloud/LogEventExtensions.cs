using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Serilog.Events;

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

    /// <summary>
    /// Returns inner exceptions if <paramref name="exception"/> is any of <paramref name="wrapperExceptionTypes"/>.
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <param name="wrapperExceptionTypes">Exception types to strip.</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal static IEnumerable<Exception> StripWrapperExceptions(this Exception exception, IEnumerable<System.Type> wrapperExceptionTypes)
    {
        if (exception.InnerException != null && wrapperExceptionTypes.Contains(exception.GetType()))
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