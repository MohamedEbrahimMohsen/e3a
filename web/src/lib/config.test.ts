import { describe, expect, it } from 'vitest';
import { config, installCommand, marketplaceAddCommand, pinnedMarketplaceCommand } from './config';

describe('installCommand', () => {
  it('should emit the engineer form by default', () => {
    expect(installCommand('payments-engineer')).toBe('/plugin install e3a-payments-engineer@e3a');
  });

  it('should emit the team form for a team', () => {
    expect(installCommand('full-stack-squad', 'Team')).toBe('/plugin install e3a-team-full-stack-squad@e3a');
  });
});

describe('config', () => {
  it('should fall back to the production site url when VITE_SITE_URL is unset', () => {
    expect(config.siteUrl).toBe('https://e3a.ai');
  });
});

describe('marketplaceAddCommand', () => {
  it('should emit the marketplace add command for the production domain', () => {
    expect(marketplaceAddCommand()).toBe('/plugin marketplace add https://e3a.ai/marketplace.json');
  });
});

describe('pinnedMarketplaceCommand', () => {
  it('should pin a version on the production host', () => {
    expect(pinnedMarketplaceCommand('e3a-payments-engineer', '1.2.3')).toBe('/plugin marketplace add e3a.ai/m/e3a-payments-engineer/1.2.3/marketplace.json');
  });
});
