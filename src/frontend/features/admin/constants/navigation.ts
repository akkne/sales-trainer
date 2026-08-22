import type { IconName } from "@/shared/components/icon";

export interface PlatformNavigationItem {
    href: string;
    label: string;
    icon: IconName;
}

/**
 * Every entry of the *platform* admin panel (Sellevate staff only, `RequirePlatformAdmin` on the
 * backend), declared at module level so the list is one testable value rather than an array literal
 * buried in a client component. Mirrors `ORGANIZATION_NAVIGATION_ITEMS` for the organization panel.
 *
 * Reaching the panel at all already implies platform staff, so no entry needs a gate of its own —
 * the superadmin-only affordances are gated inside the screens that own them.
 *
 * **`/admin/leagues` and `/admin/gamification` are deliberately absent** (Q-5,
 * `docs/NIGHT_AUDIT_QUESTIONS.md`). XP, streaks and leagues were removed from the product, so those
 * screens administer a mechanic no learner can see; on top of that their five mutations failed
 * silently (W-15, `docs/AUDIT_SILENT_WRITES.md`) and one of them — "close week" — is irreversible.
 * Unlinking them is what closes W-15: the routes still exist and still work if typed directly, so
 * nothing was deleted, but nothing in the panel leads an operator to an irreversible button on a
 * retired mechanic. Do not re-add either entry without re-opening that decision.
 */
export const PLATFORM_NAVIGATION_ITEMS: PlatformNavigationItem[] = [
    { href: "/admin/organizations", label: "Organizations", icon: "briefcase" },
    { href: "/admin/demo-requests", label: "Demo requests", icon: "send" },
    { href: "/admin/import", label: "Bundle Import", icon: "grid" },
    { href: "/admin/skills", label: "Skills", icon: "target" },
    { href: "/admin/skill-stages", label: "Skill Stages", icon: "layers" },
    { href: "/admin/topics", label: "Topics", icon: "folder" },
    { href: "/admin/lessons", label: "Lessons", icon: "book" },
    { href: "/admin/reference", label: "Reference", icon: "layers" },
    { href: "/admin/techniques", label: "Techniques", icon: "sparkle" },
    { href: "/admin/quotes", label: "Daily Quotes", icon: "message" },
    { href: "/admin/dialog", label: "Dialog", icon: "message" },
    { href: "/admin/discuss", label: "Discuss", icon: "forum" },
    { href: "/admin/prompts", label: "AI Prompts", icon: "sparkle" },
    { href: "/admin/voice/usage", label: "Voice Usage", icon: "mic" },
    { href: "/admin/users", label: "Users", icon: "users" },
];
