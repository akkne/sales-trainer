import { create } from "zustand";

type Theme = "light" | "dark" | "system";

interface ThemeStoreState {
    theme: Theme;
    setTheme: (theme: Theme) => void;
}

function getInitialTheme(): Theme {
    if (typeof window === "undefined") return "system";
    return (localStorage.getItem("theme") as Theme) || "system";
}

function applyTheme(theme: Theme) {
    if (typeof window === "undefined") return;

    const root = document.documentElement;
    const systemDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    const isDark = theme === "dark" || (theme === "system" && systemDark);

    if (isDark) {
        root.setAttribute("data-theme", "dark");
    } else {
        root.removeAttribute("data-theme");
    }
}

export const useThemeStore = create<ThemeStoreState>((set) => ({
    theme: getInitialTheme(),

    setTheme: (theme) => {
        localStorage.setItem("theme", theme);
        applyTheme(theme);
        set({ theme });
    },
}));

if (typeof window !== "undefined") {
    applyTheme(getInitialTheme());

    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
        const currentTheme = useThemeStore.getState().theme;
        if (currentTheme === "system") {
            applyTheme("system");
        }
    });
}
