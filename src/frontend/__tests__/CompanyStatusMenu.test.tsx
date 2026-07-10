import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CompanyStatusMenu } from "@/features/companies/components/company-status-menu";

describe("CompanyStatusMenu", () => {
    const onChange = vi.fn();

    beforeEach(() => {
        onChange.mockReset();
    });

    it("shows the current status label on the trigger", () => {
        render(<CompanyStatusMenu status="Contacted" onChange={onChange} />);
        expect(screen.getByRole("button", { name: /Был контакт/ })).toBeTruthy();
    });

    it("opens the menu with all five statuses on trigger click", () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);

        fireEvent.click(screen.getByRole("button", { name: /Лид/ }));

        expect(screen.getByRole("menuitem", { name: /Лид/ })).toBeTruthy();
        expect(screen.getByRole("menuitem", { name: /Был контакт/ })).toBeTruthy();
        expect(screen.getByRole("menuitem", { name: /Встреча назначена/ })).toBeTruthy();
        expect(screen.getByRole("menuitem", { name: /Сделка закрыта/ })).toBeTruthy();
        expect(screen.getByRole("menuitem", { name: /Отказ/ })).toBeTruthy();
    });

    it("calls onChange with the selected status and closes the menu", () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);

        fireEvent.click(screen.getByRole("button", { name: /Лид/ }));
        fireEvent.click(screen.getByRole("menuitem", { name: /Сделка закрыта/ }));

        expect(onChange).toHaveBeenCalledWith("DealWon");
        expect(screen.queryByRole("menu")).toBeFalsy();
    });

    it("does not call onChange when the current status is re-selected", () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);

        fireEvent.click(screen.getByRole("button", { name: /Лид/ }));
        fireEvent.click(screen.getByRole("menuitem", { name: /^Лид$/ }));

        expect(onChange).not.toHaveBeenCalled();
    });

    it("disables the trigger when disabled is true", () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} disabled />);
        expect(screen.getByRole("button", { name: /Лид/ })).toBeDisabled();
    });
});
