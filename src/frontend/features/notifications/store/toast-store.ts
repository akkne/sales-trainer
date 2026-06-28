import { create } from "zustand";

export type ToastVariant = "success" | "error" | "info";

export interface ToastItem {
    id: string;
    message: string;
    variant: ToastVariant;
}

interface ToastStoreState {
    toasts: ToastItem[];
    push: (message: string, variant: ToastVariant) => void;
    dismiss: (id: string) => void;
}

const AUTO_DISMISS_MS: Record<ToastVariant, number> = {
    success: 4000,
    info: 4000,
    error: 6000,
};

function generateId(): string {
    return `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

export const useToastStore = create<ToastStoreState>((set) => ({
    toasts: [],

    push: (message, variant) => {
        const id = generateId();

        set((state) => ({
            toasts: [...state.toasts, { id, message, variant }],
        }));

        setTimeout(() => {
            set((state) => ({
                toasts: state.toasts.filter((t) => t.id !== id),
            }));
        }, AUTO_DISMISS_MS[variant]);
    },

    dismiss: (id) => {
        set((state) => ({
            toasts: state.toasts.filter((t) => t.id !== id),
        }));
    },
}));

// Convenience helpers — call these from anywhere (hooks, mutations, etc.)
export const toast = {
    success: (message: string) => useToastStore.getState().push(message, "success"),
    error: (message: string) => useToastStore.getState().push(message, "error"),
    info: (message: string) => useToastStore.getState().push(message, "info"),
    push: (message: string, variant: ToastVariant = "info") =>
        useToastStore.getState().push(message, variant),
};
