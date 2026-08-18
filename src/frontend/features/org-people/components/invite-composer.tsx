"use client";

import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { Select, Textarea } from "@/shared/components/input";
import {
    INVITABLE_ORGANIZATION_ROLES,
    describeOrganizationRole,
} from "@/features/org-people/constants/people-dictionary";
import { parseInviteEmails } from "@/features/org-people/utils/invite-emails";

const EMAIL_FIELD_HINT = "По одному адресу в строке или через запятую.";

const ROLE_FIELD_HINT =
    "Роль применяется ко всем адресам сразу. Изменить её потом нельзя — маршрута, меняющего роль участника, в API нет; другая роль означает новое приглашение.";

const EMAIL_FIELD_PLACEHOLDER = "ivanov@acme.ru\npetrov@acme.ru, sokolova@acme.ru";

interface InviteComposerProps {
    rawEmails: string;
    onRawEmailsChange: (rawEmails: string) => void;
    role: string;
    onRoleChange: (role: string) => void;
    onSubmit: (emails: string[], role: string) => void;
    isPending: boolean;
    errorMessage: string | null;
}

/// One field for forty addresses, because onboarding a sales floor must not be forty clicks
/// (docs/TENANCY/TENANCY.md §4.3). The button carries the count so that «Отправить 3» after pasting
/// a column of forty is visible before the request, not after it.
export function InviteComposer({
    rawEmails,
    onRawEmailsChange,
    role,
    onRoleChange,
    onSubmit,
    isPending,
    errorMessage,
}: InviteComposerProps) {
    const parsedEmails = parseInviteEmails(rawEmails);
    const canSubmit = parsedEmails.length > 0 && !isPending;

    return (
        <Card className="mb-6">
            <CardContent style={{ marginTop: 0 }}>
                <h2 className="mb-4 text-xs font-medium uppercase tracking-wide text-ink-3">
                    Пригласить
                </h2>

                <div className="mb-4 max-w-xs">
                    <Select
                        label="Роль"
                        hint={ROLE_FIELD_HINT}
                        value={role}
                        onChange={(event) => onRoleChange(event.target.value)}
                        disabled={isPending}
                    >
                        {INVITABLE_ORGANIZATION_ROLES.map((invitableRole) => (
                            <option key={invitableRole} value={invitableRole}>
                                {describeOrganizationRole(invitableRole)}
                            </option>
                        ))}
                    </Select>
                </div>

                <Textarea
                    label="Адреса"
                    hint={EMAIL_FIELD_HINT}
                    placeholder={EMAIL_FIELD_PLACEHOLDER}
                    value={rawEmails}
                    onChange={(event) => onRawEmailsChange(event.target.value)}
                    disabled={isPending}
                    rows={4}
                />

                {errorMessage && (
                    <p className="mt-3 text-sm" style={{ color: "var(--bad)" }} role="alert">
                        {errorMessage}
                    </p>
                )}

                <div className="mt-4 flex justify-end">
                    <Button
                        variant="primary"
                        onClick={() => onSubmit(parsedEmails, role)}
                        disabled={!canSubmit}
                        loading={isPending}
                    >
                        {parsedEmails.length > 0
                            ? `Отправить ${parsedEmails.length}`
                            : "Отправить"}
                    </Button>
                </div>
            </CardContent>
        </Card>
    );
}
