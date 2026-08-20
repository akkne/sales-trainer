#!/usr/bin/env python3
"""Enforces the multi-tenancy request boundary rule from docs/TENANCY/TENANCY.md
section 1.3: the organization is never read from the request body, query
string, or route. Only the gateway-validated X-Organization-Id header (via
Sellevate.BuildingBlocks.Identity.IdentityHeaders / ITenantContext) may supply
it.

Three checks:
  1. A request DTO (file name ending in Request.cs or Dto.cs) declaring an
     OrganizationId member — that member would be filled from the request
     body.
  2. A [FromQuery] / [FromRoute] binding named organizationId — the query
     string / route path cases.
  3. A routing attribute or minimal-API map call whose template contains the
     literal segment {organizationId}. Only route declarations are inspected:
     "org:{organizationId}:" in a Redis key is the tenancy fix, not a breach.

Scope is passed as command-line paths (defaults to the whole backend). Files
under obj/, bin/, and Migrations/ are skipped.

Three narrow security exceptions are allow-listed by exact path — see
ALLOWED_REQUEST_DTO_PATHS and ALLOWED_ROUTE_TEMPLATE_PATHS. Section 1.3 states the
rule and its carve-outs in the same breath: a superadmin acting across tenants does
so through an explicit impersonation endpoint that mints a new token (those bodies
have to name an organization; that IS the endpoint), a machine-to-machine
internal/* route may address an organization by its route segment instead, because
its caller is another service with no membership in that organization to carry an
X-Organization-Id header for, and a platform-admin-only *read* may name an
organization in its route when the data it returns already widens to every
organization for platform staff (the query filter, not the route, is what would
have made the read cross-tenant either way) — PlatformAdminController,
InternalOrganizationBootstrapController and AdminAiQuotaController rely on this
trio of carve-outs respectively. Naming the files here keeps each exception visible
and reviewable instead of being hidden behind a file or route shape chosen to slip
past the regex.

A third, non-security allow-list — ALLOWED_OUTBOUND_ONLY_FILENAME_FALSE_POSITIVES — exists
for the filename heuristic in check 1 itself: "ends in Request.cs or Dto.cs" assumes that
suffix always means a wire-bound *inbound* request DTO, but the same suffix also names
persisted entities whose own domain name happens to end that way (a lead is literally a
"demo request") and response DTOs that are only ever returned, never accepted as a
[FromBody] parameter type. Both are false positives, not exceptions to the rule: the
OrganizationId on each allow-listed file is written by server-side logic and read by a
client, and is never bound from an inbound request body.
"""
import pathlib
import re
import sys

ORGANIZATION_ID_IDENTIFIER_PATTERN = re.compile(r"\bOrganizationId\b")
FROM_QUERY_OR_ROUTE_PATTERN = re.compile(r"\[From(?:Query|Route)\b[^\]]*\]")
ORGANIZATION_ID_BINDING_NAME_PATTERN = re.compile(r"\borganizationId\b", re.IGNORECASE)
ROUTE_TEMPLATE_ORGANIZATION_PATTERN = re.compile(r"\{organizationId(?::\w+)?\}", re.IGNORECASE)
# Matches the bare segment and a route-constrained one such as "{organizationId:guid}" — a
# constraint suffix must not be a blind spot a route template can hide behind.
# The check above is about ROUTE templates, so it only looks at lines that declare one. Before
# Phase 40.11 it scanned every line, which was fine while "{organizationId}" could only be a route
# segment; once services started building Redis keys as $"org:{organizationId}:..." — the whole
# point of 40.11 — a whole-file grep flagged the fix as the violation. Narrowing it to routing
# attributes and minimal-API map calls keeps the rule and drops the false positive.
ROUTE_DECLARATION_PATTERN = re.compile(
    r"\[\s*(?:Route|Http(?:Get|Post|Put|Patch|Delete|Head|Options))\b"
    r"|\bMap(?:Get|Post|Put|Patch|Delete|Group|Methods)\s*\("
)

REQUEST_DTO_FILENAME_PATTERN = re.compile(r"(Request|Dto)\.cs$")

SKIP_NAME_MARKERS = (".g.cs", ".designer.cs", "globalusings", "assemblyinfo")

# Request DTOs allowed to declare an OrganizationId member, by exact repo-relative path.
# Every entry must be a body consumed only by a route gated with RequireSuperAdmin — the
# platform-staff carve-out in TENANCY.md section 1.3. Adding a path here is a security
# decision, not a formality.
ALLOWED_REQUEST_DTO_PATHS = frozenset({
    "src/backend/identity-service/Identity/Features/PlatformAdmin/Models/"
    "CreateImpersonationRequestDto.cs",
    "src/backend/identity-service/Identity/Features/PlatformAdmin/Models/"
    "BootstrapOrganizationAdminRequestDto.cs",
})

# Route declarations allowed to carry an {organizationId} (or {organizationId:guid}) segment, by
# exact repo-relative path. Adding a path here is a security decision, not a formality. Two
# different justifications are allow-listed:
#   - InternalOrganizationBootstrapController.cs: a machine-to-machine internal/* route guarded by
#     InternalServiceAuthFilter rather than [TenantScoped], applied here to a route segment because
#     the caller is another service with no membership to carry an X-Organization-Id header for.
#   - AdminAiQuotaController.cs: a RequirePlatformAdmin-only GET (2026-08-21 admin audit, AD-5).
#     Every caller here is already platform staff, for whom OrganizationQuota's own EF query filter
#     (`IsPlatformWide || OrganizationId == current`) already reads across every organization — the
#     route segment only narrows an already cross-tenant-readable query to the one organization the
#     platform panel's quota screen is showing, instead of leaving it defaulted to the caller's own.
#     The PUT on the same controller carries no such segment and still writes only the caller's own
#     organization; this exception covers the GET action only.
ALLOWED_ROUTE_TEMPLATE_PATHS = frozenset({
    "src/backend/identity-service/Identity/Features/Organizations/Endpoints/"
    "InternalOrganizationBootstrapController.cs",
    "src/backend/ai-service/Ai/Features/Quotas/AdminAiQuotaController.cs",
})

# Files whose name coincidentally ends in "Request.cs" or "Dto.cs" for a domain reason having
# nothing to do with being an inbound wire type, tripping check 1's filename heuristic even
# though the OrganizationId member is never bound from a request body:
#   - DemoRequest.cs is a persisted entity (a lead is literally a "demo request"); its
#     OrganizationId is written only by DemoRequestProvisioningService, from a Guid it minted
#     itself when it inserted the Organization row.
#   - DemoRequestDto.cs is a response-only DTO — returned by GET/PATCH/POST …/provision, never
#     accepted as a [FromBody] parameter type anywhere — reporting the same OrganizationId back
#     to whoever is allowed to read it.
# See docs/DEMO_REQUEST.md.
ALLOWED_OUTBOUND_ONLY_FILENAME_FALSE_POSITIVES = frozenset({
    "src/backend/organization-service/Organization/Features/DemoRequests/Models/DemoRequest.cs",
    "src/backend/organization-service/Organization/Features/DemoRequests/Models/DemoRequestDto.cs",
})

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parent.parent


def relative_to_repository(path: pathlib.Path) -> str:
    try:
        return path.resolve().relative_to(REPOSITORY_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def is_allowed_request_dto(path: pathlib.Path) -> bool:
    return relative_to_repository(path) in (
        ALLOWED_REQUEST_DTO_PATHS | ALLOWED_OUTBOUND_ONLY_FILENAME_FALSE_POSITIVES
    )


def is_allowed_route_template(path: pathlib.Path) -> bool:
    return relative_to_repository(path) in ALLOWED_ROUTE_TEMPLATE_PATHS


def is_skipped(path: pathlib.Path) -> bool:
    lowered = path.name.lower()
    if any(marker in lowered for marker in SKIP_NAME_MARKERS):
        return True
    parts = {part.lower() for part in path.parts}
    return bool(parts & {"obj", "bin", "migrations"})


def lint_file(path: pathlib.Path):
    violations = []
    text = path.read_text(encoding="utf-8")
    if "<auto-generated" in text:
        return violations

    is_request_dto_file = (
        bool(REQUEST_DTO_FILENAME_PATTERN.search(path.name))
        and not is_allowed_request_dto(path)
    )
    route_template_is_allowed = is_allowed_route_template(path)

    for line_number, line in enumerate(text.splitlines(), start=1):
        if is_request_dto_file and ORGANIZATION_ID_IDENTIFIER_PATTERN.search(line):
            violations.append((
                line_number,
                "organization id must never appear in a request DTO — read it only from "
                "the gateway-validated X-Organization-Id header (TENANCY.md section 1.3)",
            ))

        if FROM_QUERY_OR_ROUTE_PATTERN.search(line) and ORGANIZATION_ID_BINDING_NAME_PATTERN.search(line):
            violations.append((
                line_number,
                "organization id must never be bound from a query string or route parameter "
                "(TENANCY.md section 1.3)",
            ))

        if (
            ROUTE_DECLARATION_PATTERN.search(line)
            and ROUTE_TEMPLATE_ORGANIZATION_PATTERN.search(line)
            and not route_template_is_allowed
        ):
            violations.append((
                line_number,
                "route template must not carry an organization id segment "
                "(TENANCY.md section 1.3)",
            ))

    return violations


def report_stale_allow_list_entries() -> int:
    """An allow-list entry that no longer points at a file is a silent hole: the exception
    outlives the code it was granted for. Report it as a violation rather than ignoring it."""
    stale_count = 0
    all_allowed_paths = (
        ALLOWED_REQUEST_DTO_PATHS | ALLOWED_ROUTE_TEMPLATE_PATHS | ALLOWED_OUTBOUND_ONLY_FILENAME_FALSE_POSITIVES
    )
    for allowed_path in sorted(all_allowed_paths):
        if not (REPOSITORY_ROOT / allowed_path).is_file():
            print(
                f"{allowed_path}: allow-listed in tenancy-boundary-lint but the file no longer "
                "exists — remove the entry (TENANCY.md section 1.3)"
            )
            stale_count += 1
    return stale_count


def main(argv):
    roots = argv[1:] or ["src/backend"]
    total = report_stale_allow_list_entries()
    for root in roots:
        for path in sorted(pathlib.Path(root).rglob("*.cs")):
            if is_skipped(path):
                continue
            for line_number, reason in lint_file(path):
                print(f"{path}:{line_number}: {reason}")
                total += 1
    if total:
        print(f"\ntenancy-boundary-lint: {total} violation(s) found.")
        return 1
    print("tenancy-boundary-lint: clean.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
