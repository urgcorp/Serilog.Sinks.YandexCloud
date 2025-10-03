using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Sinks.YandexCloud.Tests.Unit;
public class TemplateTokensRenderingTests
{
    [Test]
    public void SimpleEventsShouldBeRenderedAsTextMessages()
    {
        var messageTemplate = new MessageTemplate(new[] { new TextToken("message text") });

        var serilogEntry = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            messageTemplate, Array.Empty<LogEventProperty>());

        var yandexEntry = serilogEntry.ToIncomingLogEntry();

        Assert.That(yandexEntry.Message, Is.EqualTo("message text"));
    }

    [Test]
    public void PropertyTokensShouldBeSubstitutedInMessageTextAndAddedToThePayload()
    {
        var messageTemplate = new MessageTemplate("message text with {substitution}", new MessageTemplateToken[]
        {
            new TextToken("message text with "),
            new PropertyToken("substitution", "substitution value", startIndex: 18)
        });

        var serilogEntry = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            messageTemplate, Array.Empty<LogEventProperty>());

        var yandexEntry = serilogEntry.ToIncomingLogEntry();

        Assert.That(yandexEntry.Message, Is.EqualTo("message text with substitution value"));
    }
}
