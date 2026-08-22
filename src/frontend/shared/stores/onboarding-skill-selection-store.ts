import { create } from "zustand";

/**
 * Q-6 / W-13 (`docs/NIGHT_AUDIT_QUESTIONS.md`, `docs/AUDIT_SILENT_WRITES.md`).
 *
 * Onboarding does two writes: `POST /onboarding` and then `PUT /skills/enrolled`. The owner's call
 * is that the second one stays best-effort — a flapping backend must not trap a new user on the
 * onboarding screen — but that its failure is no longer invisible: the user reaches `/tree` and is
 * told there, in one line, that their skill choice did not save and where to redo it.
 *
 * This flag is the only thing carrying that fact from the onboarding mutation to `/tree`, which is
 * a different route. It is in `localStorage` rather than in memory on purpose: `router.push`
 * survives in memory, but a reload of `/tree` (or a user who closes the tab and comes back) does
 * not, and the whole point of the decision is that the person finds out. It is cleared when they
 * dismiss the banner or when any successful `PUT /skills/enrolled` makes it untrue — see
 * `useUpdateEnrolledSkills`.
 *
 * Hydration is explicit (`hydrateFromStorage`, called from an effect) rather than read at store
 * creation: this value decides whether a banner renders, so reading `localStorage` during module
 * init would make the server's HTML and the client's first render disagree.
 */
const STORAGE_KEY = "onboarding.skillSelectionUnsaved";

interface OnboardingSkillSelectionState {
    isSkillSelectionUnsaved: boolean;
    hydrateFromStorage: () => void;
    markSkillSelectionUnsaved: () => void;
    clearSkillSelectionUnsaved: () => void;
}

function writeToStorage(value: boolean): void {
    if (typeof window === "undefined") return;
    try {
        if (value) {
            localStorage.setItem(STORAGE_KEY, "true");
        } else {
            localStorage.removeItem(STORAGE_KEY);
        }
    } catch {
        // A blocked or full localStorage must not break onboarding or the tree. The in-memory
        // flag below still shows the banner for this navigation, which is the case that matters.
    }
}

function readFromStorage(): boolean {
    if (typeof window === "undefined") return false;
    try {
        return localStorage.getItem(STORAGE_KEY) === "true";
    } catch {
        return false;
    }
}

export const useOnboardingSkillSelectionStore = create<OnboardingSkillSelectionState>((set) => ({
    isSkillSelectionUnsaved: false,

    hydrateFromStorage: () => set({ isSkillSelectionUnsaved: readFromStorage() }),

    markSkillSelectionUnsaved: () => {
        writeToStorage(true);
        set({ isSkillSelectionUnsaved: true });
    },

    clearSkillSelectionUnsaved: () => {
        writeToStorage(false);
        set({ isSkillSelectionUnsaved: false });
    },
}));
