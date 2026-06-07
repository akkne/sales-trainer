"use client";

import { use, useState } from "react";
import Link from "next/link";
import {
    useAdminLeagueDetail,
    useResyncLeague,
    useMoveMembershipTier,
    useAdjustMembershipXp,
    useRemoveLeagueMembership,
} from "@/features/admin/hooks/use-admin";

const TIERS = ["bronze", "silver", "gold", "diamond"];

const outcomeBadgeClass: Record<string, string> = {
    promoted: "bg-olive-soft text-olive",
    demoted: "bg-accent-soft text-accent",
};

export default function AdminLeagueDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = use(params);

    const { data: league, isLoading } = useAdminLeagueDetail(id);
    const resync = useResyncLeague();
    const moveTier = useMoveMembershipTier();
    const adjustXp = useAdjustMembershipXp();
    const removeMembership = useRemoveLeagueMembership();

    const [xpDrafts, setXpDrafts] = useState<Record<string, string>>({});
    const [confirmRemoveId, setConfirmRemoveId] = useState<string | null>(null);

    if (isLoading) {
        return <p className="text-sm text-ink-3">Loading...</p>;
    }
    if (!league) {
        return <p className="text-sm text-ink-3">League not found.</p>;
    }

    const handleApplyXp = (membershipId: string) => {
        const delta = Number(xpDrafts[membershipId]);
        if (!delta || Number.isNaN(delta)) return;
        adjustXp.mutate(
            { membershipId, delta },
            {
                onSuccess: () =>
                    setXpDrafts((drafts) => ({ ...drafts, [membershipId]: "" })),
            }
        );
    };

    const handleRemove = (membershipId: string) => {
        if (confirmRemoveId !== membershipId) {
            setConfirmRemoveId(membershipId);
            return;
        }
        removeMembership.mutate(membershipId, {
            onSettled: () => setConfirmRemoveId(null),
        });
    };

    return (
        <div>
            <div className="mb-2">
                <Link
                    href="/admin/leagues"
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← All leagues
                </Link>
            </div>
            <div className="flex items-center justify-between mb-6">
                <div>
                    <h1 className="text-xl font-bold text-ink capitalize">
                        {league.tier} league
                    </h1>
                    <p className="text-xs text-ink-3 mt-0.5">
                        {league.weekStartDate} — {league.weekEndDate} ·{" "}
                        {league.members.length} members
                    </p>
                </div>
                <button
                    onClick={() => resync.mutate(league.id)}
                    disabled={resync.isPending}
                    className="text-sm px-3 py-1.5 rounded-lg border border-line text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors disabled:opacity-50"
                >
                    {resync.isPending ? "Syncing..." : "Force XP re-sync"}
                </button>
            </div>

            {league.members.length === 0 ? (
                <p className="text-sm text-ink-3">No members in this league.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                #
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Member
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Weekly XP
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Outcome
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Move to tier
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Adjust XP
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {league.members.map((member, index) => (
                            <tr
                                key={member.membershipId}
                                className="border-b border-line hover:bg-bg-2"
                            >
                                <td className="py-2.5 px-3 text-ink-3 text-xs">
                                    {member.rank > 0 ? member.rank : index + 1}
                                </td>
                                <td className="py-2.5 px-3">
                                    <span className="text-ink">{member.displayName}</span>
                                    <span className="block text-xs text-ink-4">
                                        {member.email}
                                    </span>
                                </td>
                                <td className="py-2.5 px-3 text-ink font-medium">
                                    {member.weeklyXpAmount}
                                </td>
                                <td className="py-2.5 px-3">
                                    {member.promotionOutcome ? (
                                        <span
                                            className={`inline-block px-2 py-0.5 text-xs rounded-full capitalize ${
                                                outcomeBadgeClass[member.promotionOutcome] ??
                                                "bg-bg-2 text-ink-3"
                                            }`}
                                        >
                                            {member.promotionOutcome}
                                        </span>
                                    ) : (
                                        <span className="text-xs text-ink-4">—</span>
                                    )}
                                </td>
                                <td className="py-2.5 px-3">
                                    <select
                                        value={league.tier}
                                        disabled={moveTier.isPending}
                                        onChange={(e) =>
                                            moveTier.mutate({
                                                membershipId: member.membershipId,
                                                tier: e.target.value,
                                            })
                                        }
                                        className="text-xs border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30 disabled:opacity-50"
                                    >
                                        {TIERS.map((t) => (
                                            <option key={t} value={t}>
                                                {t}
                                            </option>
                                        ))}
                                    </select>
                                </td>
                                <td className="py-2.5 px-3">
                                    <div className="flex items-center gap-1">
                                        <input
                                            type="number"
                                            placeholder="±XP"
                                            value={xpDrafts[member.membershipId] ?? ""}
                                            onChange={(e) =>
                                                setXpDrafts((drafts) => ({
                                                    ...drafts,
                                                    [member.membershipId]: e.target.value,
                                                }))
                                            }
                                            className="w-20 text-xs border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                        />
                                        <button
                                            onClick={() => handleApplyXp(member.membershipId)}
                                            disabled={
                                                adjustXp.isPending ||
                                                !xpDrafts[member.membershipId]
                                            }
                                            className="text-xs px-2 py-1 rounded border border-line text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors disabled:opacity-50"
                                        >
                                            Apply
                                        </button>
                                    </div>
                                </td>
                                <td className="py-2.5 px-3 text-right">
                                    <button
                                        onClick={() => handleRemove(member.membershipId)}
                                        onBlur={() => setConfirmRemoveId(null)}
                                        disabled={removeMembership.isPending}
                                        className={`text-xs px-2 py-1 rounded transition-colors disabled:opacity-50 ${
                                            confirmRemoveId === member.membershipId
                                                ? "bg-red-500 text-white"
                                                : "text-ink-3 hover:text-red-500"
                                        }`}
                                    >
                                        {confirmRemoveId === member.membershipId
                                            ? "Confirm?"
                                            : "Remove"}
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}
