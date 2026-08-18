interface LegacyAdminRedirectRule {
    prefix: string;
    replacement: string;
}

/**
 * The organization panel moved out of `/admin/*` before anything under `/admin/*` ever rendered
 * those screens, and two `actionUrl`s minted by the Phase 40.26 notification jobs already point
 * at the old addresses:
 *
 *   `AssignmentDeadlineDigest` → `/admin/assignments/{id}?action=remind&scope=not_started`
 *   `DialogReviewDisputed`     → `/admin/dialog-reviews?note={noteId}`
 *
 * They are baked into rows already sitting in the notification store, so they cannot be fixed by
 * renaming a route or by editing the backend; they are fixed here, by a table the platform layout
 * consults before its own role gate.
 *
 * Longest prefix wins. `/admin/dialog/overrides` has to beat `/admin/dialog`, which is the
 * platform screen for dialog bundles and must never redirect anywhere.
 */
const LEGACY_ADMIN_REDIRECT_RULES: LegacyAdminRedirectRule[] = [
    { prefix: "/admin/content/adaptations", replacement: "/org/content/adaptations" },
    { prefix: "/admin/content/overrides", replacement: "/org/content/overrides" },
    { prefix: "/admin/dialog/overrides", replacement: "/org/content/overrides" },
    { prefix: "/admin/content-generation", replacement: "/org/content/generation" },
    { prefix: "/admin/dialog-sessions", replacement: "/org/dialogs" },
    { prefix: "/admin/dialog-reviews", replacement: "/org/reviews" },
    { prefix: "/admin/assignments", replacement: "/org/assignments" },
    { prefix: "/admin/ai-usage", replacement: "/org/usage" },
    { prefix: "/admin/team", replacement: "/org" },
].sort((left, right) => right.prefix.length - left.prefix.length);

function matchesPrefix(pathname: string, prefix: string): boolean {
    return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

/**
 * The organization-panel address a legacy `/admin/*` path maps to, or `null` when the path
 * belongs to the platform panel and must be left alone.
 *
 * The query string is carried over verbatim — `action=remind&scope=not_started` is the whole
 * reason the notification link is worth preserving, and the target screen reads those parameters
 * rather than acting on them: a URL that sends the team a reminder as it loads is a URL that
 * fires the first time a mail scanner follows it.
 */
export function resolveLegacyAdminRedirect(
    pathname: string,
    search: string = ""
): string | null {
    const matchedRule = LEGACY_ADMIN_REDIRECT_RULES.find((rule) =>
        matchesPrefix(pathname, rule.prefix)
    );
    if (!matchedRule) return null;

    const remainder = pathname.slice(matchedRule.prefix.length);
    const normalizedSearch = search && !search.startsWith("?") ? `?${search}` : search;

    return `${matchedRule.replacement}${remainder}${normalizedSearch}`;
}
