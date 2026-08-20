"use client";

import { Fragment, useState } from "react";
import {
    useAdminDemoRequests,
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

const DETAIL_COLUMN_COUNT = 9;

export default function AdminDemoRequestsPage() {
    const { data: demoRequests = [], isLoading, error } = useAdminDemoRequests();
    const updateStatus = useUpdateDemoRequestStatus();

    const [expandedId, setExpandedId] = useState<string | null>(null);
    // Approving a lead sends the customer an email saying their request was approved, which is not
    // obvious from a plain dropdown (docs/DEMO_REQUEST.md). That one transition is held here until a
    // person presses an inline "Confirm approval" button; every other transition fires at once.
    const [pendingApprovalId, setPendingApprovalId] = useState<string | null>(null);

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

    return (
        <div>
            <h1 className="text-xl font-bold text-ink mb-1">Demo requests</h1>
            <p className="text-xs text-ink-3 mb-6">
                Leads from the public demo-request form, newest first. Nothing here belongs to an
                organization — these are prospects who have not signed up yet.
            </p>

            {isLoading && <p className="text-sm text-ink-3">Loading...</p>}
            {error && <p className="text-sm text-bad">Error: {(error as Error).message}</p>}

            {!isLoading && !error && demoRequests.length === 0 && (
                <p className="text-sm text-ink-3">No demo requests yet.</p>
            )}

            {!isLoading && !error && demoRequests.length > 0 && (
                <div className="overflow-x-auto -mx-4 px-4">
                    <table className="w-full text-sm border-collapse min-w-[1100px]">
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
                                <th className="py-2 px-3" />
                            </tr>
                        </thead>
                        <tbody>
                            {demoRequests.map((request) => {
                                const isExpanded = expandedId === request.id;
                                const hasDetail = Boolean(request.jobTitle) || Boolean(request.comment);

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
