export type CompanyStatus = "Lead" | "Contacted" | "MeetingScheduled" | "DealWon" | "DealLost";

interface CompanyStatusMeta {
    readonly value: CompanyStatus;
    readonly label: string;
    readonly toneClassName: string;
}

export const COMPANY_STATUS_ORDER: readonly CompanyStatus[] = [
    "Lead",
    "Contacted",
    "MeetingScheduled",
    "DealWon",
    "DealLost",
];

export const COMPANY_STATUS_META: Record<CompanyStatus, CompanyStatusMeta> = {
    Lead: { value: "Lead", label: "Лид", toneClassName: "co-status--lead" },
    Contacted: { value: "Contacted", label: "Был контакт", toneClassName: "co-status--contacted" },
    MeetingScheduled: {
        value: "MeetingScheduled",
        label: "Встреча назначена",
        toneClassName: "co-status--meeting",
    },
    DealWon: { value: "DealWon", label: "Сделка закрыта", toneClassName: "co-status--won" },
    DealLost: { value: "DealLost", label: "Отказ", toneClassName: "co-status--lost" },
};
