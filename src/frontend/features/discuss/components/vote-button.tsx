"use client";

import { Icon } from "@/shared/components/icon";

interface VoteButtonProps {
    count: number;
    active: boolean;
    onToggle: () => void;
    disabled?: boolean;
}

/** The upvote column used on threads and replies (mirrors the .dsc-vote design). */
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
                aria-label={active ? "Убрать голос" : "Проголосовать"}
            >
                <Icon name="arrow-up" size={20} />
            </button>
            <b className="num">{count}</b>
            <span className="dsc-vote-l">голосов</span>
        </div>
    );
}
