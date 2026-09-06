# Turns each entry's Definition from a single string into a list of senses.
# Run from api/ with:
#   PYTHONPATH=src .venv/bin/python -m lexicall_api.migration.split_definitions [--dry-run]
# Only splits on newlines the user wrote themselves — never on punctuation,
# which on this corpus separates a rephrasing far more often than a real
# sense. Numbered prefixes ("1. ", "2. ") are dropped since the list index
# replaces them. Idempotent: a Definition already stored as a list is left
# alone. The summary also lists entries that stayed single-sense but look
# multi-sense, for manual follow-up.
import argparse
import re
from dataclasses import dataclass, field

from lexicall_api.database import get_entries_collection, strip_mongo_id

_NUMBERED_PREFIX_RE = re.compile(r"^\d+\s*[.)]\s*")

# Weak signals only: they flag an entry for a human to look at, never a split.
_CANDIDATE_SIGNALS = (
    ("par extension", re.compile(r"\bpar extension\b", re.IGNORECASE)),
    ("au figuré", re.compile(r"\bfigur[ée]\b", re.IGNORECASE)),
    ("se dit aussi", re.compile(r"\b(?:se dit|désigne|signifie|s'emploie)\s+(?:aussi|également|encore)\b", re.IGNORECASE)),
    ("point-virgule", re.compile(r"\s;\s")),
)


@dataclass
class SplitSummary:
    converted: int = 0
    already_list: int = 0
    multi_sense: list[str] = field(default_factory=list)
    candidates: list[str] = field(default_factory=list)

    def render(self) -> str:
        lines = [
            "Split summary:",
            f"  Definitions converted to a list: {self.converted}",
            f"  Already a list (skipped): {self.already_list}",
            f"  Split into several senses: {len(self.multi_sense)}",
        ]
        lines.extend(f"    - {word}" for word in self.multi_sense)
        lines.append(f"  Still single-sense but worth a manual look: {len(self.candidates)}")
        lines.extend(f"    - {word}" for word in self.candidates)
        return "\n".join(lines)


def split_definition(definition: str) -> list[str]:
    senses = [_NUMBERED_PREFIX_RE.sub("", line).strip() for line in definition.split("\n")]
    return [sense for sense in senses if sense]


def run(dry_run: bool = False) -> SplitSummary:
    summary = SplitSummary()
    collection = get_entries_collection()

    for doc in [strip_mongo_id(doc) for doc in collection.find({})]:
        definition = doc.get("Definition")
        if isinstance(definition, list):
            summary.already_list += 1
            continue

        senses = split_definition(definition or "")
        summary.converted += 1
        if len(senses) > 1:
            summary.multi_sense.append(doc["Word"])
        elif any(pattern.search(definition or "") for _label, pattern in _CANDIDATE_SIGNALS):
            summary.candidates.append(doc["Word"])

        if not dry_run:
            # UpdatedAt is deliberately untouched: this is a schema change,
            # not a user edit, and bumping it would replay all 240 entries
            # down to the client and falsify the Last-Write-Wins ordering.
            collection.update_one({"Id": doc["Id"]}, {"$set": {"Definition": senses}})

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Converts every entry's Definition from a string to a list of senses."
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Counts and reports without writing anything.",
    )
    args = parser.parse_args()

    summary = run(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
