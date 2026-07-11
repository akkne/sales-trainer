import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CompanyContactsCard } from "@/features/companies/components/company-contacts-card";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";

const CONTACT: CompanyContact = {
    id: "contact-1", companyId: "c1", name: "Иван Петров", position: "Руководитель закупок",
    notes: "Любит цифры и графики", createdAt: "", updatedAt: "",
};

describe("CompanyContactsCard", () => {
    const onStartAddContact = vi.fn();
    const onCancelAddContact = vi.fn();
    const onAddContact = vi.fn();
    const onStartEditContact = vi.fn();
    const onCancelEditContact = vi.fn();
    const onUpdateContact = vi.fn();
    const onDeleteContact = vi.fn();

    beforeEach(() => {
        onStartAddContact.mockReset();
        onCancelAddContact.mockReset();
        onAddContact.mockReset();
        onStartEditContact.mockReset();
        onCancelEditContact.mockReset();
        onUpdateContact.mockReset();
        onDeleteContact.mockReset();
    });

    function renderCard(overrides: Partial<React.ComponentProps<typeof CompanyContactsCard>> = {}) {
        return render(
            <CompanyContactsCard
                contacts={[]}
                addingContact={false}
                editingContact={null}
                onStartAddContact={onStartAddContact}
                onCancelAddContact={onCancelAddContact}
                onAddContact={onAddContact}
                onStartEditContact={onStartEditContact}
                onCancelEditContact={onCancelEditContact}
                onUpdateContact={onUpdateContact}
                onDeleteContact={onDeleteContact}
                {...overrides}
            />
        );
    }

    it("shows the empty state when there are no contacts", () => {
        renderCard();
        expect(screen.getByText("Пока нет контактов — добавьте, с кем вы общаетесь в этой компании")).toBeTruthy();
    });

    it("renders contact name and position", () => {
        renderCard({ contacts: [CONTACT] });
        expect(screen.getByText("Иван Петров")).toBeTruthy();
        expect(screen.getByText("Руководитель закупок")).toBeTruthy();
    });

    it("expands notes on click and collapses on second click", () => {
        renderCard({ contacts: [CONTACT] });

        expect(screen.queryByText("Любит цифры и графики")).toBeFalsy();

        fireEvent.click(screen.getByText("Иван Петров"));
        expect(screen.getByText("Любит цифры и графики")).toBeTruthy();

        fireEvent.click(screen.getByText("Иван Петров"));
        expect(screen.queryByText("Любит цифры и графики")).toBeFalsy();
    });

    it("calls onStartAddContact when the add button is clicked", () => {
        renderCard();
        fireEvent.click(screen.getByText("+ Добавить контакт"));
        expect(onStartAddContact).toHaveBeenCalledOnce();
    });

    it("shows the add form and submits a new contact", () => {
        renderCard({ addingContact: true });

        fireEvent.change(screen.getByPlaceholderText("Имя контакта"), { target: { value: "Мария" } });
        fireEvent.click(screen.getByText("Сохранить контакт"));

        expect(onAddContact).toHaveBeenCalledWith(
            expect.objectContaining({ name: "Мария", position: "", notes: "" })
        );
    });

    it("calls onStartEditContact when the edit button is clicked", () => {
        renderCard({ contacts: [CONTACT] });
        fireEvent.click(screen.getByLabelText("Редактировать контакт"));
        expect(onStartEditContact).toHaveBeenCalledWith(CONTACT);
    });

    it("calls onDeleteContact when the delete button is clicked", () => {
        renderCard({ contacts: [CONTACT] });
        fireEvent.click(screen.getByLabelText("Удалить контакт"));
        expect(onDeleteContact).toHaveBeenCalledWith(CONTACT);
    });

    it("renders the edit form in place of the row when editing", () => {
        renderCard({ contacts: [CONTACT], editingContact: CONTACT });
        expect(screen.getByDisplayValue("Иван Петров")).toBeTruthy();
        expect(screen.getByText("Сохранить контакт")).toBeTruthy();
    });
});
