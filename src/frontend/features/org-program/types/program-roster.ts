/**
 * The two fields of `GET /memberships?status=active` (identity-service `MembershipDto`) that O18
 * needs: whom to offer for enrollment, and what to call the people already pinned.
 *
 * Declared here rather than imported from the people slice. The row carries more — email, role,
 * `joinedAt`, `deactivatedAt` — and this screen has no business reading any of it; narrowing the
 * shape at the boundary is what keeps «зачислить» from quietly growing into a second roster screen.
 */
export interface ProgramRosterMember {
    userId: string;
    displayName: string;
}
