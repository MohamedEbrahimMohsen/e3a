const MAX_INITIALS = 2;

export function initialsFor(name: string): string {
  return name
    .split(/[^a-z0-9]+/i)
    .filter(word => word.length > 0)
    .map(word => word[0].toUpperCase())
    .join('')
    .slice(0, MAX_INITIALS);
}
