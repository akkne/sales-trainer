import { apiClient } from "@/shared/api/api-client";
import type {
    CreateInvitesRequest,
    CreateInvitesResponse,
    InviteStatusFilter,
    MembershipStatusFilter,
    OrganizationInvite,
    OrganizationMember,
} from "@/features/org-people/types/organization-people";

const ORGANIZATION_PEOPLE_ROUTES = {
    memberships: (status: MembershipStatusFilter) => `/memberships?status=${status}`,
    membershipById: (userId: string) => `/memberships/${userId}`,
    invites: (status: InviteStatusFilter) => `/invites?status=${status}`,
    invitesBase: "/invites",
    inviteById: (inviteId: string) => `/invites/${inviteId}`,
} as const;

/// Every call O16 makes. Both reads are `RequireOrgAdmin` — a `TenancyAdmin` hands out assignments
/// to these people and cannot do it blind — and all three writes are `RequireOrgSuperAdmin`,
/// because adding and removing users is the one privilege the 2026-08-16 role split reserves.
///
/// There is no call that changes a member's role: `PUT /memberships/{userId}/role` does not exist
/// (docs/TENANCY/ADMIN_UI_DESIGN.md §6.2), and a client that faked one would be inventing a
/// contract.
export const organizationPeopleService = {
    listMemberships(status: MembershipStatusFilter): Promise<OrganizationMember[]> {
        return apiClient.get<OrganizationMember[]>(ORGANIZATION_PEOPLE_ROUTES.memberships(status));
    },

    listInvites(status: InviteStatusFilter): Promise<OrganizationInvite[]> {
        return apiClient.get<OrganizationInvite[]>(ORGANIZATION_PEOPLE_ROUTES.invites(status));
    },

    /// Answers partially: accepted addresses in `created`, refused ones in `rejected` with a
    /// machine-readable reason. A 200 here does not mean every address went through.
    createInvites(request: CreateInvitesRequest): Promise<CreateInvitesResponse> {
        return apiClient.post<CreateInvitesResponse>(
            ORGANIZATION_PEOPLE_ROUTES.invitesBase,
            request
        );
    },

    revokeInvite(inviteId: string): Promise<void> {
        return apiClient.delete<void>(ORGANIZATION_PEOPLE_ROUTES.inviteById(inviteId));
    },

    /// Offboarding. `DELETE` on the wire, `status = deactivated` in the database: there is no
    /// route that deletes a membership row, because the manager's attempts, recordings and scores
    /// are the organization's history (docs/TENANCY/TENANCY.md §4.3).
    deactivateMembership(userId: string): Promise<void> {
        return apiClient.delete<void>(ORGANIZATION_PEOPLE_ROUTES.membershipById(userId));
    },
};
