import type { SalesTeamSize } from "@/features/demo/types";

export interface SalesTeamSizeOption {
    value: SalesTeamSize;
    label: string;
}

/// The value sent to the backend is the English enum name; the label is the Russian copy shown
/// in the select. Order matches ascending team size.
export const SALES_TEAM_SIZE_OPTIONS: SalesTeamSizeOption[] = [
    { value: "UpToFive", label: "1–5 человек" },
    { value: "SixToTwenty", label: "6–20 человек" },
    { value: "TwentyOneToFifty", label: "21–50 человек" },
    { value: "FiftyOneToTwoHundred", label: "51–200 человек" },
    { value: "MoreThanTwoHundred", label: "Больше 200 человек" },
];
