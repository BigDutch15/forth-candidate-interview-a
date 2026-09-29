import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * Trivial stand-in for a Kafka producer. Hand this to the candidate as-is, or
 * let them write their own equivalent in a couple of minutes if they'd rather.
 */
public class MockProducer {

    public record Message(String topic, String key, Map<String, Object> value) {}

    private final List<Message> log = new ArrayList<>();

    public void publish(String topic, String key, Map<String, Object> value) {
        log.add(new Message(topic, key, value));
    }

    public List<Message> messages() {
        return new ArrayList<>(log);
    }

    public List<Message> messages(String topic) {
        return log.stream().filter(m -> m.topic().equals(topic)).collect(Collectors.toList());
    }
}
