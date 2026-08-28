export interface CatalogItem {
  emoji: string;
  name: string;
  author?: string;
  description: string;
  tags: string[];
  version?: string;
  installs: number;
  team?: boolean;
  members?: string[];
}

export interface VersionInfo {
  version: string;
  date: string;
  size: string;
  sha: string;
}

export interface TeamMemberInfo {
  emoji: string;
  name: string;
  author: string;
  pinnedVersion: string;
}

export interface ScanFinding {
  rule: string;
  severity: 'critical' | 'warning';
  file: string;
  line: string;
  message: string;
  excerpt: string;
}

export interface DraftSkill {
  name: string;
  source: 'catalog' | 'github' | 'upload';
  size: string;
}

export interface CrewMember {
  emoji: string;
  name: string;
  pinnedVersion: string;
}

export interface WorkspaceRow {
  emoji: string;
  name: string;
  type: 'Engineer' | 'Team';
  status: 'Published' | 'Draft' | 'Rejected';
  version: string;
  installs: string;
  updated: string;
  action: string;
}

export interface FaqEntry {
  question: string;
  answer: string;
}
