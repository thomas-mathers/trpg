export const THREAT_LEVEL_GAP = 5;

export function isDangerous(level: number, playerLevel: number): boolean {
  return level >= playerLevel + THREAT_LEVEL_GAP;
}
