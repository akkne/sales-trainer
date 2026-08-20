"use client";

import { useTeamMemberNames } from "@/features/org-shell/hooks/use-team-directory";
import type { AssignmentAudienceKind } from "@/features/org-assignments/types/assignment";

interface AudiencePickerProps {
    audienceKind: AssignmentAudienceKind;
    selectedUserIds: string[];
    onAudienceKindChange: (audienceKind: AssignmentAudienceKind) => void;
    onSelectedUserIdsChange: (userIds: string[]) => void;
    disabled?: boolean;
    error?: string | null;
}

/**
 * Who the assignment is for, as the rule it is stored as: «вся команда» keeps covering people hired
 * next week, a list of names is a photograph of this afternoon.
 *
 * The names come from `GET /admin/team/skill-map`, which knows only the people who have attempted
 * something — identity-service has no roster endpoint yet (ADMIN_UI_DESIGN.md §6.1). The caveat is
 * printed under the list rather than hidden, because a picker that silently omits a new hire is
 * worse than one that says it does.
 */
export function AudiencePicker({
    audienceKind,
    selectedUserIds,
    onAudienceKindChange,
    onSelectedUserIdsChange,
    disabled = false,
    error = null,
}: AudiencePickerProps) {
    const { memberNames, isRosterKnown, isLoading, isError } = useTeamMemberNames();

    const toggleMember = (userId: string) => {
        onSelectedUserIdsChange(
            selectedUserIds.includes(userId)
                ? selectedUserIds.filter((selectedId) => selectedId !== userId)
                : [...selectedUserIds, userId]
        );
    };

    return (
        <div className="flex flex-col gap-2">
            <label className="flex items-center gap-2 text-sm text-ink-2">
                <input
                    type="radio"
                    name="assignment-audience-kind"
                    checked={audienceKind === "whole_team"}
                    disabled={disabled}
                    onChange={() => onAudienceKindChange("whole_team")}
                />
                Вся команда
            </label>

            <label className="flex items-center gap-2 text-sm text-ink-2">
                <input
                    type="radio"
                    name="assignment-audience-kind"
                    checked={audienceKind === "users"}
                    disabled={disabled}
                    onChange={() => onAudienceKindChange("users")}
                />
                Выбрать людей
            </label>

            {audienceKind === "users" && (
                <div className="ml-6 flex flex-col gap-2">
                    {isLoading && <p className="text-xs text-ink-3">Загружаем состав команды…</p>}

                    {isError && (
                        <p className="text-xs" style={{ color: "var(--heart)" }}>
                            Не удалось получить список людей. Можно выдать задание всей команде.
                        </p>
                    )}

                    {!isLoading && !isError && memberNames.length === 0 && (
                        <p className="text-xs text-ink-3">
                            Пока некого выбрать: ни у кого из команды нет решённых заданий.
                            Используйте «вся команда».
                        </p>
                    )}

                    <div className="flex max-h-56 flex-col gap-1 overflow-y-auto">
                        {memberNames.map((member) => (
                            <label
                                key={member.userId}
                                className="flex items-center gap-2 text-sm text-ink-2"
                            >
                                <input
                                    type="checkbox"
                                    checked={selectedUserIds.includes(member.userId)}
                                    disabled={disabled}
                                    onChange={() => toggleMember(member.userId)}
                                />
                                <span>{member.displayName}</span>
                                {isRosterKnown && member.isActiveMember === false && (
                                    <span className="text-xs text-ink-4">
                                        уже не работает в компании
                                    </span>
                                )}
                            </label>
                        ))}
                    </div>

                    <p className="text-xs text-ink-4">
                        Здесь только те, кто уже что-то решал. Новичка без единой попытки можно
                        охватить через «вся команда».
                    </p>
                </div>
            )}

            {error && (
                <p className="text-xs" style={{ color: "var(--heart)" }} role="alert">
                    {error}
                </p>
            )}
        </div>
    );
}
