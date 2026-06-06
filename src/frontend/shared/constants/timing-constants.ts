export const TimingConstants = {
  oneSecondMs: 1_000,
  fiveSecondsMs: 5_000,
  thirtySecondsMs: 30_000,
  oneMinuteMs: 60_000,
  fiveMinutesMs: 5 * 60 * 1_000,
  oneDayMs: 1_000 * 60 * 60 * 24,
  deepgramConnectionTimeoutMs: 10_000,
  deepgramReconnectDelayMs: 1_000,
  chatRefetchIntervalMs: 5_000,
} as const
