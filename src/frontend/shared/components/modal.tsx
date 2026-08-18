"use client";

import { ReactNode, useCallback, useEffect, useId, useRef } from "react";
import { Icon } from "./icon";

export type ModalSize = "sm" | "md" | "lg" | "xl";

const SIZE_WIDTHS: Record<ModalSize, string> = {
    sm: "380px",
    md: "560px",
    lg: "760px",
    xl: "980px",
};

const FOCUSABLE_SELECTOR = [
    "a[href]",
    "button:not([disabled])",
    "input:not([disabled])",
    "select:not([disabled])",
    "textarea:not([disabled])",
    '[tabindex]:not([tabindex="-1"])',
].join(", ");

interface ModalProps {
    open: boolean;
    onClose: () => void;
    title: string;
    size?: ModalSize;
    footer?: ReactNode;
    children: ReactNode;
}

/**
 * The panel's one dialog surface. Before it there were seven hand-written copies
 * (`companies`, `dialog`, `discuss`, `profile`, `skills`, `admin/user-detail-modal`), each
 * re-deciding backdrop, scroll and dismissal; the organization panel needs six more, which is
 * where a repeated shape stops being a pattern and becomes debt.
 *
 * Escape closes, focus is trapped inside while open, and the body stops scrolling behind it.
 */
export function Modal({ open, onClose, title, size = "md", footer, children }: ModalProps) {
    const dialogElementRef = useRef<HTMLDivElement | null>(null);
    const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);
    const titleElementId = useId();

    const focusFirstElement = useCallback(() => {
        const dialogElement = dialogElementRef.current;
        if (!dialogElement) return;
        const focusable = dialogElement.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
        (focusable[0] ?? dialogElement).focus();
    }, []);

    useEffect(() => {
        if (!open) return;

        previouslyFocusedElementRef.current = document.activeElement as HTMLElement | null;
        focusFirstElement();

        const previousBodyOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";

        return () => {
            document.body.style.overflow = previousBodyOverflow;
            previouslyFocusedElementRef.current?.focus?.();
        };
    }, [open, focusFirstElement]);

    const handleKeyDown = (keyboardEvent: React.KeyboardEvent<HTMLDivElement>) => {
        if (keyboardEvent.key === "Escape") {
            keyboardEvent.stopPropagation();
            onClose();
            return;
        }

        if (keyboardEvent.key !== "Tab") return;

        const dialogElement = dialogElementRef.current;
        if (!dialogElement) return;

        const focusable = Array.from(
            dialogElement.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)
        );
        if (focusable.length === 0) {
            keyboardEvent.preventDefault();
            return;
        }

        const firstElement = focusable[0];
        const lastElement = focusable[focusable.length - 1];
        const activeElement = document.activeElement;

        if (keyboardEvent.shiftKey && activeElement === firstElement) {
            keyboardEvent.preventDefault();
            lastElement.focus();
            return;
        }
        if (!keyboardEvent.shiftKey && activeElement === lastElement) {
            keyboardEvent.preventDefault();
            firstElement.focus();
        }
    };

    if (!open) return null;

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
            onMouseDown={onClose}
            onKeyDown={handleKeyDown}
        >
            <div
                ref={dialogElementRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleElementId}
                tabIndex={-1}
                onMouseDown={(mouseEvent) => mouseEvent.stopPropagation()}
                className="w-full max-h-[90vh] flex flex-col outline-none"
                style={{
                    maxWidth: SIZE_WIDTHS[size],
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: "var(--r-lg)",
                    boxShadow: "var(--sh-2)",
                }}
            >
                <div className="flex items-start gap-3 px-5 pt-5 pb-3">
                    <h2 id={titleElementId} className="flex-1 min-w-0 text-base font-bold text-ink">
                        {title}
                    </h2>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label="Закрыть"
                        className="shrink-0 grid place-items-center w-8 h-8 rounded-lg text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors"
                    >
                        <Icon name="close" size="sm" />
                    </button>
                </div>

                <div className="flex-1 min-h-0 overflow-y-auto px-5 pb-5 text-sm text-ink-2">
                    {children}
                </div>

                {footer && (
                    <div
                        className="flex items-center justify-end gap-2 px-5 py-4"
                        style={{ borderTop: "1px solid var(--line)" }}
                    >
                        {footer}
                    </div>
                )}
            </div>
        </div>
    );
}
