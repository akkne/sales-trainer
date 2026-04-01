"use client";

import { useAdminUsers, useChangeUserRole } from "@/lib/hooks/useAdmin";
import { useAuthStore } from "@/lib/store/authStore";

const ROLES = ["User", "Admin", "SuperAdmin"];

const roleBadgeClass: Record<string, string> = {
    User: "bg-gray-100 text-gray-600",
    Admin: "bg-blue-100 text-blue-700",
    SuperAdmin: "bg-purple-100 text-purple-700",
};

export default function AdminUsersPage() {
    const { authenticatedUser } = useAuthStore();
    const { data: users = [], isLoading } = useAdminUsers();
    const changeRole = useChangeUserRole();

    if (authenticatedUser?.role !== "SuperAdmin") {
        return (
            <div>
                <h1 className="text-xl font-semibold text-gray-900 mb-4">Users</h1>
                <p className="text-sm text-gray-400">
                    Only SuperAdmins can access this page.
                </p>
            </div>
        );
    }

    return (
        <div>
            <h1 className="text-xl font-semibold text-gray-900 mb-6">Users</h1>

            {isLoading ? (
                <p className="text-sm text-gray-400">Loading...</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-gray-200">
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Email
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Display name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Role
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
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
                                    className="border-b border-gray-100 hover:bg-gray-50"
                                >
                                    <td className="py-2.5 px-3 text-gray-800">{user.email}</td>
                                    <td className="py-2.5 px-3 text-gray-500">
                                        {user.displayName}
                                        {isSelf && (
                                            <span className="ml-1 text-xs text-gray-300">
                                                (you)
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-2.5 px-3">
                                        <span
                                            className={`inline-block px-2 py-0.5 text-xs rounded-full ${
                                                roleBadgeClass[user.role] ??
                                                "bg-gray-100 text-gray-600"
                                            }`}
                                        >
                                            {user.role}
                                        </span>
                                    </td>
                                    <td className="py-2.5 px-3 text-gray-400 text-xs">
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
                                                className="text-xs border border-gray-300 rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-gray-400 disabled:opacity-50"
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
