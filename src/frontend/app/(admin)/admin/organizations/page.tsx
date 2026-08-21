"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { canManagePlatformUsers, useAuthStore } from "@/shared/stores/auth-store";
import { beginImpersonationSession } from "@/features/admin/lib/impersonation-session";
import {
    useBootstrapOrganizationAdmin,
    useCreateOrganization,
    useImpersonationAudit,
    usePlatformOrganizations,
    useSetOrganizationStatus,
    useStartImpersonation,
    type OrganizationAdminRole,
    type PlatformOrganization,
} from "@/features/admin/hooks/use-organizations";
import { ErrorState } from "@/shared/components/error-state";

const defaultAdminRole: OrganizationAdminRole = "TenancySuperAdmin";

const adminRoleLabels: Record<OrganizationAdminRole, string> = {
    TenancySuperAdmin: "Superadmin (can add and remove users)",
    TenancyAdmin: "Admin (cannot manage users)",
};

const statusBadgeClass: Record<string, string> = {
    Active: "bg-olive-soft text-olive",
    Suspended: "bg-bg-2 text-ink-3",
};

export default function AdminOrganizationsPage() {
    const router = useRouter();
    const { accessToken, setAccessToken, authenticatedUser } = useAuthStore();

    // Creating, suspending and resuming an organization is ordinary platform administration, so
    // an `Admin` does all of it. Inviting the organization's first admin adds a user, and
    // impersonation is superadmin-exclusive for its own reasons — both stay behind
    // RequireSuperAdmin on the backend (docs/DECISIONS.md, 2026-08-16).
    const canManageUsers = canManagePlatformUsers(authenticatedUser?.role);

    // R-6 (Q-14): both gates use `isLoadingError` (first load failed, no data) rather than bare
    // `isError` — a background refetch failing must not discard an already-rendered list.
    const { data: organizations = [], isLoading, isLoadingError: isError, refetch } = usePlatformOrganizations();
    const {
        data: impersonations = [],
        isLoadingError: isImpersonationsError,
        refetch: refetchImpersonations,
    } = useImpersonationAudit();
    const createOrganization = useCreateOrganization();
    const setOrganizationStatus = useSetOrganizationStatus();
    const bootstrapOrganizationAdmin = useBootstrapOrganizationAdmin();
    const startImpersonation = useStartImpersonation();

    const [newOrganizationName, setNewOrganizationName] = useState("");
    const [newOrganizationSlug, setNewOrganizationSlug] = useState("");
    const [adminEmailByOrganizationId, setAdminEmailByOrganizationId] = useState<Record<string, string>>({});
    const [adminRoleByOrganizationId, setAdminRoleByOrganizationId] = useState<
        Record<string, OrganizationAdminRole>
    >({});
    const [feedback, setFeedback] = useState<string | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    const submitNewOrganization = async (event: React.FormEvent) => {
        event.preventDefault();
        setFeedback(null);
        setErrorMessage(null);
        try {
            const organization = await createOrganization.mutateAsync({
                name: newOrganizationName.trim(),
                slug: newOrganizationSlug.trim() || undefined,
            });
            setNewOrganizationName("");
            setNewOrganizationSlug("");
            setFeedback(`Organization "${organization.name}" created.`);
        } catch (error) {
            setErrorMessage((error as Error).message);
        }
    };

    const inviteFirstAdmin = async (organization: PlatformOrganization) => {
        const email = (adminEmailByOrganizationId[organization.id] ?? "").trim();
        if (!email) return;
        const role = adminRoleByOrganizationId[organization.id] ?? defaultAdminRole;

        setFeedback(null);
        setErrorMessage(null);
        try {
            await bootstrapOrganizationAdmin.mutateAsync({
                organizationId: organization.id,
                email,
                role,
            });
            setAdminEmailByOrganizationId((current) => ({ ...current, [organization.id]: "" }));
            setFeedback(`Invited ${email} as the first ${role} of "${organization.name}".`);
        } catch (error) {
            setErrorMessage((error as Error).message);
        }
    };

    const changeStatus = async (organization: PlatformOrganization) => {
        setFeedback(null);
        setErrorMessage(null);
        const nextStatus = organization.status === "Suspended" ? "Active" : "Suspended";
        try {
            await setOrganizationStatus.mutateAsync({ id: organization.id, status: nextStatus });
            setFeedback(`"${organization.name}" is now ${nextStatus.toLowerCase()}.`);
        } catch (error) {
            setErrorMessage((error as Error).message);
        }
    };

    // Impersonation always asks for a reason: it is written to the audit record, and a crossing
    // nobody can justify afterwards is the one nobody can review.
    const impersonate = async (organization: PlatformOrganization) => {
        const reason = window.prompt(
            `Why are you entering "${organization.name}"? This is recorded in the audit log.`
        );
        if (!reason || !reason.trim()) return;

        setFeedback(null);
        setErrorMessage(null);
        try {
            const issuedToken = await startImpersonation.mutateAsync({
                organizationId: organization.id,
                reason: reason.trim(),
            });

            if (accessToken) {
                beginImpersonationSession({
                    platformAccessToken: accessToken,
                    organizationName: issuedToken.organization.name,
                    expiresAt: issuedToken.expiresAt,
                });
            }
            setAccessToken(issuedToken.accessToken);
            router.push("/tree");
        } catch (error) {
            setErrorMessage((error as Error).message);
        }
    };

    return (
        <div>
            <h1 className="text-xl font-bold text-ink mb-6">Organizations</h1>

            {feedback && (
                <p className="mb-4 text-sm text-olive" role="status">
                    {feedback}
                </p>
            )}
            {errorMessage && (
                <p className="mb-4 text-sm text-red-600" role="alert">
                    {errorMessage}
                </p>
            )}

            <form onSubmit={submitNewOrganization} className="mb-8 flex flex-wrap items-end gap-3">
                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    Name
                    <input
                        value={newOrganizationName}
                        onChange={(event) => setNewOrganizationName(event.target.value)}
                        required
                        className="px-3 py-2 text-sm rounded-xl border border-line bg-surface text-ink"
                        placeholder="Acme Sales"
                    />
                </label>
                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    Slug (optional)
                    <input
                        value={newOrganizationSlug}
                        onChange={(event) => setNewOrganizationSlug(event.target.value)}
                        className="px-3 py-2 text-sm rounded-xl border border-line bg-surface text-ink"
                        placeholder="acme-sales"
                    />
                </label>
                <button
                    type="submit"
                    disabled={createOrganization.isPending}
                    className="px-4 py-2 text-sm rounded-xl bg-indigo-soft text-indigo-ink font-medium disabled:opacity-50"
                >
                    Create organization
                </button>
            </form>

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : isError ? (
                <ErrorState onRetry={() => refetch()} />
            ) : (
                <div className="overflow-x-auto -mx-4 px-4">
                    <table className="w-full text-sm border-collapse min-w-[820px]">
                        <thead>
                            <tr className="border-b border-line">
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Name</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Slug</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Status</th>
                                <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">First admin</th>
                                <th className="py-2 px-3" />
                            </tr>
                        </thead>
                        <tbody>
                            {organizations.map((organization) => (
                                <tr key={organization.id} className="border-b border-line align-top">
                                    <td className="py-2.5 px-3 text-ink">{organization.name}</td>
                                    <td className="py-2.5 px-3 text-ink-3 text-xs">{organization.slug}</td>
                                    <td className="py-2.5 px-3">
                                        <span
                                            className={`inline-block px-2 py-0.5 text-xs rounded-full ${
                                                statusBadgeClass[organization.status] ?? "bg-bg-2 text-ink-3"
                                            }`}
                                        >
                                            {organization.status}
                                        </span>
                                    </td>
                                    <td className="py-2.5 px-3">
                                        {!canManageUsers ? (
                                            <span className="text-xs text-ink-4">
                                                Superadmins only
                                            </span>
                                        ) : (
                                        <div className="flex items-center gap-2">
                                            <input
                                                type="email"
                                                aria-label={`First admin email for ${organization.name}`}
                                                value={adminEmailByOrganizationId[organization.id] ?? ""}
                                                onChange={(event) =>
                                                    setAdminEmailByOrganizationId((current) => ({
                                                        ...current,
                                                        [organization.id]: event.target.value,
                                                    }))
                                                }
                                                placeholder="admin@customer.com"
                                                className="px-2 py-1 text-xs rounded-lg border border-line bg-surface text-ink"
                                            />
                                            <select
                                                aria-label={`First admin role for ${organization.name}`}
                                                value={adminRoleByOrganizationId[organization.id] ?? defaultAdminRole}
                                                onChange={(event) =>
                                                    setAdminRoleByOrganizationId((current) => ({
                                                        ...current,
                                                        [organization.id]: event.target.value as OrganizationAdminRole,
                                                    }))
                                                }
                                                className="px-2 py-1 text-xs rounded-lg border border-line bg-surface text-ink"
                                            >
                                                {(Object.keys(adminRoleLabels) as OrganizationAdminRole[]).map((role) => (
                                                    <option key={role} value={role}>
                                                        {adminRoleLabels[role]}
                                                    </option>
                                                ))}
                                            </select>
                                            <button
                                                type="button"
                                                onClick={() => inviteFirstAdmin(organization)}
                                                disabled={bootstrapOrganizationAdmin.isPending}
                                                className="text-xs text-indigo-ink hover:underline disabled:opacity-50"
                                            >
                                                Invite
                                            </button>
                                        </div>
                                        )}
                                    </td>
                                    <td className="py-2.5 px-3 text-right whitespace-nowrap">
                                        <button
                                            type="button"
                                            onClick={() => changeStatus(organization)}
                                            disabled={setOrganizationStatus.isPending}
                                            className="text-xs text-indigo-ink hover:underline disabled:opacity-50"
                                        >
                                            {organization.status === "Suspended" ? "Resume" : "Suspend"}
                                        </button>
                                        <Link
                                            href={`/admin/organizations/${organization.id}/quota`}
                                            className="ml-3 text-xs text-indigo-ink hover:underline"
                                        >
                                            Quota
                                        </Link>
                                        {canManageUsers && (
                                            <button
                                                type="button"
                                                onClick={() => impersonate(organization)}
                                                disabled={
                                                    startImpersonation.isPending
                                                    || organization.status === "Suspended"
                                                }
                                                className="ml-3 text-xs text-indigo-ink hover:underline disabled:opacity-50"
                                            >
                                                Impersonate
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <h2 className="text-base font-bold text-ink mt-10 mb-3">Recent impersonations</h2>
            {isImpersonationsError ? (
                <ErrorState compact onRetry={() => refetchImpersonations()} />
            ) : impersonations.length === 0 ? (
                <p className="text-sm text-ink-3">Nobody has entered a customer organization yet.</p>
            ) : (
                <ul className="space-y-2">
                    {impersonations.map((entry) => (
                        <li key={entry.id} className="text-xs text-ink-3">
                            <span className="text-ink">{entry.actorEmail}</span>
                            {" → "}
                            <span className="text-ink">{entry.organization.name}</span>
                            {" — "}
                            {entry.reason}
                            {" ("}
                            {new Date(entry.issuedAt).toLocaleString()}
                            {")"}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
