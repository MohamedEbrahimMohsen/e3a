import { describe, expect, it } from 'vitest';
import { toSlug } from './slug';

describe('toSlug', () => {
  it('should lowercase and hyphenate a display name', () => {
    expect(toSlug('Payments Engineer')).toBe('payments-engineer');
  });

  it('should collapse runs of separators', () => {
    expect(toSlug('A -- B__C')).toBe('a-b-c');
  });

  it('should trim leading and trailing separators', () => {
    expect(toSlug('  Hello!  ')).toBe('hello');
  });

  it('should return an empty string for input with no ascii alphanumerics', () => {
    expect(toSlug('…—')).toBe('');
  });
});
