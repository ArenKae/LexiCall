# Timestamp formatting shared by the repositories (CAS comparisons) and the
# routers (X-Sync-Timestamp header): centralizes the format so it stays
# identical everywhere two timestamps are compared.
from datetime import datetime, timezone


def now_iso() -> str:
    # timespec="microseconds" forces the fractional part to always be present
    # (absent when microseconds==0), otherwise two timestamps within the same
    # second would compare incorrectly lexicographically in a CAS filter.
    return datetime.now(timezone.utc).isoformat(timespec="microseconds")


def to_iso_utc(value: datetime | None) -> str | None:
    # Normalizes a client datetime (possibly non-UTC offset) to the same
    # format as now_iso(), so Mongo's lexicographic comparison reflects the
    # actual chronological order.
    if value is None:
        return None
    return value.astimezone(timezone.utc).isoformat(timespec="microseconds")
