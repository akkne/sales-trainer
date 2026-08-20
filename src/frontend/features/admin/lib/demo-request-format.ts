import type { SalesTeamSize } from "@/features/demo/types";

/// Wire values for `salesTeamSize` are the English enum names (docs/DEMO_REQUEST.md); this is the
/// only place the platform panel turns them into something a reader can scan a table of.
export const SALES_TEAM_SIZE_LABELS: Record<SalesTeamSize, string> = {
    UpToFive: "1–5",
    SixToTwenty: "6–20",
    TwentyOneToFifty: "21–50",
    FiftyOneToTwoHundred: "51–200",
    MoreThanTwoHundred: "200+",
};

export type DemoRequestStatus = "New" | "Contacted" | "Approved" | "Declined";

/// `Status` moves `New -> Contacted -> Approved | Declined`, but nothing on the backend enforces
/// that order (docs/DEMO_REQUEST.md) — this is only the order the dropdown lists them in.
export const DEMO_REQUEST_STATUSES: DemoRequestStatus[] = ["New", "Contacted", "Approved", "Declined"];

/// Moving a lead to `Approved` sends the customer an email, which is not obvious from a plain
/// dropdown — this is the one transition the screen must gate behind an inline confirmation.
export const STATUS_REQUIRING_CONFIRMATION: DemoRequestStatus = "Approved";
