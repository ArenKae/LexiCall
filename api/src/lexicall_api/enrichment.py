# AI enrichment orchestration: composes llm_client + external context
# sources (wiktionary_client, ...) into prompts for each enrichment feature.
from lexicall_api import llm_client, wiktionary_client
from lexicall_api.models.entry import VocabularyEntryType

DEFINITION_INSTRUCTIONS = (
    "Tu rédiges des définitions de dictionnaire en français pour l'application "
    "LexiCall. Style attendu : une définition concise et précise, dans le "
    "style d'un dictionnaire classique (une phrase ou une courte liste de "
    "sens), jamais une explication développée. Ne mentionne jamais la nature "
    "grammaticale du mot dans le texte de la définition (pas de \"(nom "
    "masculin)\", \"(verbe)\", etc.) : le champ `type` sert exactement à ça, "
    "garde-le hors du texte. Si un contexte est fourni, appuie-toi dessus. Si "
    "aucun contexte n'est fourni, réponds du mieux que tu peux à partir de "
    "tes connaissances."
)

DEFINITION_SCHEMA = {
    "type": "object",
    "properties": {
        "definition": {"type": "string"},
        "type": {
            "type": "string",
            "enum": [t.value for t in VocabularyEntryType if t != VocabularyEntryType.UNDEFINED],
        },
    },
    "required": ["definition", "type"],
    "additionalProperties": False,
}


def suggest_definition(word: str) -> dict:
    context = wiktionary_client.fetch_definition_context(word)
    prompt = _build_definition_prompt(word, context)
    return llm_client.generate_structured(
        prompt,
        schema_name="definition_suggestion",
        json_schema=DEFINITION_SCHEMA,
        instructions=DEFINITION_INSTRUCTIONS,
    )


def _build_definition_prompt(word: str, context: str | None) -> str:
    if context is None:
        return f"Mot : {word}\nAucun contexte disponible."
    return f"Mot : {word}\nContexte (wikitext brut du Wiktionnaire) :\n{context}"
