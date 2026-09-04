import { describe, expect, it } from 'vitest';
import { installCommand } from './config';

describe('installCommand', () => {
  it('should emit the engineer form by default', () => {
    expect(installCommand('payments-engineer')).toBe('/plugin install e3a-payments-engineer@e3a');
  });

  it('should emit the team form for a team', () => {
    expect(installCommand('full-stack-squad', 'Team')).toBe('/plugin install e3a-team-full-stack-squad@e3a');
  });
});
