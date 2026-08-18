#!/usr/bin/env python3
"""ai-provider-lint — every LLM and speech call must go through ai-service's meter.

Phase 40.33 makes ai-service the single point per-organization spend is enforced at. That claim is
only worth something if it stays true, and the way it stopped being true last time was quiet: the
monolith split left learning-service holding its own copy of `OpenAiChatService` and its own copy of
`YandexTtsService`, nobody noticed for six blocks, and `docs/DONT_FORGET.md` had to carry the item
from 40.27 until this block removed them.

So the invariant is checked rather than remembered. Two rules:

  1. Only ai-service may open an HTTP client named after a provider (`OpenAI`, `YandexTts`,
     `Deepgram`, `Whisper`) or reference a provider host. Any other service doing so has a door
     around the meter.

  2. Inside ai-service, only the files listed in ``METERED_PROVIDER_CALLERS`` may open one. Each of
     them is wired to ``IAiSpendMeter``; a sixth file appearing here is unmetered spend until it is
     reviewed and added deliberately.

Both lists are allow-lists on purpose. Adding to either is a decision about money, not a formality —
the same posture ``tenancy-boundary-lint.py`` takes about its two request DTOs.

Usage:  python3 scripts/ai-provider-lint.py [path ...]      (default: src/backend)
Exit:   0 clean, 1 violations found.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent

# The named HttpClients and provider hosts that mean "this code talks to a paid provider".
PROVIDER_CLIENT_PATTERN = re.compile(
    r"""CreateClient\(\s*"(OpenAI|YandexTts|Deepgram|Whisper)"\s*\)"""
)
PROVIDER_HOST_PATTERN = re.compile(
    r"""(api\.openai\.com|tts\.api\.cloud\.yandex\.net|api\.deepgram\.com|api\.f5ai\.ru)"""
)

# The only files in the whole backend allowed to reach a provider. Every one of them holds an
# IAiSpendMeter and gates or charges the call.
METERED_PROVIDER_CALLERS = {
    "src/backend/ai-service/Ai/Features/Dialog/Services/Implementation/OpenAiChatService.cs",
    "src/backend/ai-service/Ai/Features/Evaluation/Services/Implementation/AiEvaluationStrategyBase.cs",
    "src/backend/ai-service/Ai/Features/Transcription/Services/Implementation/WhisperTranscriptionService.cs",
    "src/backend/ai-service/Ai/Features/Voice/Services/Implementation/YandexTtsService.cs",
    # Registers the named clients and their Polly stacks; makes no call of its own.
    "src/backend/ai-service/Ai/Program.cs",
    # Warms the TCP+TLS handshake with a HEAD ping. No completion, no tokens, no charge.
    "src/backend/ai-service/Ai/Infrastructure/Http/UpstreamConnectionWarmupService.cs",
}

SKIPPED_PATH_PARTS = ("/obj/", "/bin/", "/Migrations/")
SKIPPED_SUFFIXES = (".g.cs", ".designer.cs")


def is_skipped(path: Path) -> bool:
    posix = path.as_posix()
    if any(part in posix for part in SKIPPED_PATH_PARTS):
        return True
    lowered = posix.lower()
    if lowered.endswith(SKIPPED_SUFFIXES):
        return True
    # Tests stub the provider rather than calling it; they hold no key and cost nothing.
    return ".Tests/" in posix or ".Tests\\" in posix


def scan(paths: list[Path]) -> list[str]:
    violations: list[str] = []

    for root in paths:
        for path in sorted(root.rglob("*.cs")):
            if is_skipped(path):
                continue

            relative = path.relative_to(REPOSITORY_ROOT).as_posix()
            if relative in METERED_PROVIDER_CALLERS:
                continue

            try:
                lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
            except OSError as error:  # pragma: no cover - unreadable file
                violations.append(f"{relative}: could not be read ({error})")
                continue

            # Inside ai-service a provider host in a configuration default is expected — that is
            # where the keys and base URLs belong. What must not appear outside the allow-list is a
            # *call*: a named provider HttpClient. Outside ai-service, either is a finding.
            inside_ai_service = relative.startswith("src/backend/ai-service/")

            for number, line in enumerate(lines, start=1):
                reaches_provider = PROVIDER_CLIENT_PATTERN.search(line) or (
                    not inside_ai_service and PROVIDER_HOST_PATTERN.search(line))

                if reaches_provider:
                    violations.append(
                        f"{relative}:{number}: reaches an LLM/speech provider outside the metered "
                        f"call sites — every provider call must go through ai-service's "
                        f"IAiSpendMeter (docs/AI_SERVICE.md, roadmap 40.33)\n    {line.strip()}"
                    )

    # An allow-list entry that no longer exists is the same bug in the other direction: the file was
    # renamed or deleted and the list stopped describing anything.
    for allowed in sorted(METERED_PROVIDER_CALLERS):
        if not (REPOSITORY_ROOT / allowed).exists():
            violations.append(
                f"{allowed}: listed in METERED_PROVIDER_CALLERS but does not exist — "
                f"remove the entry or fix the path"
            )

    return violations


def main() -> int:
    arguments = sys.argv[1:]
    roots = [Path(argument).resolve() for argument in arguments] or [REPOSITORY_ROOT / "src" / "backend"]

    violations = scan(roots)
    if not violations:
        print("ai-provider-lint: clean.")
        return 0

    print(f"ai-provider-lint: {len(violations)} violation(s).\n")
    for violation in violations:
        print(f"  {violation}")
    print(
        "\nA provider call outside ai-service is spend nobody's quota counts. Move the call behind "
        "an internal ai-service route (POST /ai/chat, /ai/chat/stream, /ai/tts, /ai/evaluate, "
        "/ai/content/*), or — if it genuinely belongs in ai-service — wire it to IAiSpendMeter and "
        "add it to METERED_PROVIDER_CALLERS deliberately."
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
