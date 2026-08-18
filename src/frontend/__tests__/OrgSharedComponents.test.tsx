import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { MetricBar } from "@/shared/components/metric-bar";
import { Modal } from "@/shared/components/modal";
import { PageHeader } from "@/shared/components/page-header";
import { Tabs } from "@/shared/components/tabs";

/**
 * The seven components block 40.20 adds to `shared/components`. They exist because the
 * organization panel would otherwise have written each of them by hand a sixth or eighth time;
 * what is tested here is the behaviour those hand-written copies kept getting wrong — dismissal,
 * empty states, overflow of a bar past its own limit.
 */
describe("Modal", () => {
    it("renders nothing while closed", () => {
        render(
            <Modal open={false} onClose={vi.fn()} title="Напоминание">
                Содержимое
            </Modal>
        );
        expect(screen.queryByRole("dialog")).toBeNull();
    });

    it("announces itself as a modal dialog labelled by its title", () => {
        render(
            <Modal open onClose={vi.fn()} title="Напоминание">
                Содержимое
            </Modal>
        );

        const dialog = screen.getByRole("dialog");
        expect(dialog.getAttribute("aria-modal")).toBe("true");
        expect(screen.getByRole("heading", { name: "Напоминание" })).toBeTruthy();
    });

    it("closes on Escape and on the close button", async () => {
        const user = userEvent.setup();
        const onClose = vi.fn();
        render(
            <Modal open onClose={onClose} title="Напоминание">
                Содержимое
            </Modal>
        );

        await user.keyboard("{Escape}");
        expect(onClose).toHaveBeenCalledTimes(1);

        await user.click(screen.getByLabelText("Закрыть"));
        expect(onClose).toHaveBeenCalledTimes(2);
    });

    it("renders its footer actions", () => {
        render(
            <Modal open onClose={vi.fn()} title="Напоминание" footer={<button>Отправить</button>}>
                Содержимое
            </Modal>
        );
        expect(screen.getByRole("button", { name: "Отправить" })).toBeTruthy();
    });
});

describe("ConfirmDialog", () => {
    it("puts the verb on the confirming button rather than «ОК»", async () => {
        const user = userEvent.setup();
        const onConfirm = vi.fn();
        const onCancel = vi.fn();

        render(
            <ConfirmDialog
                open
                title="Закрыть задание?"
                body="Менеджеры перестанут видеть его в списке."
                confirmLabel="Закрыть задание"
                tone="danger"
                onConfirm={onConfirm}
                onCancel={onCancel}
            />
        );

        await user.click(screen.getByRole("button", { name: "Закрыть задание" }));
        expect(onConfirm).toHaveBeenCalledTimes(1);

        await user.click(screen.getByRole("button", { name: "Отмена" }));
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it("disables the way out while the request is in flight", () => {
        render(
            <ConfirmDialog
                open
                title="Опубликовать?"
                body="Черновик станет текущей версией."
                confirmLabel="Опубликовать"
                onConfirm={vi.fn()}
                onCancel={vi.fn()}
                isPending
            />
        );

        expect(screen.getByRole("button", { name: "Отмена" }).hasAttribute("disabled")).toBe(true);
    });
});

interface MemberRow {
    userId: string;
    displayName: string;
    accuracyPercent: number | null;
}

const MEMBER_COLUMNS: Column<MemberRow>[] = [
    { key: "displayName", header: "Человек", render: (row) => row.displayName, sortable: true },
    {
        key: "accuracyPercent",
        header: "Точность",
        align: "right",
        render: (row) => (row.accuracyPercent === null ? "—" : `${row.accuracyPercent}%`),
    },
];

const MEMBER_ROWS: MemberRow[] = [
    { userId: "1", displayName: "Анна", accuracyPercent: 82 },
    { userId: "2", displayName: "Борис", accuracyPercent: null },
];

describe("DataTable", () => {
    it("lists the rows through their column renderers", () => {
        render(
            <DataTable
                columns={MEMBER_COLUMNS}
                rows={MEMBER_ROWS}
                rowKey={(row) => row.userId}
                empty={<EmptyState title="Пока никого" />}
            />
        );

        expect(screen.getByText("Анна")).toBeTruthy();
        expect(screen.getByText("82%")).toBeTruthy();
        expect(screen.getByText("—")).toBeTruthy();
    });

    it("shows the caller's empty state instead of an empty table", () => {
        render(
            <DataTable
                columns={MEMBER_COLUMNS}
                rows={[]}
                rowKey={(row) => row.userId}
                empty={<EmptyState title="Пока никого" />}
            />
        );

        expect(screen.queryByRole("table")).toBeNull();
        expect(screen.getByText("Пока никого")).toBeTruthy();
    });

    it("shows a skeleton while loading, not the empty state", () => {
        render(
            <DataTable
                columns={MEMBER_COLUMNS}
                rows={[]}
                rowKey={(row) => row.userId}
                empty={<EmptyState title="Пока никого" />}
                isLoading
            />
        );

        expect(screen.queryByText("Пока никого")).toBeNull();
        expect(screen.getByLabelText("Загрузка...")).toBeTruthy();
    });

    it("reports the current sort and flips direction on the sorted column", async () => {
        const user = userEvent.setup();
        const onSortChange = vi.fn();

        render(
            <DataTable
                columns={MEMBER_COLUMNS}
                rows={MEMBER_ROWS}
                rowKey={(row) => row.userId}
                empty={<EmptyState title="Пока никого" />}
                sort={{ key: "displayName", direction: "asc" }}
                onSortChange={onSortChange}
            />
        );

        expect(screen.getByRole("columnheader", { name: /Человек/ }).getAttribute("aria-sort")).toBe(
            "ascending"
        );

        await user.click(screen.getByRole("button", { name: /Человек/ }));
        expect(onSortChange).toHaveBeenCalledWith({ key: "displayName", direction: "desc" });
    });

    it("hands the clicked row back whole", async () => {
        const user = userEvent.setup();
        const onRowClick = vi.fn();

        render(
            <DataTable
                columns={MEMBER_COLUMNS}
                rows={MEMBER_ROWS}
                rowKey={(row) => row.userId}
                empty={<EmptyState title="Пока никого" />}
                onRowClick={onRowClick}
            />
        );

        await user.click(screen.getByText("Борис"));
        expect(onRowClick).toHaveBeenCalledWith(MEMBER_ROWS[1]);
    });
});

describe("EmptyState", () => {
    it("explains the section and offers its action", () => {
        render(
            <EmptyState
                icon="warning"
                title="Спорных оценок нет"
                description="Сюда попадают разговоры, оценку которых менеджер оспорил."
                action={<button>Открыть разговоры</button>}
            />
        );

        expect(screen.getByText("Спорных оценок нет")).toBeTruthy();
        expect(
            screen.getByText("Сюда попадают разговоры, оценку которых менеджер оспорил.")
        ).toBeTruthy();
        expect(screen.getByRole("button", { name: "Открыть разговоры" })).toBeTruthy();
    });
});

describe("PageHeader", () => {
    it("renders the title, the subtitle and one action", () => {
        render(
            <PageHeader
                title="Задания"
                subtitle="Что выдано команде и как идёт."
                action={<button>Новое задание</button>}
            />
        );

        expect(screen.getByRole("heading", { name: "Задания" })).toBeTruthy();
        expect(screen.getByText("Что выдано команде и как идёт.")).toBeTruthy();
        expect(screen.getByRole("button", { name: "Новое задание" })).toBeTruthy();
    });

    it("offers a way back only when given one", () => {
        const { rerender } = render(<PageHeader title="Задание" />);
        expect(screen.queryByRole("link")).toBeNull();

        rerender(<PageHeader title="Задание" backHref="/org/assignments" backLabel="К заданиям" />);
        const backLink = screen.getByRole("link", { name: "К заданиям" });
        expect(backLink.getAttribute("href")).toBe("/org/assignments");
    });
});

describe("Tabs", () => {
    it("marks the active tab and reports a switch", async () => {
        const user = userEvent.setup();
        const onChange = vi.fn();

        render(
            <Tabs
                items={[
                    { key: "disputes", label: "Оспаривания", badge: 2 },
                    { key: "notes", label: "Заметки", badge: 0 },
                ]}
                activeKey="disputes"
                onChange={onChange}
            />
        );

        expect(screen.getByRole("tab", { name: /Оспаривания/ }).getAttribute("aria-selected")).toBe(
            "true"
        );
        expect(screen.getByText("2")).toBeTruthy();

        await user.click(screen.getByRole("tab", { name: "Заметки" }));
        expect(onChange).toHaveBeenCalledWith("notes");
    });

    it("omits a zero badge rather than drawing it", () => {
        render(
            <Tabs
                items={[{ key: "notes", label: "Заметки", badge: 0 }]}
                activeKey="notes"
                onChange={vi.fn()}
            />
        );
        expect(screen.queryByText("0")).toBeNull();
    });
});

describe("MetricBar", () => {
    it("shows the consumption against its ceiling", () => {
        render(<MetricBar label="Минуты голоса" value={120} limit={600} tone="success" />);

        const bar = screen.getByRole("progressbar", { name: "Минуты голоса" });
        expect(bar.getAttribute("aria-valuenow")).toBe("120");
        expect(bar.getAttribute("aria-valuemax")).toBe("600");
        expect(screen.getByText("120")).toBeTruthy();
    });

    it("never draws a fill past the track when the organization is over quota", () => {
        const { container } = render(<MetricBar label="Генерации" value={900} limit={600} />);

        const fill = container.querySelector('[role="progressbar"] > div') as HTMLElement;
        expect(fill.style.width).toBe("100%");
    });

    it("draws an empty track and a dash when no ceiling is configured", () => {
        const { container } = render(<MetricBar label="Генерации" value={12} limit={0} />);

        const fill = container.querySelector('[role="progressbar"] > div') as HTMLElement;
        expect(fill.style.width).toBe("0%");
        expect(screen.getByText("/ —")).toBeTruthy();
    });

    it("formats both numbers through the caller's formatter", () => {
        render(
            <MetricBar
                label="Токены"
                value={12000}
                limit={50000}
                formatter={(value) => `${Math.round(value / 1000)}к`}
            />
        );

        expect(screen.getByText("12к")).toBeTruthy();
        expect(screen.getByText("/ 50к")).toBeTruthy();
    });
});
