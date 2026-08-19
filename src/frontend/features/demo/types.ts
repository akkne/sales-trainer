export type SalesTeamSize =
    | "UpToFive"
    | "SixToTwenty"
    | "TwentyOneToFifty"
    | "FiftyOneToTwoHundred"
    | "MoreThanTwoHundred";

export interface DemoRequestPayload {
    fullName: string;
    workEmail: string;
    phone: string;
    companyName: string;
    jobTitle: string;
    salesTeamSize: SalesTeamSize | "";
    comment: string;
    consentGiven: boolean;
    marketingConsentGiven: boolean;
    website: string;
}

export interface DemoRequestAcceptedResponse {
    id: string;
    submittedAt: string;
}

export interface DemoRequestThrottledResponse {
    message: string;
    retryAfterSeconds: number;
}
