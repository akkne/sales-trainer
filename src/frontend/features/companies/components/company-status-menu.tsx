"use client";

import { useEffect, useRef, useState } from "react";
import { Icon } from "@/shared/components/icon";
import {
    COMPANY_STATUS_META,
    COMPANY_STATUS_ORDER,
    type CompanyStatus,
} from "@/features/companies/lib/company-status";

interface CompanyStatusMenuProps {
    status: CompanyStatus;
    onChange: (status: CompanyStatus) => void;
    disabled?: boolean;
}

export function CompanyStatusMenu({ status, onChange, disabled = false }: CompanyStatusMenuProps) {
    const [isOpen, setOpen] = useState(false);
    const containerRef = useRef<HTMLDivElement>(null);
    const triggerRef = useRef<HTMLButtonElement>(null);
    const itemRefs = useRef<Array<HTMLButtonElement | null>>([]);
    const meta = COMPANY_STATUS_META[status];

    useEffect(() => {
        if (!isOpen) return;

        function handleClickOutside(event: MouseEvent) {
            if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
                setOpen(false);
            }
        }

        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, [isOpen]);

    useEffect(() => {
        if (!isOpen) return;
        // Move focus into the menu, landing on the currently selected item
        // (falling back to the first item) per the ARIA menu keyboard contract.
        const selectedIndex = COMPANY_STATUS_ORDER.indexOf(status);
        const target = itemRefs.current[selectedIndex] ?? itemRefs.current[0];
        target?.focus();
    }, [isOpen, status]);

    function closeAndReturnFocus() {
        setOpen(false);
        triggerRef.current?.focus();
    }

    function focusItem(index: number) {
        const count = COMPANY_STATUS_ORDER.length;
        const next = ((index % count) + count) % count;
        itemRefs.current[next]?.focus();
    }

    function handleMenuKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
        const currentIndex = itemRefs.current.findIndex((el) => el === document.activeElement);

        switch (event.key) {
            case "Escape":
                event.preventDefault();
                closeAndReturnFocus();
                break;
            case "ArrowDown":
                event.preventDefault();
                focusItem(currentIndex === -1 ? 0 : currentIndex + 1);
                break;
            case "ArrowUp":
                event.preventDefault();
                focusItem(currentIndex === -1 ? COMPANY_STATUS_ORDER.length - 1 : currentIndex - 1);
                break;
            case "Home":
                event.preventDefault();
                focusItem(0);
                break;
            case "End":
                event.preventDefault();
                focusItem(COMPANY_STATUS_ORDER.length - 1);
                break;
            case "Tab":
                // Menus trap tabbing inside them; leaving the menu should close it.
                closeAndReturnFocus();
                break;
            default:
                break;
        }
    }

    return (
        <div className="co-status-menu" ref={containerRef}>
            <button
                type="button"
                ref={triggerRef}
                className={`co-status-badge co-status-badge--interactive ${meta.toneClassName}`}
                onClick={() => setOpen((open) => !open)}
                onKeyDown={(event) => {
                    if (event.key === "ArrowDown" && !isOpen) {
                        event.preventDefault();
                        setOpen(true);
                    }
                }}
                disabled={disabled}
                aria-haspopup="menu"
                aria-expanded={isOpen}
            >
                {meta.label}
                <Icon name="chevron-down" size={14} />
            </button>
            {isOpen && (
                <div className="co-status-menu-list" role="menu" onKeyDown={handleMenuKeyDown}>
                    {COMPANY_STATUS_ORDER.map((statusOption, index) => {
                        const optionMeta = COMPANY_STATUS_META[statusOption];
                        return (
                            <button
                                key={statusOption}
                                type="button"
                                role="menuitem"
                                ref={(el) => {
                                    itemRefs.current[index] = el;
                                }}
                                tabIndex={-1}
                                className="co-status-menu-item"
                                onClick={() => {
                                    closeAndReturnFocus();
                                    if (statusOption !== status) onChange(statusOption);
                                }}
                            >
                                <span className={`co-status-dot ${optionMeta.toneClassName}`} aria-hidden="true" />
                                {optionMeta.label}
                                {statusOption === status && <Icon name="check" size={14} className="co-status-menu-check" />}
                            </button>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
