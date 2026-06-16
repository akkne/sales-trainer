"use client";

import { useEffect, useState } from "react";
import {
    useAdminUser,
    useChangeUserRole,
    useDeleteUserAvatar,
    useUpdateUser,
} from "@/features/admin/hooks/use-admin";
import { UserAvatar } from "@/shared/components/user-avatar";

const ROLES = ["User", "Admin", "SuperAdmin"];

interface UserDetailModalProps {
    userId: string;
    canChangeRole: boolean;
    isSelf: boolean;
    onClose: () => void;
}

export function UserDetailModal({ userId, canChangeRole, isSelf, onClose }: UserDetailModalProps) {
    const { data: user, isLoading } = useAdminUser(userId);
    const updateUser = useUpdateUser();
    const changeRole = useChangeUserRole();
    const deleteAvatar = useDeleteUserAvatar();

    const [displayName, setDisplayName] = useState("");
    // Cache-busts the avatar <img> after we reset it server-side.
    const [avatarVersion, setAvatarVersion] = useState(0);

    useEffect(() => {
        if (user) setDisplayName(user.displayName);
    }, [user]);

    const trimmedName = displayName.trim();
    const nameValid = trimmedName.length >= 2 && trimmedName.length <= 50;
    const nameChanged = !!user && trimmedName !== user.displayName;

    const avatarUrl = user
        ? avatarVersion > 0
            ? `${user.avatarUrl}?v=${avatarVersion}`
            : user.avatarUrl
        : undefined;

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
            onClick={onClose}
        >
            <div
                className="w-full max-w-lg max-h-[90vh] overflow-y-auto rounded-xl bg-bg-1 p-6 shadow-xl"
                onClick={(e) => e.stopPropagation()}
            >
                {isLoading || !user ? (
                    <p className="text-sm text-ink-3">Loading...</p>
                ) : (
                    <>
                        <div className="mb-5 flex items-start gap-4">
                            <UserAvatar
                                avatarUrl={avatarUrl}
                                seed={user.displayName}
                                size={64}
                                circle
                            />
                            <div className="min-w-0 flex-1">
                                <h2 className="truncate text-lg font-bold text-ink">
                                    {user.displayName}
                                    {isSelf && (
                                        <span className="ml-1 text-xs text-ink-4">(you)</span>
                                    )}
                                </h2>
                                <p className="truncate text-sm text-ink-3">{user.email}</p>
                                <div className="mt-1 flex flex-wrap gap-1.5 text-xs">
                                    <span className="rounded-full bg-bg-2 px-2 py-0.5 text-ink-3">
                                        {user.authProvider}
                                    </span>
                                    <span
                                        className={`rounded-full px-2 py-0.5 ${
                                            user.isEmailVerified
                                                ? "bg-olive-soft text-olive"
                                                : "bg-bg-2 text-ink-3"
                                        }`}
                                    >
                                        {user.isEmailVerified ? "Email verified" : "Email unverified"}
                                    </span>
                                </div>
                            </div>
                            <button
                                onClick={onClose}
                                className="text-ink-3 hover:text-ink"
                                aria-label="Close"
                            >
                                ✕
                            </button>
                        </div>

                        {/* Photo moderation */}
                        <section className="mb-5">
                            <h3 className="mb-1.5 text-xs font-medium text-ink-3">Photo</h3>
                            {user.hasCustomAvatar ? (
                                <button
                                    onClick={() =>
                                        deleteAvatar.mutate(user.id, {
                                            onSuccess: () => setAvatarVersion((v) => v + 1),
                                        })
                                    }
                                    disabled={deleteAvatar.isPending}
                                    className="rounded border border-bad/40 px-3 py-1.5 text-xs text-bad hover:bg-bad-soft disabled:opacity-50"
                                >
                                    {deleteAvatar.isPending
                                        ? "Removing..."
                                        : "Remove photo (reset to default)"}
                                </button>
                            ) : (
                                <p className="text-xs text-ink-4">Using a default avatar.</p>
                            )}
                        </section>

                        {/* Display name */}
                        <section className="mb-5">
                            <h3 className="mb-1.5 text-xs font-medium text-ink-3">Display name</h3>
                            <div className="flex gap-2">
                                <input
                                    value={displayName}
                                    onChange={(e) => setDisplayName(e.target.value)}
                                    maxLength={50}
                                    className="flex-1 rounded border border-line px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                />
                                <button
                                    onClick={() =>
                                        updateUser.mutate({ id: user.id, displayName: trimmedName })
                                    }
                                    disabled={!nameValid || !nameChanged || updateUser.isPending}
                                    className="rounded bg-indigo px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                                >
                                    {updateUser.isPending ? "Saving..." : "Save"}
                                </button>
                            </div>
                            {!nameValid && (
                                <p className="mt-1 text-xs text-bad">
                                    Must be 2–50 characters.
                                </p>
                            )}
                        </section>

                        {/* Role */}
                        {canChangeRole && (
                            <section className="mb-5">
                                <h3 className="mb-1.5 text-xs font-medium text-ink-3">Role</h3>
                                {isSelf ? (
                                    <p className="text-xs text-ink-4">
                                        You cannot change your own role.
                                    </p>
                                ) : (
                                    <select
                                        value={user.role}
                                        disabled={changeRole.isPending}
                                        onChange={(e) =>
                                            changeRole.mutate({ id: user.id, role: e.target.value })
                                        }
                                        className="rounded border border-line px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 disabled:opacity-50"
                                    >
                                        {ROLES.map((r) => (
                                            <option key={r} value={r}>
                                                {r}
                                            </option>
                                        ))}
                                    </select>
                                )}
                            </section>
                        )}

                        {/* Stats */}
                        <section className="mb-2">
                            <h3 className="mb-2 text-xs font-medium text-ink-3">Activity</h3>
                            <div className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                                <Stat label="Total XP" value={user.totalXpAmount} />
                                <Stat label="Current streak" value={`${user.currentStreakDayCount}d`} />
                                <Stat label="Longest streak" value={`${user.longestStreakDayCount}d`} />
                                <Stat
                                    label="Skills"
                                    value={`${user.completedSkillCount}/${user.totalSkillCount}`}
                                />
                                <Stat label="Avg score" value={user.averageExerciseScore} />
                                <Stat
                                    label="Registered"
                                    value={new Date(user.createdAt).toLocaleDateString()}
                                />
                            </div>
                            {user.persona && (
                                <p className="mt-3 text-xs text-ink-3">
                                    <span className="text-ink-4">Persona:</span> {user.persona}
                                </p>
                            )}
                        </section>
                    </>
                )}
            </div>
        </div>
    );
}

function Stat({ label, value }: { label: string; value: string | number }) {
    return (
        <div className="rounded-lg bg-bg-2 px-3 py-2">
            <div className="text-xs text-ink-4">{label}</div>
            <div className="font-semibold text-ink">{value}</div>
        </div>
    );
}
