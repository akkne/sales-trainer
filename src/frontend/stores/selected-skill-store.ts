import { create } from "zustand";

interface SelectedSkill {
    slug: string;
    title: string;
    iconName: string;
}

interface SelectedSkillStoreState {
    selectedSkill: SelectedSkill | null;
    setSelectedSkill: (skill: SelectedSkill) => void;
    clearSelectedSkill: () => void;
}

function loadFromStorage(): SelectedSkill | null {
    if (typeof window === "undefined") return null;
    try {
        const raw = localStorage.getItem("selectedSkill");
        return raw ? (JSON.parse(raw) as SelectedSkill) : null;
    } catch {
        return null;
    }
}

export const useSelectedSkillStore = create<SelectedSkillStoreState>((set) => ({
    selectedSkill: loadFromStorage(),

    setSelectedSkill: (skill) => {
        localStorage.setItem("selectedSkill", JSON.stringify(skill));
        set({ selectedSkill: skill });
    },

    clearSelectedSkill: () => {
        localStorage.removeItem("selectedSkill");
        set({ selectedSkill: null });
    },
}));
