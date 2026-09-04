export function toSlug(displayName: string): string {
  let slug = '';
  for (const character of displayName) {
    if (/[a-z0-9]/i.test(character)) {
      slug += character.toLowerCase();
    } else if (slug.length > 0 && !slug.endsWith('-')) {
      slug += '-';
    }
  }
  return slug.replace(/-+$/, '');
}
