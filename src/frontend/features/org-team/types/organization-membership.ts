export const MEMBERSHIP_STATUSES = {
    active: "Active",
    deactivated: "Deactivated",
} as const;

export type MembershipStatus = (typeof MEMBERSHIP_STATUSES)[keyof typeof MEMBERSHIP_STATUSES];

/// One row of `GET /memberships?status=all` in identity-service. `role` and `status` arrive as the
/// enum names, not as numbers — identity-service registers no `JsonStringEnumConverter`, and the
/// DTO spells them out for that reason.
export interface OrganizationMembership {
    userId: string;
    email: string;
    displayName: string;
    role: string;
    status: string;
    joinedAt: string;
    deactivatedAt: string | null;
}
