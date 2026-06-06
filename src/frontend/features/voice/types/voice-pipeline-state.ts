export type VoicePipelineState =
    | "idle"
    | "initializing"
    | "listening"
    | "speaking"
    | "processing"
    | "playing"
    | "error";
