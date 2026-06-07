"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminLeagues,
    useAdminLeagueWeeks,
    useAdminLeagueSettings,
    useUpdateLeagueSettings,
    useCloseCurrentLeagueWeek,
    type AdminLeagueSettings,
} from "@/features/admin/hooks/use-admin";

const TIERS = ["bronze", "silver", "gold", "diamond"];

const tierBadgeClass: Record<string, string> = {
    bronze: "bg-accent-soft text-accent",
    silver: "bg-bg-2 text-ink-3",
    gold: "bg-olive-soft text-olive",
    diamond: "bg-indigo-soft text-indigo",
};

export default function AdminLeaguesPage() {
    const [weekStart, setWeekStart] = useState("");
    const [tier, setTier] = useState("");

    const { data: leagues = [], isLoading } = useAdminLeagues({
        weekStart: weekStart || undefined,
        tier: tier || undefined,
    });
    const { data: weeks = [] } = useAdminLeagueWeeks();
    const { data: settings } = useAdminLeagueSettings();
    const updateSettings = useUpdateLeagueSettings();
    const closeWeek = useCloseCurrentLeagueWeek();

    const [showSettings, setShowSettings] = useState(false);
    const [settingsForm, setSettingsForm] = useState<AdminLeagueSettings | null>(null);
    const [confirmClose, setConfirmClose] = useState(false);

    const openSettings = () => {
        if (settings) setSettingsForm({ ...settings });
        setShowSettings(true);
    };

    const handleSaveSettings = () => {
        if (!settingsForm) return;
        updateSettings.mutate(settingsForm, {
            onSuccess: () => setShowSettings(false),
        });
    };

    const handleCloseWeek = () => {
        if (!confirmClose) {
            setConfirmClose(true);
            return;
        }
        closeWeek.mutate(undefined, { onSettled: () => setConfirmClose(false) });
    };

    return (
        <div>
            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-bold text-ink">Leagues</h1>
                <div className="flex items-center gap-2">
                    <button
                        onClick={openSettings}
                        disabled={!settings}
                        className="text-sm px-3 py-1.5 rounded-lg border border-line text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors disabled:opacity-50"
                    >
                        Settings
                    </button>
                    <button
                        onClick={handleCloseWeek}
                        disabled={closeWeek.isPending}
                        className={`text-sm px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50 ${
                            confirmClose
                                ? "bg-red-500 text-white"
                                : "border border-line text-ink-3 hover:text-ink hover:bg-bg-2"
                        }`}
                        onBlur={() => setConfirmClose(false)}
                    >
                        {closeWeek.isPending
                            ? "Closing..."
                            : confirmClose
                              ? "Confirm close week?"
                              : "Close current week"}
                    </button>
                </div>
            </div>

            {showSettings && settingsForm && (
                <div className="mb-6 p-4 border border-line rounded-xl bg-bg-2/50">
                    <h2 className="text-sm font-semibold text-ink mb-3">League settings</h2>
                    <div className="flex flex-wrap items-end gap-4">
                        {(
                            [
                                ["maximumLeagueParticipantCount", "Max participants"],
                                ["promotionZoneSize", "Promotion zone"],
                                ["demotionZoneSize", "Demotion zone"],
                            ] as const
                        ).map(([key, label]) => (
                            <label key={key} className="text-xs text-ink-3">
                                {label}
                                <input
                                    type="number"
                                    min={1}
                                    value={settingsForm[key]}
                                    onChange={(e) =>
                                        setSettingsForm({
                                            ...settingsForm,
                                            [key]: Number(e.target.value),
                                        })
                                    }
                                    className="block mt-1 w-32 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                />
                            </label>
                        ))}
                        <div className="flex gap-2">
                            <button
                                onClick={handleSaveSettings}
                                disabled={updateSettings.isPending}
                                className="text-sm px-3 py-1.5 rounded-lg bg-indigo text-white hover:opacity-90 transition-opacity disabled:opacity-50"
                            >
                                {updateSettings.isPending ? "Saving..." : "Save"}
                            </button>
                            <button
                                onClick={() => setShowSettings(false)}
                                className="text-sm px-3 py-1.5 rounded-lg border border-line text-ink-3 hover:text-ink transition-colors"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                    {updateSettings.isError && (
                        <p className="text-xs text-red-500 mt-2">
                            {(updateSettings.error as Error).message}
                        </p>
                    )}
                </div>
            )}

            <div className="flex items-center gap-3 mb-4">
                <select
                    value={weekStart}
                    onChange={(e) => setWeekStart(e.target.value)}
                    className="text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                >
                    <option value="">All weeks</option>
                    {weeks.map((w) => (
                        <option key={w} value={w}>
                            Week of {w}
                        </option>
                    ))}
                </select>
                <select
                    value={tier}
                    onChange={(e) => setTier(e.target.value)}
                    className="text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                >
                    <option value="">All tiers</option>
                    {TIERS.map((t) => (
                        <option key={t} value={t}>
                            {t}
                        </option>
                    ))}
                </select>
            </div>

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : leagues.length === 0 ? (
                <p className="text-sm text-ink-3">No leagues found.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Tier
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Week
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Members
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {leagues.map((league) => (
                            <tr
                                key={league.id}
                                className="border-b border-line hover:bg-bg-2"
                            >
                                <td className="py-2.5 px-3">
                                    <span
                                        className={`inline-block px-2 py-0.5 text-xs rounded-full capitalize ${
                                            tierBadgeClass[league.tier] ??
                                            "bg-bg-2 text-ink-3"
                                        }`}
                                    >
                                        {league.tier}
                                    </span>
                                </td>
                                <td className="py-2.5 px-3 text-ink-3 text-xs">
                                    {league.weekStartDate} — {league.weekEndDate}
                                </td>
                                <td className="py-2.5 px-3 text-ink">{league.memberCount}</td>
                                <td className="py-2.5 px-3 text-right">
                                    <Link
                                        href={`/admin/leagues/${league.id}`}
                                        className="text-xs text-indigo hover:underline"
                                    >
                                        Manage
                                    </Link>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}
