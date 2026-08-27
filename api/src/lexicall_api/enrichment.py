# AI enrichment orchestration: composes llm_client + external context
# sources (wiktionary_client, ...) into prompts for each enrichment feature.
from lexicall_api import llm_client, wiktionary_client
from lexicall_api.models.entry import VocabularyEntryType

ENRICHABLE_FIELDS = ("Definition", "Type", "Synonyms", "ExampleSentences")
# PascalCase (matches VocabularyEntry.LockedFields entries / JSON aliases) ->
# snake_case (matches the JSON schema sent to the LLM and the response dict
# key expected by EntryEnrichmentSuggestions).
_FIELD_SCHEMA_KEYS = {
    "Definition": "definition",
    "Type": "type",
    "Synonyms": "synonyms",
    "ExampleSentences": "example_sentences",
}

ENTRY_ENRICHMENT_INSTRUCTIONS = (
    "Tu proposes des améliorations aux champs d'une entrée de vocabulaire "
    "pour l'application LexiCall : Définition, Type grammatical, Synonymes, "
    "Exemples. Pour chaque champ présent dans le schéma de sortie, décide "
    "s'il mérite une suggestion. Règle par défaut : reste conservateur et "
    "paresseux. Un champ vide reçoit toujours une proposition. Un champ non "
    "vide ne doit être retouché que si c'est réellement justifié : faute, "
    "ponctuation manquante, sens manifestement absent — jamais une simple "
    "reformulation stylistique d'un contenu déjà correct et complet. En cas "
    "de doute, ne propose rien pour ce champ (laisse sa valeur à null). "
    "Quand tu proposes une valeur pour un champ non vide, inclus toujours une "
    "courte justification. Pour un champ vide, la justification peut rester "
    "vide. La définition ne doit jamais mentionner la nature grammaticale du "
    "mot. Aucun champ de la réponse (définition, justification, synonymes, "
    "exemples) ne doit jamais contenir de lien ni de balisage markdown (pas "
    "de \"[texte](url)\") ni de mention explicite d'une source : écris "
    "uniquement du texte brut partout, y compris quand tu t'appuies sur la "
    "recherche web. Si un contexte est fourni, appuie-toi dessus. Si aucun "
    "contexte n'est fourni, utilise la recherche web pour te documenter "
    "avant de répondre."
)


def suggest_entry_enrichment(entry: dict) -> dict:
    locked = set(entry.get("LockedFields", []))
    unlocked = [f for f in ENRICHABLE_FIELDS if f not in locked]
    if not unlocked:
        return {}

    word = entry["Word"]
    context = wiktionary_client.fetch_definition_context(word)
    prompt = _build_entry_enrichment_prompt(entry, unlocked, context)
    schema = _build_entry_enrichment_schema(unlocked)
    # No Wiktionary context to ground the answer: force a real web search
    # rather than letting the model silently fall back to internal memory
    # alone (tool_choice="auto" doesn't reliably trigger it).
    tools = None if context is not None else [{"type": "web_search"}]
    tool_choice = None if context is not None else "required"
    return llm_client.generate_structured(
        prompt,
        schema_name="entry_enrichment",
        json_schema=schema,
        instructions=ENTRY_ENRICHMENT_INSTRUCTIONS,
        tools=tools,
        tool_choice=tool_choice,
    )


_CURRENT_VALUE_LABELS = {
    "Definition": "Définition actuelle",
    "Type": "Type actuel",
    "Synonyms": "Synonymes actuels",
    "ExampleSentences": "Exemples actuels",
}


def _current_value_text(entry: dict, field: str) -> str:
    value = entry.get(field)
    if field in ("Synonyms", "ExampleSentences"):
        return ", ".join(value) if value else "aucun"
    return value if value else "vide"


def _build_entry_enrichment_prompt(entry: dict, unlocked: list[str], context: str | None) -> str:
    lines = [f"Mot : {entry['Word']}"]
    for field in unlocked:
        lines.append(f"{_CURRENT_VALUE_LABELS[field]} : {_current_value_text(entry, field)}")
    if context is not None:
        lines.append(f"Contexte (wikitext brut du Wiktionnaire) :\n{context}")
    else:
        lines.append("Aucun contexte Wiktionnaire disponible.")
    return "\n".join(lines)


def _field_value_schema(field: str) -> dict:
    if field == "Type":
        return {
            "type": "string",
            "enum": [t.value for t in VocabularyEntryType if t != VocabularyEntryType.UNDEFINED],
        }
    if field in ("Synonyms", "ExampleSentences"):
        return {"type": "array", "items": {"type": "string"}}
    return {"type": "string"}  # Definition


def _build_entry_enrichment_schema(unlocked: list[str]) -> dict:
    properties = {}
    for field in unlocked:
        key = _FIELD_SCHEMA_KEYS[field]
        properties[key] = {
            "anyOf": [
                {
                    "type": "object",
                    "properties": {
                        "value": _field_value_schema(field),
                        "justification": {"type": ["string", "null"]},
                    },
                    "required": ["value", "justification"],
                    "additionalProperties": False,
                },
                {"type": "null"},
            ]
        }
    return {
        "type": "object",
        "properties": properties,
        "required": list(properties.keys()),
        "additionalProperties": False,
    }
