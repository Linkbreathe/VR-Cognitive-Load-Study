"""Host-neutral contracts; times use one session monotonic clock, in seconds."""
from dataclasses import dataclass, field, asdict
import math


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


@dataclass(frozen=True)
class GuardConfig:
    revision: str = "evidence-guard-v3-minimal-1"
    min_gaze_valid: float = .70
    min_task_gaze: float = .60
    # Broad engineering association ranges, not validated physiological latencies.
    response_delays: dict = field(default_factory=lambda: {"rf": [0., 10.], "eeg": [0., 5.], "behavior": [0., 4.5]})
    max_sample_seconds: float = .10
    max_evidence_age: float = 30.
    movie_reference: float = 0.
    require_task_view_for_challenge: bool = True

    def __post_init__(self):
        for name in ("min_gaze_valid", "min_task_gaze", "movie_reference"):
            if not finite(getattr(self, name)) or not 0 <= getattr(self, name) <= 1:
                raise ValueError(name)
        if not finite(self.max_sample_seconds) or self.max_sample_seconds <= 0 or not finite(self.max_evidence_age) or self.max_evidence_age <= 0:
            raise ValueError("Invalid duration")
        for key in ("rf", "eeg", "behavior"):
            pair = self.response_delays[key]
            if len(pair) != 2 or not all(finite(x) for x in pair) or not 0 <= pair[0] <= pair[1]:
                raise ValueError("Invalid response delay")


@dataclass(frozen=True)
class Evidence:
    id: str
    kind: str
    session: str
    start: float
    end: float
    source: str
    level: int
    scene: str
    quality: bool
    data: dict = field(default_factory=dict)
    epoch: int = 0

    def __post_init__(self):
        if not self.id or not self.session or not self.source or not self.scene:
            raise ValueError("Evidence provenance required")
        if not finite(self.start) or not finite(self.end) or self.end < self.start or self.level not in (-1, 0, 1):
            raise ValueError("Invalid evidence interval or condition")
        if type(self.quality) is not bool:
            raise ValueError("Quality must be explicit")


@dataclass(frozen=True)
class Judgment:
    id: str
    kind: str
    evidence_ids: tuple
    level: int
    scene: str
    requires_view: bool = False
    relation_id: str = ""
    # Explicit comparability supplied by a host protocol, never inferred from time.
    comparable: bool = False


@dataclass(frozen=True)
class Proposal:
    id: str
    session: str
    time: float
    action: str
    level: int
    scene: str
    legal: bool
    # Alternative authorization paths; every judgment in one path must hold.
    paths: tuple = ()
    basis: str = ""


@dataclass(frozen=True)
class Enactment:
    id: str
    request_id: str
    session: str
    time: float
    previous_level: int
    level: int
    scene: str
    actual: bool = True


def record(value):
    return asdict(value)
