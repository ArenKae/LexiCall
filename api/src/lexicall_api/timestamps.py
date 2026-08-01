# Horodatage partagé par les repositories (comparaisons CAS) et les routeurs
# (en-tête X-Sync-Timestamp) : centralise le format pour qu'il reste identique
# partout où deux timestamps sont comparés.
from datetime import datetime, timezone


def now_iso() -> str:
    # timespec="microseconds" force la présence de la partie fractionnaire
    # (absente quand microseconds==0), sinon deux timestamps de la même
    # seconde se compareraient mal lexicographiquement dans un filtre CAS.
    return datetime.now(timezone.utc).isoformat(timespec="microseconds")


def to_iso_utc(value: datetime | None) -> str | None:
    # Normalise un datetime client (offset non-UTC éventuel) vers le même
    # format que now_iso(), pour que la comparaison lexicographique Mongo
    # reflète l'ordre chronologique réel.
    if value is None:
        return None
    return value.astimezone(timezone.utc).isoformat(timespec="microseconds")
