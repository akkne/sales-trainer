"use client";

import { useAdminUsers, useChangeUserRole } from "@/lib/hooks/useAdmin";
import { useAuthStore } from "@/lib/store/authStore";

const ROLES = ["User", "Admin", "SuperAdmin"];

const roleBadgeClass: Record<string, string> = {
    User: "bg-surface-container text-on-surface-variant",
    Admin: "bg-tertiary-container text-tertiary",
    SuperAdmin: "bg-secondary-container text-secondary",
};

export default function AdminUsersPage() {
    const { authenticatedUser } = useAuthStore();
    const { data: users = [], isLoading } = useAdminUsers();
    const changeRole = useChangeUserRole();

    if (authenticatedUser?.role !== "SuperAdmin") {
        return (
            <div>
                <h1 className="font-headline text-xl font-bold text-on-surface mb-4">Users</h1>
                <p className="text-sm text-on-surface-variant">
                    Only SuperAdmins can access this page.
                </p>
            </div>
        );
    }

    return (
        <div>
            <h1 className="font-headline text-xl font-bold text-on-surface mb-6">Users</h1>

            {isLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-outline-variant">
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Email
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Display name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Role
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
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
                                    className="border-b border-surface-container hover:bg-surface-container-low"
                                >
                                    <td className="py-2.5 px-3 text-on-surface">{user.email}</td>
                                    <td className="py-2.5 px-3 text-on-surface-variant">
                                        {user.displayName}
                                        {isSelf && (
                                            <span className="ml-1 text-xs text-outline-variant">
                                                (you)
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-2.5 px-3">
                                        <span
                                            className={`inline-block px-2 py-0.5 text-xs rounded-full ${
                                                roleBadgeClass[user.role] ??
                                                "bg-surface-container text-on-surface-variant"
                                            }`}
                                        >
                                            {user.role}
                                        </span>
                                    </td>
                                    <td className="py-2.5 px-3 text-on-surface-variant text-xs">
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
                                                className="text-xs border border-outline-variant rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-primary disabled:opacity-50"
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
