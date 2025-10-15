using System.Reflection;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.YandexCloud;

public class YandexCloudSinkSettings
{
    private static readonly Regex FieldValidationRegex = new (@"^([a-zA-Z0-9][-a-zA-Z0-9_.]{0,63})?$", RegexOptions.Compiled);

    public string? FolderId { get; set; }
    public string? LogGroupId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }

    private HashSet<System.Type>? _wrapperExceptions = [typeof(TargetInvocationException)];

    /// <summary>
    /// Gets set of outer exceptions that will be stripped, leaving only the valuable inner exception.
    /// This can be used when a wrapper exception, e.g. <see cref="TargetInvocationException"/>, contains the actual
    /// exception as the InnerException.
    /// </summary>
    public HashSet<System.Type> WrapperExceptions
    {
        get => _wrapperExceptions ??= [];
        set => _wrapperExceptions = value;
    }

    public void Validate()
    {
        if (!string.IsNullOrEmpty(FolderId) && !string.IsNullOrEmpty(LogGroupId))
            throw new ArgumentException($"{nameof(FolderId)} and {nameof(LogGroupId)} parameters can't be specified together.");

        if (string.IsNullOrEmpty(FolderId) && string.IsNullOrEmpty(LogGroupId))
            throw new ArgumentException($"One of {nameof(FolderId)} or {nameof(LogGroupId)} parameters arguments is required.");

        if (!string.IsNullOrEmpty(ResourceType) && !FieldValidationRegex.IsMatch(ResourceType))
            throw new ArgumentException($"{nameof(ResourceType)} is in incorrect format.");

        if (!string.IsNullOrEmpty(ResourceId) && !FieldValidationRegex.IsMatch(ResourceId))
            throw new ArgumentException($"{nameof(ResourceId)} is in incorrect format.");

        if (!string.IsNullOrEmpty(FolderId) && !FieldValidationRegex.IsMatch(FolderId))
            throw new ArgumentException($"{nameof(FolderId)} is in incorrect format.");

        if (!string.IsNullOrEmpty(LogGroupId) && !FieldValidationRegex.IsMatch(LogGroupId))
            throw new ArgumentException($"{nameof(FolderId)} is in incorrect format.");
    }
}