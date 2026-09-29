using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Trivial stand-in for a Kafka producer. Hand this to the candidate as-is, or
/// let them write their own equivalent in a couple of minutes if they'd rather.
/// </summary>
public record Message(string Topic, string Key, IReadOnlyDictionary<string, object?> Value);

public class MockProducer
{
    private readonly List<Message> _log = new();

    public void Publish(string topic, string key, IReadOnlyDictionary<string, object?> value)
    {
        _log.Add(new Message(topic, key, value));
    }

    public IReadOnlyList<Message> Messages(string? topic = null)
    {
        return topic is null
            ? _log.ToList()
            : _log.Where(m => m.Topic == topic).ToList();
    }
}
