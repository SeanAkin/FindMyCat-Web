const BACKEND_MAX_HISTORY_RANGE_MS = 31 * 24 * 60 * 60 * 1000

export function validateHistoryRange(from: Date, to: Date): string | null {
  if (from.getTime() >= to.getTime()) {
    return 'The start date must be earlier than the end date.'
  }
  if (to.getTime() - from.getTime() > BACKEND_MAX_HISTORY_RANGE_MS) {
    return 'History range must not exceed 31 days.'
  }
  return null
}
