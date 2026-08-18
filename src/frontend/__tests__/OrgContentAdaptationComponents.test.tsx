import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
    ApiError: class ApiError extends Error {
        readonly status: number;
        readonly payload: Record<string, unknown>;
        constructor(status: number, payload: Record<string, unknown>) {
            super("api error");
            this.status = status;
            this.payload = payload;
        }
    },
}));

vi.mock("next/link", () => ({
    default: ({ children, href }: { children: ReactNode; href: string }) => (
        <a href={href}>{children}</a>
    ),
}));

import { apiClient } from "@/shared/api/api-client";
import { FindingList } from "@/features/org-content-adaptation/components/finding-list";
import { ProposalDetailPanel } from "@/features/org-content-adaptation/components/proposal-detail-panel";
import { ProposalDiffView } from "@/features/org-content-adaptation/components/proposal-diff-view";
import { ProposalQueueList } from "@/features/org-content-adaptation/components/proposal-queue-list";
import type {
    ContentAdaptationItem,
    ContentAdaptationItemSummary,
} from "@/features/org-content-adaptation/types/adaptation";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

function renderWithQueryClient(node: ReactNode) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    return render(<QueryClientProvider client={queryClient}>{node}</QueryClientProvider>);
}

function buildItemSummary(
    overrides: Partial<ContentAdaptationItemSummary> = {}
): ContentAdaptationItemSummary {
    return {
        id: "item-1",
        exerciseId: "exercise-1",
        lessonId: "lesson-a",
        lessonTitle: "Работа с ценой",
        exerciseType: "choose_option",
        orderInLesson: 1,
        status: "proposed",
        changeSummary: "Заменил абстрактную выгоду на ваш срок внедрения.",
        findingCount: 0,
        hasBlockingFinding: false,
        changedFieldCount: 2,
        failureReason: null,
        resolvedAt: null,
        ...overrides,
    };
}

function buildItem(overrides: Partial<ContentAdaptationItem> = {}): ContentAdaptationItem {
    return {
        summary: buildItemSummary(),
        currentContent: { situation: "Клиент считает, что дорого" },
        proposedContent: { situation: "Клиент говорит, что у конкурента дешевле" },
        changes: [
            {
                path: "situation",
                before: "Клиент считает, что дорого",
                after: "Клиент говорит, что у конкурента дешевле",
            },
        ],
        findings: [],
        isStale: false,
        ...overrides,
    };
}

describe("ProposalDiffView", () => {
    it("puts the model's sentence before the list of changed leaves", () => {
        const { container } = render(
            <ProposalDiffView
                changeSummary="Заменил абстрактную выгоду на ваш срок внедрения."
                changes={[{ path: "options[1].text", before: "раньше", after: "теперь" }]}
                hasProposedContent
            />
        );

        const renderedText = container.textContent ?? "";
        expect(renderedText.indexOf("Заменил абстрактную")).toBeLessThan(
            renderedText.indexOf("options[1].text")
        );
    });

    it("collapses a long change list behind «… ещё N» rather than dumping it", async () => {
        render(
            <ProposalDiffView
                changeSummary="Правка тона."
                changes={Array.from({ length: 7 }, (_, changeIndex) => ({
                    path: `options[${changeIndex}].text`,
                    before: "было",
                    after: "стало",
                }))}
                hasProposedContent
            />
        );

        expect(screen.getByText("… ещё 3")).toBeTruthy();
        expect(screen.queryByText("options[6].text")).toBeNull();

        await userEvent.click(screen.getByText("… ещё 3"));

        expect(screen.getByText("options[6].text")).toBeTruthy();
    });

    it("refuses to compute a diff when the server sent no change list", () => {
        render(<ProposalDiffView changeSummary="Правка." changes={[]} hasProposedContent />);

        expect(screen.getByText(/ничего не досчитываем/)).toBeTruthy();
    });

    it("says the model has not reached this exercise when there is no proposal at all", () => {
        render(<ProposalDiffView changeSummary={null} changes={[]} hasProposedContent={false} />);

        expect(screen.getByText(/модель до этого упражнения не дошла/)).toBeTruthy();
    });
});

describe("FindingList", () => {
    it("puts blocking findings above advisory ones", () => {
        const { container } = render(
            <FindingList
                findings={[
                    {
                        code: "missing_explanation",
                        severity: "advisory",
                        message: "Не объяснено, почему верный ответ верен.",
                        detail: null,
                    },
                    {
                        code: "banned_claim_rewarded",
                        severity: "blocking",
                        message: "Верный ответ содержит обещание из вашего списка запрещённых.",
                        detail: "гарантируем рост выручки",
                    },
                ]}
            />
        );

        const renderedText = container.textContent ?? "";
        expect(renderedText.indexOf("Поощряется запрещённое обещание")).toBeLessThan(
            renderedText.indexOf("Нет объяснения")
        );
    });

    it("renders the server's sentence verbatim and the quoted fragment beside it", () => {
        render(
            <FindingList
                findings={[
                    {
                        code: "unmeasurable_criteria",
                        severity: "blocking",
                        message: "Критерии оценки свободного ответа нельзя проверить.",
                        detail: "ответил хорошо",
                    },
                ]}
            />
        );

        expect(screen.getByText("Критерии оценки свободного ответа нельзя проверить.")).toBeTruthy();
        expect(screen.getByText("ответил хорошо")).toBeTruthy();
    });

    it("prints a code outside the seven as the code itself", () => {
        render(
            <FindingList
                findings={[
                    {
                        code: "invented_by_the_model",
                        severity: "advisory",
                        message: "Что-то не так.",
                        detail: null,
                    },
                ]}
            />
        );

        expect(screen.getByText("invented_by_the_model")).toBeTruthy();
    });

    it("treats finding nothing as the expected answer, not as an empty screen", () => {
        render(<FindingList findings={[]} />);

        expect(screen.getByText("Замечаний нет")).toBeTruthy();
    });
});

describe("ProposalQueueList", () => {
    const items = [
        buildItemSummary({ id: "one", orderInLesson: 1, status: "accepted" }),
        buildItemSummary({ id: "two", orderInLesson: 2, status: "proposed" }),
        buildItemSummary({
            id: "three",
            lessonId: "lesson-b",
            lessonTitle: "Дожим",
            orderInLesson: 1,
            status: "unchanged",
        }),
    ];

    it("groups rows under their lesson and numbers them in reading order", () => {
        render(
            <ProposalQueueList
                items={items}
                mode="tone_rewrite"
                selectedItemId="two"
                onSelect={() => {}}
            />
        );

        expect(screen.getByText("Работа с ценой")).toBeTruthy();
        expect(screen.getByText("Дожим")).toBeTruthy();
        expect(screen.getByText("принято")).toBeTruthy();
        expect(screen.getByText("без изменений")).toBeTruthy();
    });

    it("reports the selected row to assistive technology", () => {
        render(
            <ProposalQueueList
                items={items}
                mode="tone_rewrite"
                selectedItemId="two"
                onSelect={() => {}}
            />
        );

        const selectedRows = screen
            .getAllByRole("button")
            .filter((row) => row.getAttribute("aria-current") === "true");

        expect(selectedRows).toHaveLength(1);
    });

    it("hands the clicked item id back, from the alphabetically first lesson down", async () => {
        const onSelect = vi.fn();
        render(
            <ProposalQueueList
                items={items}
                mode="tone_rewrite"
                selectedItemId="two"
                onSelect={onSelect}
            />
        );

        // «Дожим» sorts before «Работа с ценой», so the first row is that lesson's only item.
        await userEvent.click(screen.getAllByRole("button")[0]);

        expect(onSelect).toHaveBeenCalledWith("three");
    });
});

describe("ProposalDetailPanel", () => {
    beforeEach(() => {
        mockGet.mockReset();
    });

    it("offers «Принять» on a fresh rewrite, with the publishing caveat under it", async () => {
        mockGet.mockResolvedValue(buildItem());

        renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="tone_rewrite"
                itemId="item-1"
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        await waitFor(() => expect(screen.getByText("Принять")).toBeTruthy());
        expect(
            screen.getByText(/команда увидит её только после публикации новой версии урока/)
        ).toBeTruthy();
    });

    it("has no «Применить всё» anywhere — the block the whole feature exists for", async () => {
        mockGet.mockResolvedValue(buildItem());

        const { container } = renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="tone_rewrite"
                itemId="item-1"
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        await waitFor(() => expect(screen.getByText("Принять")).toBeTruthy());
        expect(container.textContent).not.toMatch(/Применить всё|Принять все/i);
    });

    it("disables «Принять» on a stale item and says why", async () => {
        mockGet.mockResolvedValue(buildItem({ isStale: true }));

        renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="tone_rewrite"
                itemId="item-1"
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        await waitFor(() =>
            expect(screen.getByText("Принять").closest("button")?.disabled).toBe(true)
        );
        expect(screen.getByText(/Запустите пакет заново/)).toBeTruthy();
    });

    it("renders no «Принять» at all in review mode, and offers the two real fixes instead", async () => {
        mockGet.mockResolvedValue(
            buildItem({
                summary: buildItemSummary({ changeSummary: null, findingCount: 1, hasBlockingFinding: true }),
                proposedContent: null,
                changes: [],
                findings: [
                    {
                        code: "obvious_distractors",
                        severity: "advisory",
                        message: "Неверные варианты слишком очевидны.",
                        detail: null,
                    },
                ],
            })
        );

        renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="quality_review"
                itemId="item-1"
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        await waitFor(() => expect(screen.getByText("Отклонить")).toBeTruthy());
        expect(screen.queryByText("Принять")).toBeNull();
        expect(screen.getByText("Открыть упражнение")).toBeTruthy();
        expect(screen.getByText("Переписать этот этап под нас")).toBeTruthy();
    });

    it("asks a person to pick something when nothing is selected", () => {
        renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="tone_rewrite"
                itemId={null}
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        expect(screen.getByText("Выберите предложение слева")).toBeTruthy();
    });

    it("offers a retry when the one item cannot be read", async () => {
        mockGet.mockRejectedValue(new Error("boom"));

        renderWithQueryClient(
            <ProposalDetailPanel
                jobId="job-1"
                mode="tone_rewrite"
                itemId="item-1"
                nextAwaitingItemId={null}
                onOpenNext={() => {}}
                onRewriteStage={() => {}}
            />
        );

        await waitFor(() => expect(screen.getByText("Не удалось загрузить предложение")).toBeTruthy());
    });
});
