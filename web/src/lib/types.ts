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

export interface CrewMember {
  emoji: string;
  name: string;
  pinnedVersion: string;
}

export interface FaqEntry {
  question: string;
  answer: string;
}
