import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CallLogForm } from "@/features/companies/components/call-log-form";
import type { CallLogEntry } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";

describe("CallLogForm", () => {
    const onSubmit = vi.fn();
    const onCancel = vi.fn();

    beforeEach(() => {
        onSubmit.mockReset();
        onCancel.mockReset();
    });

    it("disables save until the required 'С кем говорил' field has content", () => {
        render(<CallLogForm onSubmit={onSubmit} onCancel={onCancel} />);
        const saveButton = screen.getByText("Сохранить запись");
        expect(saveButton).toBeDisabled();

        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "Иван" } });
        expect(saveButton).not.toBeDisabled();
    });

    it("submits trimmed field values with an ISO occurredAt date", () => {
        render(<CallLogForm onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "  Иван  " } });
        fireEvent.change(screen.getByPlaceholderText("Кратко о ходе разговора"), { target: { value: "Обсудили цену" } });
        fireEvent.change(screen.getByPlaceholderText("Договорённости, следующий шаг"), { target: { value: "Пришлём КП" } });
        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({
                contactName: "Иван",
                subject: "Обсудили цену",
                outcome: "Пришлём КП",
                contactId: null,
            })
        );
        const payload = onSubmit.mock.calls[0][0];
        expect(new Date(payload.occurredAt).toString()).not.toBe("Invalid Date");
    });

    it("calls onCancel when the cancel button is clicked", () => {
        render(<CallLogForm onSubmit={onSubmit} onCancel={onCancel} />);
        fireEvent.click(screen.getByText("Отмена"));
        expect(onCancel).toHaveBeenCalledOnce();
    });

    it("pre-fills fields when editing an existing entry", () => {
        const initial: CallLogEntry = {
            id: "l1", companyId: "c1", contactName: "Пётр", subject: "Тема", outcome: "Итог",
            occurredAt: "2026-07-01T00:00:00Z", createdAt: "", updatedAt: "", contactId: null,
        };
        render(<CallLogForm initial={initial} onSubmit={onSubmit} onCancel={onCancel} />);

        expect(screen.getByDisplayValue("Пётр")).toBeTruthy();
        expect(screen.getByDisplayValue("Тема")).toBeTruthy();
        expect(screen.getByDisplayValue("Итог")).toBeTruthy();
    });

    const CONTACT: CompanyContact = {
        id: "contact-1", companyId: "c1", name: "Иван Петров", position: "Руководитель закупок",
        notes: "", createdAt: "", updatedAt: "",
    };

    it("picking a contact chip fills the name field and sets contactId on submit", () => {
        render(<CallLogForm contacts={[CONTACT]} onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.click(screen.getByText("Иван Петров"));
        expect(screen.getByDisplayValue("Иван Петров")).toBeTruthy();

        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({ contactName: "Иван Петров", contactId: "contact-1" })
        );
    });

    it("typing free text after picking a contact clears contactId on submit", () => {
        render(<CallLogForm contacts={[CONTACT]} onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.click(screen.getByText("Иван Петров"));
        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "Другой человек" } });
        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({ contactName: "Другой человек", contactId: null })
        );
    });
});
