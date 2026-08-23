export function copyToClipboard(text: string): void {
  try {
    void navigator.clipboard.writeText(text);
  } catch {
    // Clipboard access can be denied in insecure contexts; the copied-state feedback still shows.
  }
}
