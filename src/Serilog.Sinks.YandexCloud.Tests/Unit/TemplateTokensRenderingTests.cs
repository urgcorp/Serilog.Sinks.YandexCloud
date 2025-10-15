using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Sinks.YandexCloud.Tests.Unit;
public class TemplateTokensRenderingTests
{
    [Test]
    public void SimpleEventsShouldBeRenderedAsTextMessages()
    {
        var messageTemplate = new MessageTemplate([new TextToken("message text")]);

        var serilogEntry = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            messageTemplate, []);

        var yandexEntry = serilogEntry.ToIncomingLogEntry();

        Assert.That(yandexEntry.Message, Is.EqualTo("message text"));
    }

    [Test]
    public void PropertyTokensShouldBeSubstitutedInMessageTextAndAddedToThePayload()
    {
        var messageTemplate = new MessageTemplate("message text with {substitution}", [
            new TextToken("message text with "),
            new PropertyToken("substitution", "{substitution}")
        ]);

        var properties = new[] { new LogEventProperty("substitution", new ScalarValue("substitution value")) };

        var serilogEntry = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            messageTemplate, properties);

        var yandexEntry = serilogEntry.ToIncomingLogEntry();

        Assert.That(yandexEntry.Message, Is.EqualTo("message text with \"substitution value\""));
        Assert.That(yandexEntry.JsonPayload.Fields.ContainsKey("substitution"), Is.True);
        Assert.That(yandexEntry.JsonPayload.Fields["substitution"].StringValue, Is.EqualTo("substitution value"));
    }
}
