import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ProfileFullForm } from "@/features/org-profile/components/profile-full-form";
import type { OrganizationProfile } from "@/features/org-profile/types/organization-profile";

const profileWithClaims: OrganizationProfile = {
    product: "Облачный учёт складских остатков",
    icp: "Розничные сети",
    objections: [],
    scriptStages: [],
    tone: null,
    glossary: {},
    bannedClaims: ["гарантируем рост выручки", "окупится за месяц"],
    createdAt: "2026-05-20T10:00:00Z",
    updatedAt: "2026-08-18T10:00:00Z",
};

const renderForm = (onSave = vi.fn()) => {
    render(
        <ProfileFullForm
            profile={profileWithClaims}
            isSaving={false}
            saveError={null}
            onSave={onSave}
            onClose={vi.fn()}
        />
    );
    return onSave;
};

/**
 * `banned_claims` binds two prompts at once — the AI persona never voices a banned promise, and the
 * grader never rewards one (docs/CONTENT_PARAMETERIZATION.md §4). This form is the only path in the
 * product that can shorten that list, so losing an entry must take two deliberate confirmations and
 * must never be reachable by one stray click.
 */
describe("the full profile form guards banned claims", () => {
    it("says out loud what the list does", () => {
        renderForm();
        expect(screen.getByText(/Собеседник-ИИ их не произнесёт/)).toBeTruthy();
    });

    it("does not remove a claim on the first click — it asks first", async () => {
        const user = userEvent.setup();
        renderForm();

        await user.click(screen.getByRole("button", { name: "Снять запрет 1" }));

        expect(screen.getByRole("dialog")).toBeTruthy();
        expect(screen.getByText(/гарантируем рост выручки/)).toBeTruthy();
        expect(
            (screen.getAllByDisplayValue("гарантируем рост выручки")).length
        ).toBeGreaterThan(0);
    });

    it("keeps the claim when the confirmation is dismissed", async () => {
        const user = userEvent.setup();
        renderForm();

        await user.click(screen.getByRole("button", { name: "Снять запрет 2" }));
        await user.click(screen.getByRole("button", { name: "Отмена" }));

        expect(screen.getByDisplayValue("окупится за месяц")).toBeTruthy();
    });

    it("still does not save after the removal is confirmed — a second confirmation names the loss", async () => {
        const user = userEvent.setup();
        const onSave = renderForm();

        await user.click(screen.getByRole("button", { name: "Снять запрет 2" }));
        await user.click(screen.getByRole("button", { name: "Снять запрет" }));
        expect(screen.queryByDisplayValue("окупится за месяц")).toBeNull();
        expect(onSave).not.toHaveBeenCalled();

        await user.click(screen.getByRole("button", { name: "Сохранить" }));
        expect(screen.getByRole("dialog")).toBeTruthy();
        expect(screen.getByText(/перестанут быть запрещёнными/)).toBeTruthy();
        expect(onSave).not.toHaveBeenCalled();
    });

    it("saves the shortened list only after both confirmations", async () => {
        const user = userEvent.setup();
        const onSave = renderForm();

        await user.click(screen.getByRole("button", { name: "Снять запрет 2" }));
        await user.click(screen.getByRole("button", { name: "Снять запрет" }));
        await user.click(screen.getByRole("button", { name: "Сохранить" }));
        await user.click(screen.getByRole("button", { name: "Снять и сохранить" }));

        expect(onSave).toHaveBeenCalledTimes(1);
        expect(onSave.mock.calls[0][0].bannedClaims).toEqual(["гарантируем рост выручки"]);
    });

    it("saves without asking when nothing is being un-forbidden", async () => {
        const user = userEvent.setup();
        const onSave = renderForm();

        await user.click(screen.getByRole("button", { name: "Ещё запрет" }));
        await user.click(screen.getByRole("button", { name: "Сохранить" }));

        expect(onSave).toHaveBeenCalledTimes(1);
        expect(onSave.mock.calls[0][0].bannedClaims).toEqual([
            "гарантируем рост выручки",
            "окупится за месяц",
        ]);
    });
});
