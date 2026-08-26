# Wrapper around the OpenAI Responses API: centralizes structured JSON
# output, reasoning effort, and the built-in web_search tool for every
# LLM-backed feature (definition suggestion, field enrichment, categorization).
import json

from openai import OpenAI

from lexicall_api.config import settings

MODEL = "gpt-5.6-luna"


def generate_structured(
    input: str | list[dict],
    *,
    schema_name: str,
    json_schema: dict,
    instructions: str | None = None,
    reasoning_effort: str = "low",
    tools: list[dict] | None = None,
    tool_choice: str | dict | None = None,
) -> dict:
    if not settings.openai_api_key:
        # Fails with a precise, actionable message instead of the SDK's own
        # generic "Missing credentials..." (which OpenAI(api_key=None) would
        # otherwise raise) — this is a server misconfiguration, not an
        # OpenAI-side failure, so it's worth being explicit about.
        raise RuntimeError("OPENAI_API_KEY n'est pas configurée côté serveur (voir api/.env).")

    client = OpenAI(api_key=settings.openai_api_key)
    extra_kwargs = {} if tool_choice is None else {"tool_choice": tool_choice}
    response = client.responses.create(
        model=MODEL,
        input=input,
        instructions=instructions,
        text={
            "format": {
                "type": "json_schema",
                "name": schema_name,
                "schema": json_schema,
                "strict": True,
            },
        },
        reasoning={"effort": reasoning_effort},
        tools=tools or [],
        **extra_kwargs,
    )
    return json.loads(response.output_text)
