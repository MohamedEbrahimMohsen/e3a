const BYTES_PER_MEGABYTE = 1024 * 1024;

export function validateUploadFile(file: { name: string; size: number }, maxMegabytes: number): string | null {
  if (!file.name.toLowerCase().endsWith('.zip')) {
    return 'Only .zip archives are accepted.';
  }
  if (file.size > maxMegabytes * BYTES_PER_MEGABYTE) {
    return `That file is larger than the ${maxMegabytes} MB limit.`;
  }
  return null;
}
