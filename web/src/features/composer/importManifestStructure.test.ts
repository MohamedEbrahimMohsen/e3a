import { describe, expect, it } from 'vitest';
import type { ImportManifest } from '../../lib/workspaceApi';
import { toStructurePaths } from './importManifestStructure';

function manifestWith(overrides: Partial<ImportManifest>): ImportManifest {
  return { imported: [], converted: [], skipped: [], strippedPaths: [], hookWarnings: [], claudeMdSnippet: null, uploadedAt: '2026-08-29T00:00:00Z', ...overrides };
}

describe('toStructurePaths', () => {
  it('should list imported and converted target paths sorted', () => {
    const manifest = manifestWith({
      imported: [
        { sourcePath: 'skills/b/SKILL.md', targetPath: 'skills/b/SKILL.md', category: 'Skill' },
        { sourcePath: 'agents/a.md', targetPath: 'agents/a.md', category: 'Agent' },
      ],
      converted: [{ sourcePath: 'CLAUDE.md', targetPath: 'skills/house-rules/SKILL.md', reason: 'Converted' }],
    });

    expect(toStructurePaths(manifest)).toEqual(['agents/a.md', 'skills/b/SKILL.md', 'skills/house-rules/SKILL.md']);
  });

  it('should de-duplicate repeated target paths', () => {
    const manifest = manifestWith({
      imported: [{ sourcePath: 'agents/a.md', targetPath: 'agents/a.md', category: 'Agent' }],
      converted: [{ sourcePath: 'other.md', targetPath: 'agents/a.md', reason: 'Converted' }],
    });

    expect(toStructurePaths(manifest)).toEqual(['agents/a.md']);
  });

  it('should return an empty array for an empty manifest', () => {
    expect(toStructurePaths(manifestWith({}))).toEqual([]);
  });

  it('should ignore skipped and stripped entries', () => {
    const manifest = manifestWith({
      skipped: [{ sourcePath: 'settings.local.json', reason: 'Local file' }],
      strippedPaths: ['.claude/settings.local.json'],
    });

    expect(toStructurePaths(manifest)).toEqual([]);
  });
});
