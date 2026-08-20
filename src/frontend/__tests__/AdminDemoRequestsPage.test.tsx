import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        patch: vi.fn(),
        delete: vi.fn(),
    },
}));

import { apiClient } from "@/shared/api/api-client";
import AdminDemoRequestsPage from "@/app/(admin)/admin/demo-requests/page";
import type { DemoRequestDto } from "@/features/admin/hooks/use-demo-requests";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPatch = apiClient.patch as ReturnType<typeof vi.fn>;

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const TestQueryWrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    TestQueryWrapper.displayName = "TestQueryWrapper";
    return TestQueryWrapper;
}

function renderDemoRequestsPage() {
    const Wrapper = createWrapper();
    return render(
        <Wrapper>
            <AdminDemoRequestsPage />
        </Wrapper>,
    );
}

function buildDemoRequest(overrides: Partial<DemoRequestDto> = {}): DemoRequestDto {
    return {
        id: "demo-1",
        fullName: "Иван Петров",
        workEmail: "ivan@customer.test",
        phone: "+7 900 123-45-67",
        companyName: "ООО Ромашка",
        jobTitle: "Руководитель отдела продаж",
        salesTeamSize: "SixToTwenty",
        comment: "Интересует голосовая практика",
        status: "New",
        consentGivenAt: "2026-08-18T10:00:00Z",
        marketingConsentGivenAt: "2026-08-18T10:00:00Z",
        createdAt: "2026-08-18T10:00:00Z",
        updatedAt: "2026-08-18T10:00:00Z",
        ...overrides,
    };
}

describe("AdminDemoRequestsPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("lists every lead with the fields that matter at a glance", async () => {
        mockGet.mockResolvedValueOnce([buildDemoRequest()]);

        renderDemoRequestsPage();

        await waitFor(() => expect(mockGet).toHaveBeenCalledWith("/admin/demo-requests"));
        expect(await screen.findByText("Иван Петров")).toBeInTheDocument();
        expect(screen.getByText("ООО Ромашка")).toBeInTheDocument();
        expect(screen.getByText("ivan@customer.test")).toBeInTheDocument();
        expect(screen.getByText("+7 900 123-45-67")).toBeInTheDocument();
        expect(screen.getByText("6–20")).toBeInTheDocument();
    });

    it("shows the status as a colour-coded badge", async () => {
        mockGet.mockResolvedValueOnce([buildDemoRequest({ status: "Declined" })]);

        renderDemoRequestsPage();

        const badge = await screen.findByText("Declined", { selector: "span" });
        expect(badge.className).toContain("bg-bad-soft");
        expect(badge.className).toContain("text-bad");
    });

    it("changes a lead to a non-approve status with no confirmation, calling PATCH with the right body", async () => {
        // `mockResolvedValue` (not `Once`): invalidating the list query after the mutation refetches it.
        mockGet.mockResolvedValue([buildDemoRequest({ status: "New" })]);
        mockPatch.mockResolvedValueOnce(buildDemoRequest({ status: "Contacted" }));

        renderDemoRequestsPage();

        const select = await screen.findByLabelText("Status for Иван Петров");
        await userEvent.selectOptions(select, "Contacted");

        await waitFor(() =>
            expect(mockPatch).toHaveBeenCalledWith("/admin/demo-requests/demo-1/status", {
                status: "Contacted",
            }),
        );
        expect(screen.queryByText(/Confirm approval/)).not.toBeInTheDocument();
    });

    it("requires an inline confirmation before approving fires any PATCH", async () => {
        mockGet.mockResolvedValue([buildDemoRequest({ status: "New" })]);
        mockPatch.mockResolvedValueOnce(buildDemoRequest({ status: "Approved" }));

        renderDemoRequestsPage();

        const select = await screen.findByLabelText("Status for Иван Петров");
        await userEvent.selectOptions(select, "Approved");

        expect(await screen.findByText(/Approving sends the customer an email/)).toBeInTheDocument();
        expect(mockPatch).not.toHaveBeenCalled();

        const confirmButton = screen.getByRole("button", { name: "Confirm approval" });
        await userEvent.click(confirmButton);

        await waitFor(() =>
            expect(mockPatch).toHaveBeenCalledWith("/admin/demo-requests/demo-1/status", {
                status: "Approved",
            }),
        );
    });

    it("lets the confirmation be cancelled without ever sending the PATCH", async () => {
        mockGet.mockResolvedValueOnce([buildDemoRequest({ status: "New" })]);

        renderDemoRequestsPage();

        const select = await screen.findByLabelText("Status for Иван Петров");
        await userEvent.selectOptions(select, "Approved");

        await userEvent.click(await screen.findByRole("button", { name: "Cancel" }));

        expect(screen.queryByText(/Approving sends the customer an email/)).not.toBeInTheDocument();
        expect(mockPatch).not.toHaveBeenCalled();
    });

    it("surfaces marketing consent as a boolean-ish indicator", async () => {
        mockGet.mockResolvedValueOnce([
            buildDemoRequest({ id: "demo-2", marketingConsentGivenAt: null }),
        ]);

        renderDemoRequestsPage();

        const row = (await screen.findByText("Иван Петров")).closest("tr");
        expect(row).not.toBeNull();
        expect(within(row as HTMLElement).getByText("No")).toBeInTheDocument();
    });

    it("shows the empty state when there are no leads", async () => {
        mockGet.mockResolvedValueOnce([]);

        renderDemoRequestsPage();

        expect(await screen.findByText("No demo requests yet.")).toBeInTheDocument();
    });
});
