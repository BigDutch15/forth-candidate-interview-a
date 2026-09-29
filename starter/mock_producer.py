"""
Trivial stand-in for a Kafka producer. Hand this to the candidate as-is, or let
them write their own equivalent in a couple of minutes if they'd rather.
"""
from dataclasses import dataclass, field
from typing import Any


@dataclass
class Message:
    topic: str
    key: str
    value: dict[str, Any]


class MockProducer:
    def __init__(self) -> None:
        self._log: list[Message] = []

    def publish(self, topic: str, key: str, value: dict[str, Any]) -> None:
        self._log.append(Message(topic=topic, key=key, value=value))

    def messages(self, topic: str | None = None) -> list[Message]:
        if topic is None:
            return list(self._log)
        return [m for m in self._log if m.topic == topic]
