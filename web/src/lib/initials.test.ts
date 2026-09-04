import { describe, expect, it } from 'vitest';
import { initialsFor } from './initials';

describe('initialsFor', () => {
  it('should take the first letter of the first two words', () => {
    expect(initialsFor('Mohamed Mohsen')).toBe('MM');
  });

  it('should handle a single-word name', () => {
    expect(initialsFor('mohamed-dive')).toBe('MD');
  });

  it('should return an empty string for an empty name', () => {
    expect(initialsFor('')).toBe('');
  });
});
