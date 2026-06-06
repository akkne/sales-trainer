"use client";

import { useAdminUsers, useChangeUserRole } from "@/features/admin/hooks/use-admin";
import { useAuthStore } from "@/shared/stores/auth-store";

const ROLES = ["User", "Admin", "SuperAdmin"];

const roleBadgeClass: Record<string, string> = {
    User: "bg-bg-2 text-ink-3",
    Admin: "bg-accent-soft text-accent",
    SuperAdmin: "bg-olive-soft text-olive",
};

export default function AdminUsersPage() {
    const { authenticatedUser } = useAuthStore();
    const { data: users = [], isLoading } = useAdminUsers();
    const changeRole = useChangeUserRole();

    if (authenticatedUser?.role !== "SuperAdmin") {
        return (
            <div>
                <h1 className="text-xl font-bold text-ink mb-4">Users</h1>
                <p className="text-sm text-ink-3">
                    Only SuperAdmins can access this page.
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
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Email
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Display name
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
                                    className="border-b border-line hover:bg-bg-2"
                                >
                                    <td className="py-2.5 px-3 text-ink">{user.email}</td>
                                    <td className="py-2.5 px-3 text-ink-3">
                                        {user.displayName}
                                        {isSelf && (
                                            <span className="ml-1 text-xs text-ink-4">
                                                (you)
                                            </span>
                                        )}
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
                                        {!isSelf && (
                                            <select
                                                value={user.role}
                                                disabled={changeRole.isPending}
                                                onChange={(e) =>
                                                    changeRole.mutate({
                                                        id: user.id,
                                                        role: e.target.value,
                                                    })
                                                }
                                                className="text-xs border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30 disabled:opacity-50"
                                            >
                                                {ROLES.map((r) => (
                                                    <option key={r} value={r}>
                                                        {r}
                                                    </option>
                                                ))}
                                            </select>
                                        )}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            )}
        </div>
    );
}
