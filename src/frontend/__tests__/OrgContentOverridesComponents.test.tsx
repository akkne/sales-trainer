import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { AccuracySeriesChart } from "@/features/org-content-overrides/components/accuracy-series-chart";
import { OverrideStateBadge } from "@/features/org-content-overrides/components/override-state-badge";
import { PublishDialog } from "@/features/org-content-overrides/components/publish-dialog";
import { ThreeWayCompare } from "@/features/org-content-overrides/components/three-way-compare";
import { UnpublishedDraftBanner } from "@/features/org-content-overrides/components/unpublished-draft-banner";
import type { LessonAccuracySeries } from "@/features/org-content-overrides/types/lesson-editor";

/**
 * The parts of O15 and O19 whose failure mode is silence: a review screen that grows an «apply»
 * button, a chart that joins two segments, a publish dialog that pre-answers the one question it
 * exists to ask.
 */
describe("ThreeWayCompare", () => {
    const lessonSnapshot = {
        title: "Работа с ценой",
        schemaVersion: 1,
        exercises: [] as unknown[],
    };

    it("draws three columns when the base at fork is known", () => {
        render(
            <ThreeWayCompare
                columns={[
                    { key: "fork", title: "База на момент копирования", subtitle: "версия 3", document: lessonSnapshot },
                    { key: "own", title: "Ваша версия", subtitle: "с вашими правками", document: lessonSnapshot },
                    { key: "base", title: "База сейчас", subtitle: "версия 5", document: lessonSnapshot },
                ]}
            />
        );

        expect(screen.getByText("База на момент копирования")).toBeTruthy();
        expect(screen.getByText("Ваша версия")).toBeTruthy();
        expect(screen.getByText("База сейчас")).toBeTruthy();
    });

    it("drops to two columns and explains why, when the family has no version history", () => {
        render(
            <ThreeWayCompare
                columns={[
                    { key: "own", title: "Ваша версия", subtitle: "с вашими правками", document: { name: "Три да" } },
                    { key: "base", title: "База сейчас", subtitle: "текущий оригинал", document: { name: "Три «да»" } },
                ]}
                missingBaseAtForkNotice="Каким оригинал был в момент копирования, мы не знаем."
            />
        );

        expect(screen.queryByText("База на момент копирования")).toBeNull();
        expect(screen.getByText("Каким оригинал был в момент копирования, мы не знаем.")).toBeTruthy();
    });

    it("marks a differing block and offers no button beside it — there is no merge to trigger", () => {
        render(
            <ThreeWayCompare
                columns={[
                    { key: "own", title: "Ваша версия", subtitle: "", document: { title: "Наш заголовок" } },
                    { key: "base", title: "База сейчас", subtitle: "", document: { title: "Общий заголовок" } },
                ]}
            />
        );

        expect(screen.getByText("блок отличается")).toBeTruthy();
        expect(screen.queryAllByRole("button")).toHaveLength(0);
    });

    it("says so plainly when the server returned nothing to compare", () => {
        render(
            <ThreeWayCompare
                columns={[{ key: "own", title: "Ваша версия", subtitle: "", document: null }]}
            />
        );

        expect(screen.getByText(/пустые документы/)).toBeTruthy();
    });
});

describe("OverrideStateBadge", () => {
    it("labels each state with the design's own wording", () => {
        const { rerender } = render(<OverrideStateBadge state="base_moved" />);
        expect(screen.getByText("оригинал обновился")).toBeTruthy();

        rerender(<OverrideStateBadge state="base_unknown" />);
        expect(screen.getByText("основа неизвестна")).toBeTruthy();

        rerender(<OverrideStateBadge state="in_sync" />);
        expect(screen.getByText("совпадает с базой")).toBeTruthy();
    });
});

describe("PublishDialog", () => {
    it("keeps the publish button disabled until the one mandatory question is answered", async () => {
        const user = userEvent.setup();
        const onConfirm = vi.fn();
        render(
            <PublishDialog open onCancel={vi.fn()} onConfirm={onConfirm} isPending={false} />
        );

        const publishButton = screen.getByRole("button", { name: "Опубликовать" });
        expect(publishButton.hasAttribute("disabled")).toBe(true);

        await user.click(screen.getByRole("radio", { name: /Косметика/ }));
        await user.click(screen.getByRole("button", { name: "Опубликовать" }));

        expect(onConfirm).toHaveBeenCalledWith(false);
    });

    it("passes isBreaking: true for a semantic change", async () => {
        const user = userEvent.setup();
        const onConfirm = vi.fn();
        render(
            <PublishDialog open onCancel={vi.fn()} onConfirm={onConfirm} isPending={false} />
        );

        await user.click(screen.getByRole("radio", { name: /По смыслу/ }));
        await user.click(screen.getByRole("button", { name: "Опубликовать" }));

        expect(onConfirm).toHaveBeenCalledWith(true);
    });

    it("shows the server's «nothing to publish» answer instead of moving a version number", () => {
        render(
            <PublishDialog
                open
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
                isPending={false}
                notice="Изменений нет — публиковать нечего."
            />
        );

        expect(screen.getByText("Изменений нет — публиковать нечего.")).toBeTruthy();
    });
});

describe("UnpublishedDraftBanner", () => {
    it("names the version the team is still answering", () => {
        render(
            <UnpublishedDraftBanner publishedVersionNumber={4} onPublish={vi.fn()} isPublishing={false} />
        );

        expect(screen.getByText(/версию 4/)).toBeTruthy();
        expect(screen.getByRole("button", { name: "Опубликовать" })).toBeTruthy();
    });
});

describe("AccuracySeriesChart", () => {
    const statistics = {
        attemptCount: 10,
        correctAttemptCount: 6,
        accuracy: 0.6,
        averageScore: 60,
        firstAttemptAt: null,
        lastAttemptAt: null,
    };

    it("explains an empty series rather than drawing an axis with nothing on it", () => {
        const series: LessonAccuracySeries = {
            lessonId: "lesson-1",
            segments: [],
            unversionedAttempts: { ...statistics, attemptCount: 0, accuracy: 0 },
        };

        render(<AccuracySeriesChart series={series} />);
        expect(screen.getByText(/нет опубликованных версий/)).toBeTruthy();
    });

    it("puts unversioned attempts in a footnote and never on the axis", () => {
        const series: LessonAccuracySeries = {
            lessonId: "lesson-1",
            segments: [
                {
                    startVersionNumber: 1,
                    endVersionNumber: 1,
                    versionNumbers: [1],
                    versionIds: ["v1"],
                    startsAtBreakingChange: false,
                    statistics,
                },
            ],
            unversionedAttempts: { ...statistics, attemptCount: 340 },
        };

        render(<AccuracySeriesChart series={series} />);

        expect(screen.getByText(/340 попыток записаны до появления версий/)).toBeTruthy();
        expect(screen.queryByText("v0")).toBeNull();
    });
});
