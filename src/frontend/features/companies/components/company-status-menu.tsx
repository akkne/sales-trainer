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

    return (
        <div className="co-status-menu" ref={containerRef}>
            <button
                type="button"
                className={`co-status-badge co-status-badge--interactive ${meta.toneClassName}`}
                onClick={() => setOpen((open) => !open)}
                disabled={disabled}
                aria-haspopup="menu"
                aria-expanded={isOpen}
            >
                {meta.label}
                <Icon name="chevron-down" size={14} />
            </button>
            {isOpen && (
                <div className="co-status-menu-list" role="menu">
                    {COMPANY_STATUS_ORDER.map((statusOption) => {
                        const optionMeta = COMPANY_STATUS_META[statusOption];
                        return (
                            <button
                                key={statusOption}
                                type="button"
                                role="menuitem"
                                className="co-status-menu-item"
                                onClick={() => {
                                    setOpen(false);
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
