"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Select } from "@/shared/components/input";
import {
    DEFAULT_PANEL_SCORE_CEILING,
    PANEL_SCORE_CEILING_CHOICES,
} from "@/features/org-dialogs/lib/dialog-score";

export interface DialogScenarioOption {
    modeId: string;
    title: string;
}

export interface DialogSessionFilterSelection {
    userId: string | null;
    modeId: string | null;
    maximumPanelScore: number | null;
}

interface DialogSessionFiltersProps {
    memberOptions: { userId: string; displayName: string }[];
    scenarioOptions: DialogScenarioOption[];
    /**
     * What the list is filtered by right now, and the draft's starting point. It is the *initial*
     * value only: a change made elsewhere resets this form by remounting it under a new `key`,
     * which is React's own answer to "props changed, throw the local state away".
     */
    initialSelection: DialogSessionFilterSelection;
    onApply: (selection: DialogSessionFilterSelection) => void;
    isRefreshing: boolean;
}

const ALL_OPTION_VALUE = "";

/**
 * The three filters of O5 (docs/TENANCY/ADMIN_UI_DESIGN.md O5).
 *
 * The grade ceiling is the important one and it opens preset at 60: a list of every conversation
 * the team ever held is not an agenda for Monday, and a list of the ones that went badly is.
 *
 * It is a `Select` of whole tens rather than a free number box because the underlying grade is
 * 0–10 (see `dialog-score.ts`) — a box would accept 65 and quietly search for 60, which is a
 * control that lies about what it did.
 *
 * The scenario list is distinct over the rows already on screen, not a separate request: no
 * endpoint returns the dialog modes an organization actually uses.
 */
export function DialogSessionFilters({
    memberOptions,
    scenarioOptions,
    initialSelection,
    onApply,
    isRefreshing,
}: DialogSessionFiltersProps) {
    const [draft, setDraft] = useState<DialogSessionFilterSelection>(initialSelection);

    return (
        <form
            className="flex flex-wrap items-end gap-3 mb-6"
            onSubmit={(submitEvent) => {
                submitEvent.preventDefault();
                onApply(draft);
            }}
        >
            <div className="min-w-[180px]">
                <Select
                    label="Менеджер"
                    inputSize="sm"
                    value={draft.userId ?? ALL_OPTION_VALUE}
                    onChange={(changeEvent) =>
                        setDraft({
                            ...draft,
                            userId: changeEvent.target.value || null,
                        })
                    }
                >
                    <option value={ALL_OPTION_VALUE}>все</option>
                    {memberOptions.map((member) => (
                        <option key={member.userId} value={member.userId}>
                            {member.displayName}
                        </option>
                    ))}
                </Select>
            </div>

            <div className="min-w-[180px]">
                <Select
                    label="Сценарий"
                    inputSize="sm"
                    value={draft.modeId ?? ALL_OPTION_VALUE}
                    onChange={(changeEvent) =>
                        setDraft({
                            ...draft,
                            modeId: changeEvent.target.value || null,
                        })
                    }
                >
                    <option value={ALL_OPTION_VALUE}>все</option>
                    {scenarioOptions.map((scenario) => (
                        <option key={scenario.modeId} value={scenario.modeId}>
                            {scenario.title}
                        </option>
                    ))}
                </Select>
            </div>

            <div className="min-w-[150px]">
                <Select
                    label="Оценка не выше"
                    inputSize="sm"
                    value={
                        draft.maximumPanelScore === null
                            ? ALL_OPTION_VALUE
                            : String(draft.maximumPanelScore)
                    }
                    onChange={(changeEvent) =>
                        setDraft({
                            ...draft,
                            maximumPanelScore: changeEvent.target.value
                                ? Number(changeEvent.target.value)
                                : null,
                        })
                    }
                >
                    <option value={ALL_OPTION_VALUE}>без ограничения</option>
                    {PANEL_SCORE_CEILING_CHOICES.map((ceiling) => (
                        <option key={ceiling} value={ceiling}>
                            {ceiling}
                            {ceiling === DEFAULT_PANEL_SCORE_CEILING ? " (по умолчанию)" : ""}
                        </option>
                    ))}
                </Select>
            </div>

            <Button type="submit" variant="secondary" loading={isRefreshing}>
                Показать
            </Button>
        </form>
    );
}
