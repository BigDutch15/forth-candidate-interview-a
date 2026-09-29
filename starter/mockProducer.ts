/**
 * Trivial stand-in for a Kafka producer. Hand this to the candidate as-is, or
 * let them write their own equivalent in a couple of minutes if they'd rather.
 */
export interface Message {
  topic: string;
  key: string;
  value: Record<string, unknown>;
}

export class MockProducer {
  private log: Message[] = [];

  publish(topic: string, key: string, value: Record<string, unknown>): void {
    this.log.push({ topic, key, value });
  }

  messages(topic?: string): Message[] {
    if (!topic) return [...this.log];
    return this.log.filter((m) => m.topic === topic);
  }
}
