import { requestJson } from './http';

export type ReportItemType = 'Engineer' | 'Team';
export type ReportReason = 'Malicious' | 'Spam' | 'Copyright' | 'Other';

export interface ReportTarget {
  itemType: ReportItemType;
  itemId: string;
  label: string;
}

export interface ReportInput {
  itemType: ReportItemType;
  itemId: string;
  reason: ReportReason;
  details: string | null;
}

export interface SubmittedReport {
  id: string;
  status: string;
  createdAt: string;
}

export const REPORT_REASON_OPTIONS: { value: ReportReason; label: string }[] = [
  { value: 'Malicious', label: 'Malicious or unsafe behavior' },
  { value: 'Spam', label: 'Spam or misleading listing' },
  { value: 'Copyright', label: 'Copyright or license violation' },
  { value: 'Other', label: 'Other' },
];

export const DEFAULT_REPORT_REASON: ReportReason = 'Malicious';

export function normalizeReportDetails(details: string): string | null {
  const trimmed = details.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function canSubmitReport(reason: ReportReason, details: string): boolean {
  return reason !== 'Other' || normalizeReportDetails(details) !== null;
}

export function submitReport(input: ReportInput): Promise<SubmittedReport> {
  return requestJson<SubmittedReport>('/reports', { method: 'POST', body: input });
}
