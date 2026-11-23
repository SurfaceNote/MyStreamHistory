/**
 * Display name of a stream language code as Twitch reports it ("ru", "zh-hk", "other"), in the UI locale.
 * Returns null for "other" and for codes the runtime does not know, so the caller can pick its own wording.
 */
export function streamLanguageName(code: string, uiLocale: string): string | null {
  if (!code || code === 'other') return null;
  try {
    const name = new Intl.DisplayNames([uiLocale], { type: 'language', fallback: 'none' }).of(code);
    return name ? name.charAt(0).toLocaleUpperCase(uiLocale) + name.slice(1) : null;
  } catch {
    return null;
  }
}
