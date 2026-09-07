# AI enrichment orchestration: composes llm_client + external context
# sources (wiktionary_client, embeddings, ...) into prompts for each
# enrichment feature.
from lexicall_api import embeddings, llm_client, wiktionary_client
from lexicall_api.models.entry import VocabularyEntryType
from lexicall_api.repositories import categories_repo, category_embeddings_repo

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
    "Avant toute proposition, vérifie que « Mot » est un mot ou une "
    "expression française réelle et attestée — pas une suite de caractères "
    "aléatoire, un mot inventé, ou une faute de frappe non confirmée. Si le "
    "contexte fourni ne porte pas sur ce mot précis et que tu n'as pas de "
    "certitude raisonnable de son existence réelle (y compris après "
    "recherche web), réponds word_recognized=false et laisse tous les "
    "autres champs à null : ne te rabats jamais sur un mot proche qui "
    "existe pour combler l'absence de résultat, ce serait présenter une "
    "supposition comme un fait. Sinon, réponds word_recognized=true et "
    "poursuis normalement. "
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
    "vide. "
    "RÈGLES IMPÉRATIVES POUR LA DÉFINITION, une liste de sens : "
    "(1) UN SEUL sens est le cas normal, c'est ce que tu dois renvoyer par "
    "défaut. N'ajoute un élément supplémentaire que si le mot a "
    "vraiment des acceptions sans rapport entre elles. "
    "(2) CINQ ÉLÉMENTS MAXIMUM, jamais six, quel que soit le nombre de "
    "sens attestés ailleurs : choisis les plus utiles et ignore le reste. "
    "Ce plafond est une limite haute pour les mots vraiment polysémiques, "
    "pas un objectif à atteindre — la plupart des mots restent à un seul "
    "élément. "
    "(3) Ne mets jamais deux éléments qui disent la même chose autrement : "
    "une reformulation, une généralisation ou une nuance d'un élément déjà "
    "présent n'est pas un sens de plus — c'est le défaut à éviter avant tout, "
    "celui des dictionnaires qui éclatent un même usage en variantes. "
    "TEST À APPLIQUER AVANT CHAQUE ÉLÉMENT SUPPLÉMENTAIRE : un lecteur qui a "
    "compris les éléments déjà écrits saurait-il déjà interpréter cet "
    "emploi-là ? Si oui, ne l'ajoute pas. "
    "Comptent pour UN SEUL élément, à fusionner en une seule formulation : "
    "deux nuances d'une même idée (« d'un rouge éclatant » et « qui brille "
    "d'un vif éclat ») ; une formulation et sa généralisation (« pluie très "
    "fine » et « tout ce qui tombe en fines gouttes ») ; un cas particulier "
    "d'un emploi déjà donné (« se déplacer dans les airs » et « piloter un "
    "avion ») ; l'adjectif et le nom correspondant ; un verbe et sa forme "
    "pronominale ; un nom et l'adjectif, la couleur ou le verbe qui en "
    "dérive ; un sens propre et son extension figurée immédiate. "
    "(4) Classe les sens du plus courant au plus rare, pour que ce soit "
    "toujours le plus marginal qui saute si tu dois t'arrêter. Un sens "
    "spécialisé mais réel a sa place (le canon en musique, le point de "
    "couture), tout comme les sens littéraires ou vieillis qu'on croise en "
    "lisant ; n'écarte que les emplois régionaux, argotiques ou propres à "
    "une espèce animale, qu'un lecteur francophone ne rencontrera "
    "pratiquement jamais. "
    "La définition ne doit jamais "
    "mentionner la nature grammaticale du mot. Aucun champ de la réponse (définition, justification, synonymes, "
    "exemples) ne doit jamais contenir de lien ni de balisage markdown (pas "
    "de \"[texte](url)\") ni de mention explicite d'une source : écris "
    "uniquement du texte brut partout, y compris quand tu t'appuies sur la "
    "recherche web. N'utilise jamais, dans aucun champ, les mots « contexte », "
    "« source », « Wiktionnaire » ou « recherche web », ni aucune autre "
    "référence à la façon dont tu as obtenu l'information : rédige comme si "
    "tu connaissais directement le sens du mot, jamais comme si tu "
    "répondais à partir d'un texte fourni — l'utilisateur ne voit jamais ce "
    "texte et une telle référence n'aurait aucun sens pour lui. Si un "
    "contexte est fourni, appuie-toi dessus. Si aucun contexte n'est fourni, "
    "utilise la recherche web pour te documenter avant de répondre."
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
    result = llm_client.generate_structured(
        prompt,
        schema_name="entry_enrichment",
        json_schema=schema,
        instructions=ENTRY_ENRICHMENT_INSTRUCTIONS,
        tools=tools,
        tool_choice=tool_choice,
    )
    # Enforced here too, not just via the prompt: a model that ignores the
    # instruction and returns word_recognized=false alongside real-looking
    # field values must not leak them to the caller.
    if not result.pop("word_recognized", True):
        return {"word_recognized": False}
    return result


_CURRENT_VALUE_LABELS = {
    "Definition": "Définition actuelle",
    "Type": "Type actuel",
    "Synonyms": "Synonymes actuels",
    "ExampleSentences": "Exemples actuels",
}


def _current_value_text(entry: dict, field: str) -> str:
    value = entry.get(field)
    if field == "Definition":
        # Numbered so the model sees the senses as distinct entries rather
        # than one run-on definition.
        if not value:
            return "vide"
        if len(value) == 1:
            return value[0]
        return "\n" + "\n".join(f"  {i}. {sense}" for i, sense in enumerate(value, start=1))
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
    # Definition included: one array element per sense.
    return {"type": "array", "items": {"type": "string"}}


def _build_entry_enrichment_schema(unlocked: list[str]) -> dict:
    properties = {"word_recognized": {"type": "boolean"}}
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


REPHRASE_DEFINITION_INSTRUCTIONS = (
    "Tu reformules une définition de dictionnaire déjà rédigée pour "
    "l'application LexiCall, en français. Produis une autre formulation du "
    "même sens, dans le même style concis de dictionnaire — pas une "
    "explication développée, pas un résumé plus court ni plus long, juste "
    "une manière différente de le dire, si possible plus simple à "
    "comprendre. Ne change jamais le sens, n'ajoute ni ne retire aucune "
    "information par rapport à la définition fournie. Ne mentionne jamais "
    "la nature grammaticale du mot. Le texte ne doit contenir aucun lien ni "
    "balisage markdown, aucune mention explicite d'une source, ni les mots "
    "« contexte », « source », « Wiktionnaire » ou « recherche web »."
)

REPHRASE_DEFINITION_SCHEMA = {
    "type": "object",
    "properties": {"definition": {"type": "string"}},
    "required": ["definition"],
    "additionalProperties": False,
}


def rephrase_definition(word: str, definition: str) -> str:
    prompt = f"Mot : {word}\nDéfinition actuelle : {definition}"
    result = llm_client.generate_structured(
        prompt,
        schema_name="rephrase_definition",
        json_schema=REPHRASE_DEFINITION_SCHEMA,
        instructions=REPHRASE_DEFINITION_INSTRUCTIONS,
        reasoning_effort="none",
    )
    return result["definition"]


DEFAULT_CATEGORY_CANDIDATES = 6
MAX_CATEGORY_SUGGESTIONS = 3


def find_category_candidates(word: str, senses: list[str], k: int = DEFAULT_CATEGORY_CANDIDATES) -> list[dict]:
    """The categories closest to a word, best first, as
    {id, name, path, score}. One embeddings call, then a purely local
    comparison against the stored category vectors — no LLM, and no token
    cost that grows with the corpus.

    Each sense is vectorized and ranked separately, then the top-k of each
    are merged: a single vector for a word meaning two different things
    lands between the two and surfaces neither. Measured on "ladre" (leper /
    miser), the second sense's category sat at rank 9 of 42 when both senses
    shared one vector, and at rank 3 once split. k is therefore per sense,
    and the returned list can hold up to k per sense — dropping the excess
    by global score would put the low-scoring sense right back out of reach.

    Comes back empty when no category has been indexed yet, which reads the
    same as "nothing matches": with no category to attach to, proposing a
    new one is the right answer anyway."""
    stored = category_embeddings_repo.list_embeddings()
    if not stored:
        return []

    query_texts = [f"{word} — {sense}".strip(" —") for sense in senses if sense.strip()] or [word]
    query_vectors = embeddings.embed_texts(query_texts)

    pairs = [(doc["Id"], doc["Vector"]) for doc in stored]
    best_by_id: dict[str, float] = {}
    for query_vector in query_vectors:
        for category_id, score in embeddings.top_similar(query_vector, pairs, k):
            if score > best_by_id.get(category_id, float("-inf")):
                best_by_id[category_id] = score
    ranked = sorted(best_by_id.items(), key=lambda item: item[1], reverse=True)

    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}
    candidates = []
    for category_id, score in ranked:
        category = by_id.get(category_id)
        # A vector whose category disappeared between the two reads above;
        # the next reindex drops it.
        if category is None:
            continue
        candidates.append(
            {
                "id": category_id,
                "name": category["Name"],
                "path": embeddings.build_category_path(category, by_id),
                "score": score,
            }
        )
    return candidates


CATEGORIZATION_INSTRUCTIONS = (
    "Avant toute suggestion, vérifie que « Mot » est un mot ou une expression "
    "française réelle et attestée — pas une suite de caractères aléatoire, un "
    "mot inventé, ou une faute de frappe. Si tu n'as pas de certitude "
    "raisonnable de son existence, réponds word_recognized=false avec une "
    "liste de suggestions vide : ne range jamais dans l'arborescence un mot "
    "dont tu ne peux pas confirmer l'existence, et n'invente surtout pas une "
    "catégorie pour l'accueillir. Une définition fournie par l'utilisateur ne "
    "prouve rien à elle seule — c'est l'existence du mot qui compte. Sinon, "
    "réponds word_recognized=true et poursuis normalement. "
    "Tu ranges un mot de vocabulaire français dans l'arborescence de "
    "catégories de l'application LexiCall. Chaque suggestion porte l'une de "
    "deux décisions : rattacher le mot à une catégorie existante, ou "
    "proposer d'en créer une nouvelle. "
    "Ne renvoie qu'une seule suggestion dans la grande majorité des cas — "
    "c'est le comportement attendu par défaut. N'en ajoute une deuxième, "
    "exceptionnellement une troisième (jamais plus), que si le mot a des "
    "sens réellement distincts relevant de champs lexicaux différents : "
    "chaque suggestion doit alors dire explicitement quel sens elle couvre. "
    "Deux facettes d'un même sens ne justifient jamais deux suggestions, et "
    "une catégorie déjà proposée ne doit jamais être répétée. "
    "Préfère toujours une catégorie existante quand l'une d'elles "
    "convient réellement ; ne propose une création que si aucune ne "
    "correspond au champ lexical visé. Les candidates te sont données par "
    "ordre de proximité calculée, mais cet ordre n'est qu'un indice : juge "
    "sur le sens, la première n'est pas forcément la bonne, et il arrive "
    "qu'aucune ne convienne. Si tu proposes une création, donne un nom dans "
    "le même style que les catégories existantes (un groupe nominal court, "
    "en français) et choisis comme parent la catégorie racine la plus "
    "pertinente parmi celles fournies ; ne laisse le parent vide que si le "
    "mot n'a sa place sous aucune d'elles. Justifie chaque décision en une "
    "phrase courte. Le texte ne doit contenir aucun lien ni balisage "
    "markdown, ni les mots « contexte », « source », « Wiktionnaire » ou "
    "« recherche web » : l'utilisateur ne voit que ta réponse."
)


def suggest_category(word: str, senses: list[str]) -> dict:
    """Decides where a word belongs: an existing category, or a new one to
    create. Only the closest candidates reach the LLM — that's what keeps
    the token cost flat as the corpus grows — plus every root category, so a
    new category can still be parented sensibly when similarity didn't
    surface the right branch at all."""
    candidates = find_category_candidates(word, senses)

    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}
    roots = [
        {
            "id": category["Id"],
            "name": category["Name"],
            "path": embeddings.build_category_path(category, by_id),
        }
        for category in categories
        if by_id.get(category.get("ParentId")) is None
    ]

    prompt = _build_categorization_prompt(word, senses, candidates, roots)
    schema = _build_categorization_schema(
        [candidate["id"] for candidate in candidates],
        [root["id"] for root in roots],
    )
    result = llm_client.generate_structured(
        prompt,
        schema_name="categorization",
        json_schema=schema,
        instructions=CATEGORIZATION_INSTRUCTIONS,
    )
    # Enforced here too, not just via the prompt: a model that flags the word
    # as unknown but still fills the list must not get its categories through
    # — same gate as the entry-enrichment route.
    if not result.pop("word_recognized", True):
        return {"word_recognized": False, "suggestions": []}
    return _resolve_categorization(result, by_id)


def _build_categorization_prompt(
    word: str,
    senses: list[str],
    candidates: list[dict],
    roots: list[dict],
) -> str:
    lines = [f"Mot : {word}"]
    written = [sense for sense in senses if sense.strip()]
    if len(written) == 1:
        lines.append(f"Définition : {written[0]}")
    elif written:
        lines.append("Définition (plusieurs sens) :")
        lines.extend(f"  {i}. {sense}" for i, sense in enumerate(written, start=1))

    if candidates:
        lines.append("\nCatégories candidates (de la plus proche à la moins proche) :")
        lines.extend(f"- [{c['id']}] {c['path']}" for c in candidates)
    else:
        lines.append("\nAucune catégorie candidate.")

    if roots:
        lines.append("\nCatégories racines (parents possibles pour une nouvelle catégorie) :")
        lines.extend(f"- [{r['id']}] {r['name']}" for r in roots)
    return "\n".join(lines)


def _id_enum_schema(ids: list[str]) -> dict:
    # An enum of the ids actually shown in the prompt: the model can't name a
    # category it never saw, and can't invent an id at all.
    if not ids:
        return {"type": "null"}
    return {"enum": [*ids, None]}


def _build_categorization_schema(candidate_ids: list[str], root_ids: list[str]) -> dict:
    # No maxItems on the array, deliberately: the API accepts it, but a
    # capped array makes the model cram what it wanted to say into the last
    # element rather than drop it (checked against the real API). The count
    # is bounded by the prompt and trimmed in _resolve_categorization.
    return {
        "type": "object",
        "properties": {
            "word_recognized": {"type": "boolean"},
            "suggestions": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "decision": {"enum": ["existing", "new"]},
                        "category_id": _id_enum_schema(candidate_ids),
                        "new_category_name": {"type": ["string", "null"]},
                        "new_category_parent_id": _id_enum_schema(root_ids),
                        "justification": {"type": "string"},
                    },
                    "required": [
                        "decision",
                        "category_id",
                        "new_category_name",
                        "new_category_parent_id",
                        "justification",
                    ],
                    "additionalProperties": False,
                },
            }
        },
        "required": ["word_recognized", "suggestions"],
        "additionalProperties": False,
    }


def _resolve_categorization(result: dict, by_id: dict[str, dict]) -> dict:
    """Turns the model's ids back into full categories, drops the entries it
    contradicts itself on, and trims to MAX_CATEGORY_SUGGESTIONS. The enums
    keep every id valid but can't require that "existing" actually names one,
    nor that the same category isn't proposed twice."""
    raw_suggestions = result.get("suggestions", [])
    resolved: list[dict] = []
    seen_existing: set[str] = set()
    seen_new: set[str] = set()

    for raw in raw_suggestions:
        suggestion = _resolve_one_suggestion(raw, by_id, seen_existing, seen_new)
        if suggestion is not None:
            resolved.append(suggestion)
        if len(resolved) == MAX_CATEGORY_SUGGESTIONS:
            break

    # Dropping the odd malformed entry is fine; dropping every single one
    # means the answer was unusable, and that should surface rather than
    # look like "no category fits".
    if raw_suggestions and not resolved:
        raise RuntimeError("Le modèle n'a produit aucune suggestion de catégorie exploitable.")
    return {"suggestions": resolved}


def _resolve_one_suggestion(
    raw: dict,
    by_id: dict[str, dict],
    seen_existing: set[str],
    seen_new: set[str],
) -> dict | None:
    suggestion = {
        "decision": raw["decision"],
        "category": None,
        "new_category_name": None,
        "new_category_parent": None,
        "justification": raw["justification"],
    }

    if raw["decision"] == "existing":
        category = by_id.get(raw["category_id"] or "")
        if category is None or category["Id"] in seen_existing:
            return None
        seen_existing.add(category["Id"])
        suggestion["category"] = _category_ref(category, by_id)
        return suggestion

    name = (raw["new_category_name"] or "").strip()
    if not name or name.casefold() in seen_new:
        return None
    seen_new.add(name.casefold())
    suggestion["new_category_name"] = name
    parent = by_id.get(raw["new_category_parent_id"] or "")
    if parent is not None:
        suggestion["new_category_parent"] = _category_ref(parent, by_id)
    return suggestion


def _category_ref(category: dict, by_id: dict[str, dict]) -> dict:
    return {
        "id": category["Id"],
        "name": category["Name"],
        "path": embeddings.build_category_path(category, by_id),
    }
