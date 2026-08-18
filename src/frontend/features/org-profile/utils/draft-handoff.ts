import { PROFILE_DRAFT_HANDOFF_STORAGE_KEY } from "../constants/profile-fields";
import type { ExtractedProfileDraft } from "../types/organization-profile";

/**
 * The handoff from the 40.27 checkpoint (O11) into this screen's preview.
 *
 * The checkpoint's «Заполнить профиль компании из этой структуры» writes the reviewed structure
 * here and navigates to `/org/profile`; this screen reads it once, previews it, and clears it. A
 * `sessionStorage` slot rather than a query parameter because the draft is a whole document —
 * a document in an address bar is a document in a proxy log — and rather than a store because the
 * navigation is a real page load.
 */
export function readHandedOverDraft(): ExtractedProfileDraft | null {
    if (typeof window === "undefined") return null;

    const serializedDraft = window.sessionStorage.getItem(PROFILE_DRAFT_HANDOFF_STORAGE_KEY);
    if (!serializedDraft) return null;

    try {
        const parsedDraft: unknown = JSON.parse(serializedDraft);
        if (typeof parsedDraft !== "object" || parsedDraft === null) return null;
        return parsedDraft as ExtractedProfileDraft;
    } catch {
        return null;
    }
}

export function clearHandedOverDraft(): void {
    if (typeof window === "undefined") return;
    window.sessionStorage.removeItem(PROFILE_DRAFT_HANDOFF_STORAGE_KEY);
}
