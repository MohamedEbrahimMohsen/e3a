import { describe, expect, it } from 'vitest';
import { validateUploadFile } from './uploadFileValidation';

describe('validateUploadFile', () => {
  it('should accept a zip within the limit', () => {
    expect(validateUploadFile({ name: 'claude.zip', size: 1024 }, 20)).toBeNull();
  });

  it('should reject a file that is not a zip', () => {
    expect(validateUploadFile({ name: 'claude.tar.gz', size: 10 }, 20)).toContain('.zip');
  });

  it('should reject a zip over the limit', () => {
    expect(validateUploadFile({ name: 'a.zip', size: 21 * 1024 * 1024 }, 20)).toContain('20');
  });

  it('should accept an uppercase extension', () => {
    expect(validateUploadFile({ name: 'A.ZIP', size: 10 }, 20)).toBeNull();
  });
});
