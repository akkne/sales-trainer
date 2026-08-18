"use client";

import { Icon } from "@/shared/components/icon";

export const PEOPLE_READ_ONLY_MESSAGE =
    "Приглашать и отключать людей может только суперадминистратор организации.";

const PEOPLE_READ_ONLY_EXPLANATION =
    "Состав команды и приглашения вы видите целиком — они нужны вам, чтобы выдавать задания.";

/// The strip a `TenancyAdmin` sees instead of buttons that would answer 403.
///
/// It is a sentence, not a disabled toolbar: a row of greyed-out controls reads as «сломалось», and
/// the reason this administrator cannot invite people is a deliberate split of privileges, not a
/// failure. Adding and removing users is the one thing the 2026-08-16 role split reserves for a
/// superadmin (docs/TENANCY/TENANCY.md §4.2).
export function ReadOnlyNotice() {
    return (
        <div
            className="mb-6 flex items-start gap-3 rounded-xl px-4 py-3"
            style={{ background: "var(--bg-2)", border: "1px solid var(--line)" }}
            role="status"
        >
            <Icon name="lock" size="sm" className="mt-0.5 shrink-0 text-ink-3" />
            <div className="min-w-0">
                <p className="text-sm text-ink">{PEOPLE_READ_ONLY_MESSAGE}</p>
                <p className="mt-1 text-sm text-ink-3">{PEOPLE_READ_ONLY_EXPLANATION}</p>
            </div>
        </div>
    );
}
