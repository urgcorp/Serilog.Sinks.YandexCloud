using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.YandexCloud
{
    public class YandexCloudSinkSettings
    {
        private static readonly Regex FieldValidationRegex = new Regex(@"^([a-zA-Z0-9][-a-zA-Z0-9_.]{0,63})?$", RegexOptions.Compiled);

        /// <summary>
        /// <para>Entry destination.</para>
        /// <para>Entry should be written to default log group for the folder.</para>
        /// <para>Includes only one of the fields <see cref="FolderId"/>, <see cref="LogGroupId"/></para>
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// <para>Entry destination.</para>
        /// <para>Entry should be written to log group resolved by ID.</para>
        /// <para>Includes only one of the fields <see cref="FolderId"/>, <see cref="LogGroupId"/></para>
        /// </summary>
        public string? LogGroupId { get; set; }

        /// <summary>
        /// <para>Resource type</para>
        /// <example><c>serverless.function</c></example>
        /// </summary>
        public string? ResourceType { get; set; }

        /// <summary>
        /// <para>Resource ID.</para>
        /// <example>ID of the function producing logs</example>
        /// </summary>
        public string? ResourceId { get; set; }

        /// <summary>
        /// <para>Gets set of outer exceptions that will be stripped, leaving only the valuable inner exception.</para>
        /// <para>This can be used when a wrapper exception, e.g. <see cref="TargetInvocationException"/>,
        /// contains the actual exception as the InnerException.</para>
        /// </summary>
        public HashSet<Type>? WrapperExceptions { get; set; } = new HashSet<Type>() { typeof(TargetInvocationException) };

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
}