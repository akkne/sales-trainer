"use client";

import { Fragment, useState } from "react";
import { canManagePlatformUsers, useAuthStore } from "@/shared/stores/auth-store";
import {
    useAdminDemoRequests,
    useProvisionDemoRequest,
    useUpdateDemoRequestStatus,
    type DemoRequestDto,
} from "@/features/admin/hooks/use-demo-requests";
import {
    DEMO_REQUEST_STATUSES,
    SALES_TEAM_SIZE_LABELS,
    STATUS_REQUIRING_CONFIRMATION,
    type DemoRequestStatus,
} from "@/features/admin/lib/demo-request-format";

const statusBadgeClass: Record<DemoRequestStatus, string> = {
    New: "bg-indigo-soft text-indigo-ink",
    Contacted: "bg-warn-soft text-warn",
    Approved: "bg-good-soft text-good",
    Declined: "bg-bad-soft text-bad",
};

const DETAIL_COLUMN_COUNT = 10;

/// A client-side preview only — the authoritative slug normalization lives on the server
/// (docs/DEMO_REQUEST.md). This exists purely so the confirmation panel can show something
/// plausible before the admin has typed anything; whatever the admin actually submits, edited
/// or not, is compared against this same preview to decide whether `slug` is worth sending at
/// all (see `confirmProvision` below).
function previewSlug(companyName: string): string {
    return companyName
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");
}

interface ProvisionDraft {
    demoRequestId: string;
    /// Org creation already happened — the admin is only re-sending the invite, so the slug
    /// field is not offered (editing it now would do nothing: the organization has a slug).
    isRetry: boolean;
    slug: string;
    adminEmail: string;
    defaultSlug: string;
    defaultAdminEmail: string;
    errorMessage: string | null;
}

interface ProvisionedDetails {
    organizationName: string;
    organizationSlug: string;
    inviteEmail: string;
    /// `null` when this call landed on the `alreadyProvisioned` fast path — the backend never
    /// re-asks identity-service for an already-issued invite's expiry (docs/AUDIT_CONTRACTS.md
    /// finding C-6).
    inviteExpiresAt: string | null;
}

export default function AdminDemoRequestsPage() {
    // R-6 (Q-14): the list gates below use `isLoadingError`, not bare `error` — a background
    // refetch failing must not discard the already-rendered table.
    const { data: demoRequests = [], isLoading, error, isLoadingError } = useAdminDemoRequests();
    const updateStatus = useUpdateDemoRequestStatus();
    const provisionDemoRequest = useProvisionDemoRequest();

    const authenticatedUser = useAuthStore((state) => state.authenticatedUser);
    // Provisioning creates a real tenant and adds it a first administrator, which is exactly the
    // kind of add-a-user operation `RequireSuperAdmin` gates everywhere else in this panel
    // (organizations' first-admin bootstrap, users' role changes) — reusing the same predicate
    // rather than inventing a new one.
    const canProvision = canManagePlatformUsers(authenticatedUser?.role);

    const [expandedId, setExpandedId] = useState<string | null>(null);
    // Approving a lead sends the customer an email saying their request was approved, which is not
    // obvious from a plain dropdown (docs/DEMO_REQUEST.md). That one transition is held here until a
    // person presses an inline "Confirm approval" button; every other transition fires at once.
    const [pendingApprovalId, setPendingApprovalId] = useState<string | null>(null);

    // Provisioning's inline confirmation (never `window.confirm` — browser modals break this
    // repo's automation). Only one row's draft is open at a time.
    const [provisionDraft, setProvisionDraft] = useState<ProvisionDraft | null>(null);
    // The organization's name and slug come from `DemoRequestDto` itself — organization-service owns
    // both the lead and the registry, so it resolves them in one join and they survive a reload.
    // This cache exists only for the invite's expiry, which identity-service owns and the list DTO
    // therefore cannot report; the provision response carries it once, right after the call that
    // produced it. A reload loses the expiry and the row says so rather than guessing.
    const [provisionedDetailsById, setProvisionedDetailsById] = useState<
        Record<string, ProvisionedDetails>
    >({});

    const changeStatus = (request: DemoRequestDto, nextStatus: DemoRequestStatus) => {
        if (nextStatus === request.status) return;

        if (nextStatus === STATUS_REQUIRING_CONFIRMATION) {
            setPendingApprovalId(request.id);
            return;
        }

        setPendingApprovalId(null);
        updateStatus.mutate({ id: request.id, status: nextStatus });
    };

    const confirmApproval = (request: DemoRequestDto) => {
        updateStatus.mutate(
            { id: request.id, status: "Approved" },
            { onSettled: () => setPendingApprovalId(null) },
        );
    };

    const openProvisionDraft = (request: DemoRequestDto) => {
        const isRetry = request.provisioningState === "OrganizationCreated";
        const cached = provisionedDetailsById[request.id];
        const defaultSlug = cached?.organizationSlug ?? previewSlug(request.companyName);
        const defaultAdminEmail =
            cached?.inviteEmail ?? request.bootstrapAdminEmail ?? request.workEmail;

        setProvisionDraft({
            demoRequestId: request.id,
            isRetry,
            slug: defaultSlug,
            adminEmail: defaultAdminEmail,
            defaultSlug,
            defaultAdminEmail,
            errorMessage: null,
        });
    };

    /// Used while the confirmation is open: edits to the slug/email inputs, which clear the last
    /// error so an admin amending a rejected slug is not staring at a stale message.
    const editProvisionDraft = (patch: Partial<Pick<ProvisionDraft, "slug" | "adminEmail">>) => {
        setProvisionDraft((current) => (current ? { ...current, ...patch, errorMessage: null } : current));
    };

    /// Used once the mutation settles: reports the outcome without touching whatever the admin
    /// has typed, so a rejected slug stays exactly as entered (docs/DEMO_REQUEST.md, "409
    /// slug-taken").
    const reportProvisionOutcome = (patch: Partial<ProvisionDraft>) => {
        setProvisionDraft((current) => (current ? { ...current, ...patch } : current));
    };

    const confirmProvision = (request: DemoRequestDto) => {
        if (!provisionDraft || provisionDraft.demoRequestId !== request.id) return;

        const trimmedSlug = provisionDraft.slug.trim();
        const trimmedAdminEmail = provisionDraft.adminEmail.trim();
        // Only send what the admin actually changed. The server already knows how to derive a
        // slug from the lead's company name and the invite address from its work email — sending
        // those back unchanged would duplicate normalization logic that only the server should
        // own, and would silently pin a slug the server might have generated slightly differently
        // on its own. An edit is unambiguous; a field left exactly as previewed is not.
        const slugOverride =
            !provisionDraft.isRetry && trimmedSlug !== provisionDraft.defaultSlug ? trimmedSlug : undefined;
        const adminEmailOverride =
            trimmedAdminEmail !== provisionDraft.defaultAdminEmail ? trimmedAdminEmail : undefined;

        provisionDemoRequest.mutate(
            { id: request.id, slug: slugOverride, adminEmail: adminEmailOverride },
            {
                onSuccess: (result) => {
                    setProvisionedDetailsById((current) => ({
                        ...current,
                        [request.id]: {
                            organizationName: result.organization.name,
                            organizationSlug: result.organization.slug,
                            inviteEmail: result.inviteEmail,
                            inviteExpiresAt: result.inviteExpiresAt,
                        },
                    }));
                    setProvisionDraft(null);
                },
                onError: (apiError) => {
                    const code = apiError.payload?.code;

                    if (apiError.status === 409 && code === "slug-taken") {
                        // Keep the panel open with whatever the admin typed so they can amend the
                        // slug and resubmit without re-opening anything.
                        reportProvisionOutcome({
                            errorMessage: `The slug "${String(apiError.payload?.slug ?? trimmedSlug)}" is already taken — change it and try again.`,
                        });
                        return;
                    }

                    if (apiError.status === 409 && code === "organization-has-admin") {
                        reportProvisionOutcome({
                            errorMessage:
                                "This organization already has an administrator — no invite was sent.",
                        });
                        return;
                    }

                    if (apiError.status === 503) {
                        reportProvisionOutcome({
                            isRetry: true,
                            errorMessage:
                                'The organization was created, but the invite failed to send. Press "Finish provisioning" again to finish.',
                        });
                        return;
                    }

                    reportProvisionOutcome({
                        errorMessage:
                            typeof apiError.payload?.message === "string"
                                ? apiError.payload.message
                                : apiError.message,
                    });
                },
            },
        );
    };

    return (
        <div>
            <h1 className="text-xl font-bold text-ink mb-1">Demo requests</h1>
            <p className="text-xs text-ink-3 mb-1">
                Leads from the public demo-request form, newest first. Nothing here belongs to an
                organization — these are prospects who have not signed up yet.
            </p>
            <p className="text-xs text-ink-4 mb-6">
                Approving a lead emails the customer, and provisioning sends them their workspace
                invite. Both are real emails to a real inbox, not a preview.
            </p>

            {isLoading && <p className="text-sm text-ink-3">Loading...</p>}
            {error && <p className="text-sm text-bad">Error: {(error as Error).message}</p>}

            {!isLoading && !isLoadingError && demoRequests.length === 0 && (
                <p className="text-sm text-ink-3">No demo requests yet.</p>
            )}

            {!isLoading && !isLoadingError && demoRequests.length > 0 && (
                <div className="overflow-x-auto -mx-4 px-4">
                    <table className="w-full text-sm border-collapse min-w-[1350px]">
                        <thead>
                            <tr className="border-b border-line">
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Created</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Name</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Company</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Work email</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Phone</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Team size</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Marketing</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Status</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Provisioning</th>
                                <th className="py-2 px-3" />
                            </tr>
                        </thead>
                        <tbody>
                            {demoRequests.map((request) => {
                                const isExpanded = expandedId === request.id;
                                const hasDetail = Boolean(request.jobTitle) || Boolean(request.comment);
                                const draftOpenHere = provisionDraft?.demoRequestId === request.id;
                                const cachedDetails = provisionedDetailsById[request.id];

                                return (
                                    <Fragment key={request.id}>
                                        <tr className="border-b border-line align-top hover:bg-bg-2">
                                            <td className="py-2.5 px-3 text-ink-3 text-xs whitespace-nowrap">
                                                {new Date(request.createdAt).toLocaleDateString()}
                                            </td>
                                            <td className="py-2.5 px-3 text-ink">{request.fullName}</td>
                                            <td className="py-2.5 px-3 text-ink">{request.companyName}</td>
                                            <td className="py-2.5 px-3 text-ink-3 text-xs">{request.workEmail}</td>
                                            <td className="py-2.5 px-3 text-ink-3 text-xs whitespace-nowrap">
                                                {request.phone}
                                            </td>
                                            <td className="py-2.5 px-3 text-ink-3 text-xs whitespace-nowrap">
                                                {SALES_TEAM_SIZE_LABELS[request.salesTeamSize]}
                                            </td>
                                            <td className="py-2.5 px-3">
                                                <span
                                                    className={`inline-block px-2 py-0.5 text-xs rounded-full ${
                                                        request.marketingConsentGivenAt
                                                            ? "bg-good-soft text-good"
                                                            : "bg-bg-2 text-ink-3"
                                                    }`}
                                                >
                                                    {request.marketingConsentGivenAt ? "Yes" : "No"}
                                                </span>
                                            </td>
                                            <td className="py-2.5 px-3">
                                                <div className="flex flex-col gap-1.5 w-fit">
                                                    <span
                                                        className={`inline-block w-fit px-2 py-0.5 text-xs rounded-full ${statusBadgeClass[request.status]}`}
                                                    >
                                                        {request.status}
                                                    </span>
                                                    <select
                                                        aria-label={`Status for ${request.fullName}`}
                                                        value={request.status}
                                                        onChange={(event) =>
                                                            changeStatus(
                                                                request,
                                                                event.target.value as DemoRequestStatus,
                                                            )
                                                        }
                                                        disabled={updateStatus.isPending}
                                                        className="px-2 py-1 text-xs rounded-lg border border-line bg-surface text-ink disabled:opacity-50"
                                                    >
                                                        {DEMO_REQUEST_STATUSES.map((status) => (
                                                            <option key={status} value={status}>
                                                                {status}
                                                            </option>
                                                        ))}
                                                    </select>
                                                    {pendingApprovalId === request.id && (
                                                        <div className="mt-1 p-2 rounded-lg bg-warn-soft border border-line text-xs max-w-[220px]">
                                                            <p className="text-ink-3 mb-1.5">
                                                                Approving sends the customer an email saying their
                                                                request was approved. Confirm?
                                                            </p>
                                                            <div className="flex gap-2">
                                                                <button
                                                                    type="button"
                                                                    onClick={() => confirmApproval(request)}
                                                                    disabled={updateStatus.isPending}
                                                                    className="px-2 py-1 text-xs rounded-md bg-good text-white disabled:opacity-50"
                                                                >
                                                                    Confirm approval
                                                                </button>
                                                                <button
                                                                    type="button"
                                                                    onClick={() => setPendingApprovalId(null)}
                                                                    className="px-2 py-1 text-xs rounded-md text-ink-3 hover:bg-bg-2"
                                                                >
                                                                    Cancel
                                                                </button>
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>
                                            </td>
                                            <td className="py-2.5 px-3">
                                                <div className="flex flex-col gap-1.5 w-fit min-w-[200px]">
                                                    {request.provisioningState === "AdminInvited" ? (
                                                        <div className="text-xs">
                                                            <span className="inline-block w-fit px-2 py-0.5 rounded-full bg-good-soft text-good mb-1">
                                                                Provisioned
                                                            </span>
                                                            <p className="text-ink">
                                                                {request.organizationName
                                                                    ?? cachedDetails?.organizationName
                                                                    ?? request.companyName}
                                                                {(request.organizationSlug
                                                                    ?? cachedDetails?.organizationSlug) && (
                                                                    <span className="text-ink-3">
                                                                        {" "}
                                                                        ({request.organizationSlug
                                                                            ?? cachedDetails?.organizationSlug})
                                                                    </span>
                                                                )}
                                                            </p>
                                                            <p className="text-ink-3">
                                                                Invited:{" "}
                                                                {request.bootstrapAdminEmail ?? cachedDetails?.inviteEmail ?? "—"}
                                                            </p>
                                                            <p className="text-ink-3">
                                                                Expires:{" "}
                                                                {cachedDetails?.inviteExpiresAt
                                                                    ? new Date(cachedDetails.inviteExpiresAt).toLocaleString()
                                                                    : "unknown (not provisioned this session)"}
                                                            </p>
                                                        </div>
                                                    ) : request.provisioningState === "OrganizationCreated" ? (
                                                        <>
                                                            <span className="inline-block w-fit px-2 py-0.5 text-xs rounded-full bg-warn-soft text-warn">
                                                                Organization created, invite not sent
                                                            </span>
                                                            {canProvision && (
                                                                <button
                                                                    type="button"
                                                                    onClick={() => openProvisionDraft(request)}
                                                                    disabled={provisionDemoRequest.isPending}
                                                                    className="text-xs text-indigo-ink hover:underline disabled:opacity-50 text-left"
                                                                >
                                                                    Finish provisioning
                                                                </button>
                                                            )}
                                                        </>
                                                    ) : canProvision ? (
                                                        <button
                                                            type="button"
                                                            onClick={() => openProvisionDraft(request)}
                                                            disabled={provisionDemoRequest.isPending}
                                                            className="text-xs text-indigo-ink hover:underline disabled:opacity-50 text-left"
                                                        >
                                                            Provision
                                                        </button>
                                                    ) : (
                                                        <span className="text-xs text-ink-4">—</span>
                                                    )}

                                                    {draftOpenHere && provisionDraft && (
                                                        <div className="mt-1 p-2 rounded-lg bg-bg-2 border border-line text-xs w-[260px]">
                                                            {provisionDraft.isRetry ? (
                                                                <p className="text-ink-3 mb-1.5">
                                                                    The organization already exists. Finishing sends
                                                                    the invite below.
                                                                </p>
                                                            ) : (
                                                                <p className="text-ink-3 mb-1.5">
                                                                    Creates an organization for &quot;{request.companyName}&quot;
                                                                    and invites the address below as its administrator.
                                                                </p>
                                                            )}

                                                            {!provisionDraft.isRetry && (
                                                                <label className="flex flex-col gap-0.5 mb-1.5">
                                                                    <span className="text-ink-4">Slug</span>
                                                                    <input
                                                                        aria-label={`Organization slug for ${request.fullName}`}
                                                                        value={provisionDraft.slug}
                                                                        onChange={(event) =>
                                                                            editProvisionDraft({ slug: event.target.value })
                                                                        }
                                                                        className="px-2 py-1 rounded-md border border-line bg-surface text-ink"
                                                                    />
                                                                </label>
                                                            )}

                                                            <label className="flex flex-col gap-0.5 mb-1.5">
                                                                <span className="text-ink-4">Invite email</span>
                                                                <input
                                                                    type="email"
                                                                    aria-label={`Invited admin email for ${request.fullName}`}
                                                                    value={provisionDraft.adminEmail}
                                                                    onChange={(event) =>
                                                                        editProvisionDraft({ adminEmail: event.target.value })
                                                                    }
                                                                    className="px-2 py-1 rounded-md border border-line bg-surface text-ink w-full"
                                                                />
                                                            </label>

                                                            {provisionDraft.errorMessage && (
                                                                <p className="text-bad mb-1.5">{provisionDraft.errorMessage}</p>
                                                            )}

                                                            <div className="flex gap-2">
                                                                <button
                                                                    type="button"
                                                                    onClick={() => confirmProvision(request)}
                                                                    disabled={provisionDemoRequest.isPending}
                                                                    className="px-2 py-1 rounded-md bg-good text-white disabled:opacity-50"
                                                                >
                                                                    {provisionDraft.isRetry
                                                                        ? "Finish provisioning"
                                                                        : "Confirm provision"}
                                                                </button>
                                                                <button
                                                                    type="button"
                                                                    onClick={() => setProvisionDraft(null)}
                                                                    className="px-2 py-1 rounded-md text-ink-3 hover:bg-bg-2"
                                                                >
                                                                    Cancel
                                                                </button>
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>
                                            </td>
                                            <td className="py-2.5 px-3 text-right whitespace-nowrap">
                                                {hasDetail ? (
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            setExpandedId(isExpanded ? null : request.id)
                                                        }
                                                        className="text-xs text-indigo-ink hover:underline"
                                                    >
                                                        {isExpanded ? "Hide" : "Details"}
                                                    </button>
                                                ) : (
                                                    <span className="text-xs text-ink-4">—</span>
                                                )}
                                            </td>
                                        </tr>
                                        {isExpanded && (
                                            <tr className="border-b border-line bg-bg-2/50">
                                                <td colSpan={DETAIL_COLUMN_COUNT} className="py-3 px-3 text-xs text-ink-3">
                                                    <div className="flex flex-wrap gap-6">
                                                        <div>
                                                            <span className="block text-ink-4 mb-0.5">Job title</span>
                                                            <span className="text-ink">
                                                                {request.jobTitle || "—"}
                                                            </span>
                                                        </div>
                                                        <div className="flex-1 min-w-[240px]">
                                                            <span className="block text-ink-4 mb-0.5">Comment</span>
                                                            <span className="text-ink whitespace-pre-wrap">
                                                                {request.comment || "—"}
                                                            </span>
                                                        </div>
                                                    </div>
                                                </td>
                                            </tr>
                                        )}
                                    </Fragment>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
}
