/// Every backend value this screen renders, translated once (docs/LOCALIZATION.md). Nothing here
/// is written as a literal in JSX: `TenancySuperAdmin` reading «Суперадминистратор» in the roster
/// and «суперадмин» in the invite select is how two screens start disagreeing about one enum.
///
/// Every lookup falls back to the raw backend value rather than to a guess or to «неизвестно». A
/// value this table has not heard of is a contract change, and showing it verbatim is what makes
/// that visible instead of silently smoothing it over.

export const ORGANIZATION_ROLE_LABELS: Record<string, string> = {
    Manager: "Менеджер",
    TenancyAdmin: "Администратор",
    TenancySuperAdmin: "Суперадминистратор",
};

/// The order the invite select offers. `Manager` first — it is the answer for almost every invite,
/// and the two administrator roles are what a РОП picks once per company.
export const INVITABLE_ORGANIZATION_ROLES = [
    "Manager",
    "TenancyAdmin",
    "TenancySuperAdmin",
] as const;

export const DEFAULT_INVITE_ROLE = "Manager";

export const MEMBERSHIP_STATUS_LABELS: Record<string, string> = {
    Active: "Работает",
    Deactivated: "Отключён",
};

/// The four states of an invite. All four can appear at once under «все приглашения», which is why
/// they have to read as four different things and not as «активно / неактивно».
export const INVITE_STATUS_LABELS: Record<string, string> = {
    pending: "Ждёт ответа",
    accepted: "Принято",
    revoked: "Отозвано",
    expired: "Истекло",
};

export const INVITE_STATUS_PENDING = "pending";

/// The four reasons `POST /invites` gives for an address it refused, verbatim from
/// docs/TENANCY/ADMIN_UI_DESIGN.md → O16.
export const INVITE_REJECTION_LABELS: Record<string, string> = {
    "invalid-email": "непохоже на адрес",
    "duplicate-in-request": "повторяется в списке",
    "already-a-member": "уже в компании",
    "invite-already-pending": "приглашение уже отправлено",
};

export function describeOrganizationRole(role: string): string {
    return ORGANIZATION_ROLE_LABELS[role] ?? role;
}

export function describeMembershipStatus(status: string): string {
    return MEMBERSHIP_STATUS_LABELS[status] ?? status;
}

export function describeInviteStatus(status: string): string {
    return INVITE_STATUS_LABELS[status] ?? status;
}

export function describeInviteRejection(reason: string): string {
    return INVITE_REJECTION_LABELS[reason] ?? reason;
}
