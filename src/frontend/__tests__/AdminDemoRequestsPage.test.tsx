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
import { useAuthStore, type UserRole } from "@/shared/stores/auth-store";
import type {
    DemoRequestDto,
    ProvisionDemoRequestResult,
} from "@/features/admin/hooks/use-demo-requests";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPatch = apiClient.patch as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

/// Mimics the `ApiError` shape (`status` + `payload`) that `@/shared/api/api-client` throws for a
/// non-2xx response, without pulling in the real class from a module this file mocks wholesale.
function fakeApiError(status: number, payload: Record<string, unknown>): Error & {
    status: number;
    payload: Record<string, unknown>;
} {
    const error = new Error(
        typeof payload.message === "string" ? payload.message : `HTTP ${status}`,
    ) as Error & { status: number; payload: Record<string, unknown> };
    error.status = status;
    error.payload = payload;
    return error;
}

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

function signIn(role: UserRole) {
    useAuthStore.getState().setAuthenticatedUser({
        id: "staff-1",
        email: "staff@sellevate.com",
        displayName: "Staff",
        isOnboardingCompleted: true,
        role,
        orgId: null,
        orgRole: null,
    });
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
        organizationId: null,
        organizationName: null,
        organizationSlug: null,
        provisioningState: "NotProvisioned",
        bootstrapInviteId: null,
        bootstrapAdminEmail: null,
        provisionedAt: null,
        ...overrides,
    };
}

function buildProvisionResult(
    overrides: Partial<ProvisionDemoRequestResult> = {},
): ProvisionDemoRequestResult {
    return {
        demoRequestId: "demo-1",
        status: "Approved",
        provisioningState: "AdminInvited",
        organization: { id: "org-1", name: "Acme Corp", slug: "acme-corp" },
        inviteId: "invite-1",
        inviteEmail: "ivan@acme.test",
        inviteExpiresAt: "2026-08-27T10:00:00Z",
        alreadyProvisioned: false,
        ...overrides,
    };
}

describe("AdminDemoRequestsPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        useAuthStore.getState().clearAuthSession();
        useAuthStore.setState({ authenticatedUser: null });
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

    it("names both emails the screen sends, so nobody learns about them from a support ticket", async () => {
        mockGet.mockResolvedValueOnce([buildDemoRequest()]);

        renderDemoRequestsPage();

        expect(
            await screen.findByText(/provisioning sends them their workspace invite/i),
        ).toBeInTheDocument();
    });

    describe("provisioning — role gate", () => {
        it("hides the Provision button from a plain platform Admin", async () => {
            signIn("Admin");
            mockGet.mockResolvedValueOnce([buildDemoRequest({ companyName: "Acme Corp" })]);

            renderDemoRequestsPage();

            await screen.findByText("Acme Corp");
            expect(screen.queryByRole("button", { name: "Provision" })).not.toBeInTheDocument();
            // The Admin still sees the rest of the row, including its own status control.
            expect(screen.getByLabelText("Status for Иван Петров")).toBeInTheDocument();
        });

        it("shows the Provision button to a SuperAdmin", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValueOnce([buildDemoRequest({ companyName: "Acme Corp" })]);

            renderDemoRequestsPage();

            expect(await screen.findByRole("button", { name: "Provision" })).toBeInTheDocument();
        });
    });

    describe("provisioning — confirmation and submission", () => {
        it("sends an edited slug and omits the untouched email default", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([
                buildDemoRequest({ companyName: "Acme Corp", workEmail: "ivan@acme.test" }),
            ]);
            mockPost.mockResolvedValueOnce(buildProvisionResult());

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));

            const slugInput = await screen.findByLabelText("Organization slug for Иван Петров");
            expect(slugInput).toHaveValue("acme-corp");
            const emailInput = screen.getByLabelText("Invited admin email for Иван Петров");
            expect(emailInput).toHaveValue("ivan@acme.test");

            await userEvent.clear(slugInput);
            await userEvent.type(slugInput, "acme-2");

            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            await waitFor(() =>
                expect(mockPost).toHaveBeenCalledWith("/admin/demo-requests/demo-1/provision", {
                    slug: "acme-2",
                    adminEmail: undefined,
                }),
            );
        });

        it("sends an edited email and omits the untouched slug default", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([
                buildDemoRequest({ companyName: "Acme Corp", workEmail: "ivan@acme.test" }),
            ]);
            mockPost.mockResolvedValueOnce(buildProvisionResult());

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));

            const emailInput = await screen.findByLabelText("Invited admin email for Иван Петров");
            await userEvent.clear(emailInput);
            await userEvent.type(emailInput, "someone-else@acme.test");

            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            await waitFor(() =>
                expect(mockPost).toHaveBeenCalledWith("/admin/demo-requests/demo-1/provision", {
                    slug: undefined,
                    adminEmail: "someone-else@acme.test",
                }),
            );
        });

        it("cancelling the confirmation sends no request", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValueOnce([buildDemoRequest({ companyName: "Acme Corp" })]);

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));
            await screen.findByLabelText("Organization slug for Иван Петров");

            await userEvent.click(screen.getByRole("button", { name: "Cancel" }));

            expect(
                screen.queryByLabelText("Organization slug for Иван Петров"),
            ).not.toBeInTheDocument();
            expect(mockPost).not.toHaveBeenCalled();
        });
    });

    describe("provisioning — errors", () => {
        it("renders the slug-taken message inline and keeps the entered slug", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([buildDemoRequest({ companyName: "Acme Corp" })]);
            mockPost.mockRejectedValueOnce(
                fakeApiError(409, { code: "slug-taken", slug: "acme-corp" }),
            );

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));
            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            expect(
                await screen.findByText(/slug "acme-corp" is already taken/i),
            ).toBeInTheDocument();
            // The panel stays open with the value the admin submitted, ready to amend and resend.
            expect(
                screen.getByLabelText("Organization slug for Иван Петров"),
            ).toHaveValue("acme-corp");
        });

        it("renders a distinct message when the organization already has an administrator", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([buildDemoRequest({ companyName: "Acme Corp" })]);
            mockPost.mockRejectedValueOnce(
                fakeApiError(409, { code: "organization-has-admin", organizationId: "org-1" }),
            );

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));
            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            expect(
                await screen.findByText(/already has an administrator — no invite was sent/i),
            ).toBeInTheDocument();
        });

        it("says the organization was created but the invite failed on a 503, and refreshes the row", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([buildDemoRequest({ companyName: "Acme Corp" })]);
            mockPost.mockRejectedValueOnce(
                fakeApiError(503, {
                    code: "invite-failed",
                    organizationId: "org-1",
                    provisioningState: "OrganizationCreated",
                }),
            );

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));
            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            expect(
                await screen.findByText(/organization was created, but the invite failed to send/i),
            ).toBeInTheDocument();
            // The list is refetched so the row's real state (OrganizationCreated) is not left stale.
            await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2));
        });

        it("shows the plain server message on a 400", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValue([buildDemoRequest({ companyName: "Acme Corp" })]);
            mockPost.mockRejectedValueOnce(fakeApiError(400, { message: "adminEmail is not a valid address" }));

            renderDemoRequestsPage();

            await userEvent.click(await screen.findByRole("button", { name: "Provision" }));
            await userEvent.click(screen.getByRole("button", { name: "Confirm provision" }));

            expect(
                await screen.findByText("adminEmail is not a valid address"),
            ).toBeInTheDocument();
        });
    });

    describe("provisioning — state display", () => {
        it("renders 'Finish provisioning' rather than 'Provision' once the organization exists", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValueOnce([
                buildDemoRequest({
                    companyName: "Acme Corp",
                    organizationId: "org-1",
                    provisioningState: "OrganizationCreated",
                }),
            ]);

            renderDemoRequestsPage();

            expect(
                await screen.findByRole("button", { name: "Finish provisioning" }),
            ).toBeInTheDocument();
            expect(screen.queryByRole("button", { name: "Provision" })).not.toBeInTheDocument();
            expect(screen.getByText(/organization created, invite not sent/i)).toBeInTheDocument();
        });

        it("renders the organization, invited email and expiry with no button once admin-invited", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValueOnce([
                buildDemoRequest({
                    companyName: "Acme Corp",
                    organizationId: "org-1",
                    provisioningState: "AdminInvited",
                    bootstrapInviteId: "invite-1",
                    bootstrapAdminEmail: "ivan@acme.test",
                    provisionedAt: "2026-08-20T10:00:00Z",
                }),
            ]);

            renderDemoRequestsPage();

            expect(await screen.findByText("Provisioned")).toBeInTheDocument();
            expect(screen.getByText("ivan@acme.test", { exact: false })).toBeInTheDocument();
            expect(screen.queryByRole("button", { name: "Provision" })).not.toBeInTheDocument();
            expect(screen.queryByRole("button", { name: "Finish provisioning" })).not.toBeInTheDocument();
        });

        // The reload case, which is the reason `DemoRequestDto` carries the organization's name and
        // slug at all: nothing was provisioned in this session, so there is no cached provision
        // response to fall back on. Before the DTO grew these two fields the row showed the lead's
        // own `companyName` and the literal text "slug unknown" here — for every already-provisioned
        // lead, which in normal operation is most of the list.
        it("shows the real organization name and slug for a lead provisioned in an earlier session", async () => {
            signIn("SuperAdmin");
            mockGet.mockResolvedValueOnce([
                buildDemoRequest({
                    companyName: "ООО Ромашка",
                    organizationId: "org-1",
                    organizationName: "Acme Corp",
                    organizationSlug: "acme-corp",
                    provisioningState: "AdminInvited",
                    bootstrapInviteId: "invite-1",
                    bootstrapAdminEmail: "ivan@acme.test",
                    provisionedAt: "2026-08-20T10:00:00Z",
                }),
            ]);

            renderDemoRequestsPage();

            expect(await screen.findByText("Acme Corp", { exact: false })).toBeInTheDocument();
            expect(screen.getByText("acme-corp", { exact: false })).toBeInTheDocument();
            expect(screen.queryByText("slug unknown")).not.toBeInTheDocument();
        });
    });
});
