"use client";

import { AnimatePresence, motion } from "framer-motion";
import { createPortal } from "react-dom";
import { useEffect, useState } from "react";
import { Icon, type IconName } from "@/shared/components/icon";
import { useToastStore, type ToastItem, type ToastVariant } from "@/features/notifications/store/toast-store";

const VARIANT_STYLES: Record<ToastVariant, { bar: string; icon: string; iconName: IconName }> = {
    success: {
        bar: "bg-success",
        icon: "text-success",
        iconName: "check",
    },
    error: {
        bar: "bg-bad",
        icon: "text-bad",
        iconName: "warning",
    },
    info: {
        bar: "bg-primary",
        icon: "text-primary",
        iconName: "info",
    },
};

function ToastChip({ toast, onDismiss }: { toast: ToastItem; onDismiss: (id: string) => void }) {
    const styles = VARIANT_STYLES[toast.variant];

    return (
        <motion.div
            layout
            initial={{ opacity: 0, y: 16, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.96 }}
            transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
            role={toast.variant === "error" ? "alert" : "status"}
            aria-live={toast.variant === "error" ? "assertive" : "polite"}
            aria-atomic="true"
            className="flex items-start gap-3 w-full max-w-sm bg-surface border border-line rounded-[var(--r-md)] shadow-[var(--sh-3)] px-4 py-3 pointer-events-auto"
        >
            {/* Accent bar */}
            <span className={`flex-shrink-0 mt-0.5 w-1 self-stretch rounded-full ${styles.bar}`} aria-hidden />

            {/* Icon */}
            <span className={`flex-shrink-0 mt-0.5 ${styles.icon}`} aria-hidden>
                <Icon name={styles.iconName} size="sm" />
            </span>

            {/* Message */}
            <span className="flex-1 text-sm text-ink leading-snug">{toast.message}</span>

            {/* Dismiss */}
            <button
                type="button"
                onClick={() => onDismiss(toast.id)}
                aria-label="Dismiss notification"
                className="flex-shrink-0 mt-0.5 text-ink-4 hover:text-ink-2 transition-colors"
            >
                <Icon name="close" size="sm" />
            </button>
        </motion.div>
    );
}

export function Toaster() {
    const { toasts, dismiss } = useToastStore();
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        setMounted(true);
    }, []);

    if (!mounted) return null;

    return createPortal(
        <div
            aria-label="Notifications"
            className="fixed top-4 right-4 z-[9999] flex flex-col gap-2 items-end pointer-events-none w-full max-w-sm"
        >
            <AnimatePresence initial={false} mode="sync">
                {toasts.map((t) => (
                    <ToastChip key={t.id} toast={t} onDismiss={dismiss} />
                ))}
            </AnimatePresence>
        </div>,
        document.body
    );
}
