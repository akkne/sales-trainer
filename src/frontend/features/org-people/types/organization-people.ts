/// One row of `GET /memberships` in identity-service. `role` and `status` arrive as the enum
/// names («Manager», «Active»), not as numbers: identity-service registers no
/// `JsonStringEnumConverter` and the DTO spells them out for that reason.
export interface OrganizationMember {
    userId: string;
    email: string;
    displayName: string;
    role: string;
    status: string;
    joinedAt: string;
    deactivatedAt: string | null;
}

/// One row of `GET /invites`. There is deliberately no `token`: the raw single-use token exists
/// once, in the creation response and in the invitee's mailbox, and a listing that returned it
/// would turn any administrator read into a takeover of a pending invitee's account.
///
/// `status` is derived on the server — `pending` / `accepted` / `revoked` / `expired`, with the
/// recorded facts outranking the clock — so an accepted invite whose expiry has passed still reads
/// `accepted`. The browser never recomputes it.
export interface OrganizationInvite {
    id: string;
    email: string;
    role: string;
    status: string;
    invitedBy: string | null;
    createdAt: string;
    expiresAt: string;
}

export interface CreateInvitesRequest {
    emails: string[];
    role: string;
}

/// The invite that was created. `token` is present on the wire and is deliberately absent from
/// this type: the screen has no control that shows, copies or links it, and a field nothing can
/// read is the cheapest way to keep it that way.
export interface CreatedInvite {
    id: string;
    email: string;
    role: string;
    expiresAt: string;
}

export interface RejectedInvite {
    email: string;
    reason: string;
}

/// `POST /invites` answers partially on purpose: one malformed address in a pasted list of forty
/// must not discard the other thirty-nine.
export interface CreateInvitesResponse {
    created: CreatedInvite[];
    rejected: RejectedInvite[];
}

export type MembershipStatusFilter = "active" | "all";

export type InviteStatusFilter = "pending" | "all";
