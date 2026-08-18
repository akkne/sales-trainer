/**
 * Russian noun agreement for the two things this screen counts out loud.
 *
 * «Осталось ещё 4 вопрос» is the kind of detail that makes a paid product read as a prototype, and
 * the rule is not «add an s»: 1, 21, 31 take one form, 2–4 and 22–24 another, everything else a
 * third, and the teens are all exceptions.
 */
function pickRussianForm(count: number, forms: [string, string, string]): string {
    const absoluteCount = Math.abs(count) % 100;
    if (absoluteCount >= 11 && absoluteCount <= 14) return forms[2];

    switch (absoluteCount % 10) {
        case 1:
            return forms[0];
        case 2:
        case 3:
        case 4:
            return forms[1];
        default:
            return forms[2];
    }
}

export function formatQuestionCount(count: number): string {
    return `${count} ${pickRussianForm(count, ["вопрос", "вопроса", "вопросов"])}`;
}

export function formatOptionalQuestionCount(count: number): string {
    const adjective = pickRussianForm(count, [
        "необязательный",
        "необязательных",
        "необязательных",
    ]);
    return `${count} ${adjective} ${pickRussianForm(count, ["вопрос", "вопроса", "вопросов"])}`;
}

export function formatEntryCount(count: number): string {
    return `${count} ${pickRussianForm(count, ["запись", "записи", "записей"])}`;
}
