import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
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

    it("moves focus into the menu on open, landing on the currently selected item", async () => {
        render(<CompanyStatusMenu status="Contacted" onChange={onChange} />);

        fireEvent.click(screen.getByRole("button", { name: /Был контакт/ }));

        await waitFor(() =>
            expect(screen.getByRole("menuitem", { name: /^Был контакт$/ })).toHaveFocus()
        );
    });

    it("closes on Escape and returns focus to the trigger", async () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);
        const trigger = screen.getByRole("button", { name: /Лид/ });

        fireEvent.click(trigger);
        await waitFor(() => expect(screen.getByRole("menu")).toBeTruthy());

        fireEvent.keyDown(screen.getByRole("menu"), { key: "Escape" });

        expect(screen.queryByRole("menu")).toBeFalsy();
        expect(trigger).toHaveFocus();
    });

    it("moves focus between items with ArrowDown/ArrowUp, wrapping at the ends", async () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);
        fireEvent.click(screen.getByRole("button", { name: /Лид/ }));

        const menu = screen.getByRole("menu");
        await waitFor(() => expect(screen.getByRole("menuitem", { name: /^Лид$/ })).toHaveFocus());

        fireEvent.keyDown(menu, { key: "ArrowDown" });
        expect(screen.getByRole("menuitem", { name: /Был контакт/ })).toHaveFocus();

        fireEvent.keyDown(menu, { key: "ArrowUp" });
        expect(screen.getByRole("menuitem", { name: /^Лид$/ })).toHaveFocus();

        // Wraps to the last item when moving up from the first item.
        fireEvent.keyDown(menu, { key: "ArrowUp" });
        expect(screen.getByRole("menuitem", { name: /Отказ/ })).toHaveFocus();
    });

    it("jumps to the first/last item with Home/End", async () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);
        fireEvent.click(screen.getByRole("button", { name: /Лид/ }));

        const menu = screen.getByRole("menu");
        await waitFor(() => expect(screen.getByRole("menuitem", { name: /^Лид$/ })).toHaveFocus());

        fireEvent.keyDown(menu, { key: "End" });
        expect(screen.getByRole("menuitem", { name: /Отказ/ })).toHaveFocus();

        fireEvent.keyDown(menu, { key: "Home" });
        expect(screen.getByRole("menuitem", { name: /^Лид$/ })).toHaveFocus();
    });

    it("closes and returns focus to the trigger after selecting an item", async () => {
        render(<CompanyStatusMenu status="Lead" onChange={onChange} />);
        const trigger = screen.getByRole("button", { name: /Лид/ });

        fireEvent.click(trigger);
        fireEvent.click(screen.getByRole("menuitem", { name: /Был контакт/ }));

        expect(onChange).toHaveBeenCalledWith("Contacted");
        expect(screen.queryByRole("menu")).toBeFalsy();
        expect(trigger).toHaveFocus();
    });
});
