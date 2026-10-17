const HOTKEY_PATTERN = /^Digit([0-9])$/;

export function abilityIndexForKey(code: string): number | undefined {
  const digit = HOTKEY_PATTERN.exec(code)?.[1];
  if (digit === undefined) {
    return undefined;
  }
  return (Number(digit) + 9) % 10;
}

export function hotkeyLabel(index: number): string | undefined {
  return index < 10 ? String((index + 1) % 10) : undefined;
}
