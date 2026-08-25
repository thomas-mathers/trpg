import type { ItemDetail } from '@/api/client';

export function sumItemWeight(items: readonly ItemDetail[]): number {
  return items.reduce((sum, item) => sum + Number(item.weight) * Number(item.quantity), 0);
}

export function sumSelectedWeight(
  items: readonly ItemDetail[],
  selection: ReadonlyMap<string, number>,
): number {
  let sum = 0;
  for (const [itemId, quantity] of selection) {
    const item = items.find((candidate) => candidate.itemId === itemId);
    if (item) {
      sum += Number(item.weight) * quantity;
    }
  }
  return sum;
}
