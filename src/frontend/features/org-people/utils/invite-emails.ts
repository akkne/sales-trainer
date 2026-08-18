const EMAIL_SEPARATOR_PATTERN = /[\n\r,;]+/;

/// Splits a pasted block into addresses on newline, comma and semicolon — the three shapes a list
/// arrives in when it comes out of a spreadsheet column, an email client or a chat message.
///
/// Duplicates are **not** removed and nothing is lower-cased. Both are the server's job, and the
/// server answers with `duplicate-in-request` for exactly this case: silently collapsing two
/// identical lines here would hide from the РОП that their list had them.
export function parseInviteEmails(rawInput: string): string[] {
    return rawInput
        .split(EMAIL_SEPARATOR_PATTERN)
        .map((candidate) => candidate.trim())
        .filter((candidate) => candidate.length > 0);
}
