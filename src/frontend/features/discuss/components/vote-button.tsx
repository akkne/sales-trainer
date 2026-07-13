"use client";

import { Icon } from "@/shared/components/icon";

interface VoteButtonProps {
    count: number;
    active: boolean;
    onToggle: () => void;
    disabled?: boolean;
}

/** Upvote column — up-chevron + bold count. Used on thread rows and replies. */
export function VoteButton({ count, active, onToggle, disabled }: VoteButtonProps) {
    return (
        <div className="dsc-vote">
            <button
                type="button"
                className={`vote-btn${active ? " on" : ""}`}
                onClick={(event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    onToggle();
                }}
                disabled={disabled}
                aria-pressed={active}
                aria-label={active ? "Убрать голос" : "Голосовать"}
            >
                <Icon name="chevron-up" size={16} />
            </button>
            <b className="num">{count}</b>
        </div>
    );
}
