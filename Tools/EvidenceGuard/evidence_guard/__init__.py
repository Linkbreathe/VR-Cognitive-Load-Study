"""Stage 3 engineering demonstrator. No active acquisition or model training."""
from .core import EvidenceGuard
from .contracts import Evidence, Judgment, Proposal, Enactment, GuardConfig

__version__ = "0.1.0"
__all__ = ["EvidenceGuard", "Evidence", "Judgment", "Proposal", "Enactment", "GuardConfig"]
