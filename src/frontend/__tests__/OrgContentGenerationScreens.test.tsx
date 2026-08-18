import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { CompletedRunPanel } from "@/features/org-content-generation/components/completed-run-panel";
import { ContentQueueCard } from "@/features/org-content-generation/components/content-queue-card";
import { FailedRunPanel } from "@/features/org-content-generation/components/failed-run-panel";
import { InsufficiencyPanel } from "@/features/org-content-generation/components/insufficiency-panel";
import { RunProgressPanel } from "@/features/org-content-generation/components/run-progress-panel";
import { StructureEditor } from "@/features/org-content-generation/components/structure-editor";
import { EMPTY_STRUCTURE_DRAFT } from "@/features/org-content-generation/utils/structure-draft";
import { describeOwnLessonsQueue } from "@/features/org-content-generation/utils/queue-copy";
import type { ContentInsufficiency } from "@/features/org-content-generation/types/content-generation";

/**
 * O11's layouts, rendered.
 *
 * The assertion this file exists for is the negative one: **no control anywhere on the refusal
 * offers to generate anyway.** The design says so and the reason is structural — the checkpoint and
 * the sufficiency threshold are the two things standing between a thin deck and a lesson nobody can
 * use, and one button labelled «всё равно» cancels both.
 */

const STRUCTURE_STAGE_REFUSAL: ContentInsufficiency = {
    stage: "structure",
    gaps: [
        {
            code: "no_objections",
            message:
                "В материале нет ни одного возражения клиента. Добавьте примеры возражений, которые менеджеры слышат чаще всего, или запись звонка.",
        },
        { code: "no_icp", message: "Не описано, кому вы продаёте." },
    ],
    note: "модель считает материал маркетинговой презентацией",
};

const MATERIAL_STAGE_REFUSAL: ContentInsufficiency = {
    stage: "material",
    gaps: [{ code: "too_short", message: "Материала слишком мало." }],
    note: null,
};

const BYPASS_WORDINGS = [
    /всё равно/i,
    /все равно/i,
    /принудительн/i,
    /игнорировать/i,
    /пропустить проверку/i,
];

function renderRefusal(insufficiency: ContentInsufficiency, canOpenStructure: boolean) {
    const onOpenStructure = vi.fn();
    const onSupplementMaterial = vi.fn();

    render(
        <InsufficiencyPanel
            insufficiency={insufficiency}
            canOpenStructure={canOpenStructure}
            onOpenStructure={onOpenStructure}
            onSupplementMaterial={onSupplementMaterial}
            isSupplementPending={false}
            supplementErrorMessage={null}
        />
    );

    return { onOpenStructure, onSupplementMaterial };
}

describe("the refusal is a screen, not a toast", () => {
    it("renders every gap as its own bullet, never joined into a paragraph", () => {
        renderRefusal(STRUCTURE_STAGE_REFUSAL, true);

        const bullets = screen.getAllByRole("listitem");
        expect(bullets).toHaveLength(2);
        expect(bullets[0].textContent).toContain("нет ни одного возражения клиента");
        expect(bullets[1].textContent).toContain("кому вы продаёте");
    });

    it("says why we are not generating, in the product's own words", () => {
        renderRefusal(STRUCTURE_STAGE_REFUSAL, true);

        expect(
            screen.getByText(/Четыре хороших упражнения лучше пятнадцати водянистых/)
        ).toBeTruthy();
    });

    it("never shows the model's diagnostic note to the customer", () => {
        const { container } = render(
            <InsufficiencyPanel
                insufficiency={STRUCTURE_STAGE_REFUSAL}
                canOpenStructure
                onOpenStructure={vi.fn()}
                onSupplementMaterial={vi.fn()}
                isSupplementPending={false}
                supplementErrorMessage={null}
            />
        );

        expect(container.textContent).not.toContain("маркетинговой презентацией");
    });

    it("offers NO «сгенерировать всё равно» control of any kind", () => {
        const { container } = render(
            <InsufficiencyPanel
                insufficiency={STRUCTURE_STAGE_REFUSAL}
                canOpenStructure
                onOpenStructure={vi.fn()}
                onSupplementMaterial={vi.fn()}
                isSupplementPending={false}
                supplementErrorMessage={null}
            />
        );

        for (const bypassWording of BYPASS_WORDINGS) {
            expect(container.textContent ?? "").not.toMatch(bypassWording);
        }

        const buttonLabels = screen.getAllByRole("button").map((button) => button.textContent ?? "");
        expect(buttonLabels).toEqual(expect.arrayContaining(["Добавить", "Открыть структуру"]));
        expect(buttonLabels.some((label) => /сгенерировать/i.test(label))).toBe(false);
    });

    it("offers «Открыть структуру» only when a structure exists to open", () => {
        renderRefusal(MATERIAL_STAGE_REFUSAL, false);

        expect(screen.queryByRole("button", { name: "Открыть структуру" })).toBeNull();
        expect(screen.getByRole("button", { name: "Добавить" })).toBeTruthy();
    });

    it("promises that added material is the only thing paid for a second time", () => {
        renderRefusal(MATERIAL_STAGE_REFUSAL, false);

        expect(screen.getByText(/за уже разобранное платить второй раз не придётся/)).toBeTruthy();
    });

    it("refuses to send an empty supplement and says what to paste instead", async () => {
        const user = userEvent.setup();
        const { onSupplementMaterial } = renderRefusal(MATERIAL_STAGE_REFUSAL, false);

        await user.click(screen.getByRole("button", { name: "Добавить" }));

        expect(onSupplementMaterial).not.toHaveBeenCalled();
        expect(screen.getByText(/Вставьте материал/)).toBeTruthy();
    });

    it("sends what was typed, once, when there is something to send", async () => {
        const user = userEvent.setup();
        const { onSupplementMaterial } = renderRefusal(MATERIAL_STAGE_REFUSAL, false);

        await user.type(
            screen.getByLabelText("Дополнительный материал"),
            "Клиенты говорят «дорого» и «уже есть поставщик»."
        );
        await user.click(screen.getByRole("button", { name: "Добавить" }));

        expect(onSupplementMaterial).toHaveBeenCalledTimes(1);
        expect(onSupplementMaterial).toHaveBeenCalledWith(
            "Клиенты говорят «дорого» и «уже есть поставщик»."
        );
    });
});

describe("the in-progress layout", () => {
    it("tells the reviewer that leaving is safe while the material is being read", () => {
        render(<RunProgressPanel status="structuring" startedAtLabel="18 августа, 09:14" />);

        expect(screen.getByRole("status").textContent).toContain("Разбираем материал…");
        expect(screen.getByRole("status").textContent).toContain("Можно закрыть страницу");
    });

    it("switches the headline for the half that is already paid for", () => {
        render(<RunProgressPanel status="generating" startedAtLabel="18 августа, 09:14" />);

        expect(screen.getByRole("status").textContent).toContain("Собираем упражнения…");
    });
});

describe("the checkpoint editor", () => {
    it("shows an empty list as «0 из 20 — пусто» rather than hiding the section", () => {
        render(<StructureEditor draft={EMPTY_STRUCTURE_DRAFT} onDraftChange={vi.fn()} />);

        expect(screen.getByText("Запрещённые обещания").textContent).toContain("(0 из 20)");
        expect(screen.getAllByText("пусто").length).toBe(4);
    });

    it("disables «+ добавить» once a list is at the server's cap", () => {
        render(
            <StructureEditor
                draft={{
                    ...EMPTY_STRUCTURE_DRAFT,
                    scriptStages: Array.from({ length: 12 }, (_, index) => `этап ${index}`),
                }}
                onDraftChange={vi.fn()}
            />
        );

        const addStageButton = screen.getByRole("button", { name: "Добавить этап скрипта" });
        expect((addStageButton as HTMLButtonElement).disabled).toBe(true);
    });

    it("adds one empty row per press, and hands the whole draft back", async () => {
        const user = userEvent.setup();
        const onDraftChange = vi.fn();

        render(<StructureEditor draft={EMPTY_STRUCTURE_DRAFT} onDraftChange={onDraftChange} />);
        await user.click(screen.getByRole("button", { name: "Добавить возражение" }));

        expect(onDraftChange).toHaveBeenCalledWith({
            ...EMPTY_STRUCTURE_DRAFT,
            objections: [{ text: "", bestResponse: "" }],
        });
    });
});

describe("the finished run", () => {
    it("says the lesson is hidden until somebody looks, and links to the ordinary editor", () => {
        render(
            <CompletedRunPanel
                jobId="job-1"
                title="Работа с ценой"
                producedLessonId="lesson-9"
                producedExerciseCount={12}
                onShowToTeam={vi.fn()}
                isShowToTeamPending={false}
                wasShownToTeam={false}
                showToTeamErrorMessage={null}
            />
        );

        expect(screen.getByText(/Урок скрыт от команды/)).toBeTruthy();
        expect(screen.getByRole("link", { name: "Открыть урок" }).getAttribute("href")).toBe(
            "/org/content/lessons/lesson-9"
        );
        expect(screen.getByRole("button", { name: "Показать команде" })).toBeTruthy();
    });

    it("does not offer to show a lesson whose exercises all failed validation", () => {
        render(
            <CompletedRunPanel
                jobId="job-1"
                title="Работа с ценой"
                producedLessonId="lesson-9"
                producedExerciseCount={0}
                onShowToTeam={vi.fn()}
                isShowToTeamPending={false}
                wasShownToTeam={false}
                showToTeamErrorMessage={null}
            />
        );

        expect(screen.queryByRole("button", { name: "Показать команде" })).toBeNull();
        expect(screen.getByRole("alert").textContent).toContain("Ни одно упражнение не прошло");
    });

    it("does not offer per-exercise accept/reject — that is a different queue", () => {
        const { container } = render(
            <CompletedRunPanel
                jobId="job-1"
                title="Работа с ценой"
                producedLessonId="lesson-9"
                producedExerciseCount={12}
                onShowToTeam={vi.fn()}
                isShowToTeamPending={false}
                wasShownToTeam={false}
                showToTeamErrorMessage={null}
            />
        );

        expect(container.textContent ?? "").not.toMatch(/принять|отклонить/i);
    });
});

describe("the failed run", () => {
    it("prints the recorded reason and says which half a retry will redo", () => {
        render(
            <FailedRunPanel
                failureReason="ai-service ответил 503"
                hasStructure
                onRetry={vi.fn()}
                isRetryPending={false}
                retryErrorMessage={null}
            />
        );

        expect(screen.getByText("ai-service ответил 503")).toBeTruthy();
        expect(screen.getByText(/платить второй раз не придётся/)).toBeTruthy();
    });

    it("says the reason was not recorded rather than leaving the line blank", () => {
        render(
            <FailedRunPanel
                failureReason={null}
                hasStructure={false}
                onRetry={vi.fn()}
                isRetryPending={false}
                retryErrorMessage={null}
            />
        );

        expect(screen.getByText("Причина не записана.")).toBeTruthy();
    });
});

describe("the O9 hub cards", () => {
    it("shows the explanation instead of a zero when the queue is empty", () => {
        render(
            <ContentQueueCard
                title="Собственные уроки"
                copy={describeOwnLessonsQueue({
                    awaitingReviewRunCount: 0,
                    insufficientRunCount: 0,
                    completedRunCount: 0,
                    totalRunCount: 0,
                    awaitingReviewProposalCount: 0,
                    staleOverrideCount: 0,
                    totalOverrideCount: 0,
                })}
                actionLabel="Сделать урок из материалов"
                actionHref="/org/content/generation?new=1"
                isLoading={false}
                hasCountFailure={false}
            />
        );

        expect(screen.getByText(/Загрузите материалы внутреннего тренинга/)).toBeTruthy();
        expect(screen.getByRole("link").getAttribute("href")).toBe(
            "/org/content/generation?new=1"
        );
    });

    it("says the count could not be read rather than showing a zero it did not measure", () => {
        render(
            <ContentQueueCard
                title="Свои версии"
                copy={{ lines: [], emptyDescription: "объяснение раздела" }}
                actionLabel="Разобрать очередь"
                actionHref="/org/content/overrides"
                isLoading={false}
                hasCountFailure
            />
        );

        expect(screen.getByText(/Не удалось прочитать очередь/)).toBeTruthy();
        expect(screen.queryByText("объяснение раздела")).toBeNull();
    });
});

describe("no gamification", () => {
    it("mentions no XP, no streaks and no leagues on any layout of the pipeline", () => {
        const { container } = render(
            <>
                <InsufficiencyPanel
                    insufficiency={STRUCTURE_STAGE_REFUSAL}
                    canOpenStructure
                    onOpenStructure={vi.fn()}
                    onSupplementMaterial={vi.fn()}
                    isSupplementPending={false}
                    supplementErrorMessage={null}
                />
                <RunProgressPanel status="generating" startedAtLabel="18 августа, 09:14" />
                <StructureEditor draft={EMPTY_STRUCTURE_DRAFT} onDraftChange={vi.fn()} />
                <CompletedRunPanel
                    jobId="job-1"
                    title="Работа с ценой"
                    producedLessonId="lesson-9"
                    producedExerciseCount={12}
                    onShowToTeam={vi.fn()}
                    isShowToTeamPending={false}
                    wasShownToTeam={false}
                    showToTeamErrorMessage={null}
                />
            </>
        );

        const renderedText = (container.textContent ?? "").toLowerCase();
        for (const forbiddenWord of ["xp", "опыт", "стрик", "streak", "лига", "league"]) {
            expect(renderedText).not.toContain(forbiddenWord);
        }
    });
});
