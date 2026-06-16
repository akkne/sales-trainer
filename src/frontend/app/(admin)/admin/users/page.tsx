"use client";

import { useState } from "react";
import { useAdminUsers } from "@/features/admin/hooks/use-admin";
import { UserDetailModal } from "@/features/admin/components/user-detail-modal";
import { UserAvatar } from "@/shared/components/user-avatar";
import { useAuthStore } from "@/shared/stores/auth-store";

const roleBadgeClass: Record<string, string> = {
    User: "bg-bg-2 text-ink-3",
    Admin: "bg-accent-soft text-accent",
    SuperAdmin: "bg-olive-soft text-olive",
};

export default function AdminUsersPage() {
    const { authenticatedUser } = useAuthStore();
    const { data: users = [], isLoading } = useAdminUsers();
    const [selectedId, setSelectedId] = useState<string | null>(null);

    const role = authenticatedUser?.role;
    const isAdmin = role === "Admin" || role === "SuperAdmin";
    const canChangeRole = role === "SuperAdmin";

    if (!isAdmin) {
        return (
            <div>
                <h1 className="text-xl font-bold text-ink mb-4">Users</h1>
                <p className="text-sm text-ink-3">
                    Only admins can access this page.
                </p>
            </div>
        );
    }

    return (
        <div>
            <h1 className="text-xl font-bold text-ink mb-6">Users</h1>

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : (
                <div className="overflow-x-auto -mx-4 px-4">
                <table className="w-full text-sm border-collapse min-w-[640px]">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium" />
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Email
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Display name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Provider
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Role
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Registered
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {users.map((user) => {
                            const isSelf = user.id === authenticatedUser?.id;
                            return (
                                <tr
                                    key={user.id}
                                    className="border-b border-line hover:bg-bg-2 cursor-pointer"
                                    onClick={() => setSelectedId(user.id)}
                                >
                                    <td className="py-2 px-3 w-8">
                                        <UserAvatar
                                            avatarUrl={user.avatarUrl}
                                            seed={user.displayName}
                                            size={28}
                                            circle
                                        />
                                    </td>
                                    <td className="py-2.5 px-3 text-ink">
                                        {user.email}
                                        {!user.isEmailVerified && (
                                            <span className="ml-1 text-xs text-ink-4">
                                                (unverified)
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-2.5 px-3 text-ink-3">
                                        {user.displayName}
                                        {isSelf && (
                                            <span className="ml-1 text-xs text-ink-4">
                                                (you)
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-2.5 px-3 text-ink-3 text-xs">
                                        {user.authProvider}
                                    </td>
                                    <td className="py-2.5 px-3">
                                        <span
                                            className={`inline-block px-2 py-0.5 text-xs rounded-full ${
                                                roleBadgeClass[user.role] ??
                                                "bg-bg-2 text-ink-3"
                                            }`}
                                        >
                                            {user.role}
                                        </span>
                                    </td>
                                    <td className="py-2.5 px-3 text-ink-3 text-xs">
                                        {new Date(user.createdAt).toLocaleDateString()}
                                    </td>
                                    <td className="py-2.5 px-3 text-right">
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                setSelectedId(user.id);
                                            }}
                                            className="text-xs text-indigo hover:underline"
                                        >
                                            Manage
                                        </button>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
                </div>
            )}

            {selectedId && (
                <UserDetailModal
                    userId={selectedId}
                    canChangeRole={canChangeRole}
                    isSelf={selectedId === authenticatedUser?.id}
                    onClose={() => setSelectedId(null)}
                />
            )}
        </div>
    );
}
