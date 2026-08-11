import { useEffect, useState } from 'react';

import type { ItemDetail } from '@/api/client';

type Direction = 'toOther' | 'toPlayer';

interface PendingMove {
  itemId: string;
  quantity: number;
  direction: Direction;
}

interface MoveSelection {
  sourceItems: readonly ItemDetail[];
  destinationItems: readonly ItemDetail[];
  selected: ReadonlyMap<string, number>;
  direction: Direction;
}

interface MoveSelectionResult {
  sourceItems: ItemDetail[];
  destinationItems: ItemDetail[];
  moves: PendingMove[];
}

export function useTransferDraft(
  initialPlayerItems: readonly ItemDetail[] | undefined,
  initialOtherItems: readonly ItemDetail[] | undefined,
) {
  const [playerItems, setPlayerItems] = useStagedItems(initialPlayerItems);
  const [otherItems, setOtherItems] = useStagedItems(initialOtherItems);
  const [playerSelection, setPlayerSelection] = useState(new Map<string, number>());
  const [otherSelection, setOtherSelection] = useState(new Map<string, number>());
  const [pendingMoves, setPendingMoves] = useState<PendingMove[]>([]);

  const moveToPlayer = () => {
    if (playerItems === null || otherItems === null) {
      return;
    }
    const moved = moveSelected({
      sourceItems: otherItems,
      destinationItems: playerItems,
      selected: otherSelection,
      direction: 'toPlayer',
    });
    setOtherItems(moved.sourceItems);
    setPlayerItems(moved.destinationItems);
    setOtherSelection(new Map());
    setPendingMoves((current) => [...current, ...moved.moves]);
  };

  const moveToOther = () => {
    if (playerItems === null || otherItems === null) {
      return;
    }
    const moved = moveSelected({
      sourceItems: playerItems,
      destinationItems: otherItems,
      selected: playerSelection,
      direction: 'toOther',
    });
    setPlayerItems(moved.sourceItems);
    setOtherItems(moved.destinationItems);
    setPlayerSelection(new Map());
    setPendingMoves((current) => [...current, ...moved.moves]);
  };

  const playerToOther = netMoves(pendingMoves, 'toOther');
  const otherToPlayer = netMoves(pendingMoves, 'toPlayer');

  return {
    playerItems,
    otherItems,
    playerSelection,
    setPlayerSelection,
    otherSelection,
    setOtherSelection,
    moveToPlayer,
    moveToOther,
    playerToOther,
    otherToPlayer,
    changedCount: playerToOther.size + otherToPlayer.size,
  };
}

function useStagedItems(initialItems: readonly ItemDetail[] | undefined) {
  const [items, setItems] = useState<ItemDetail[] | null>(null);

  useEffect(() => {
    if (items === null && initialItems !== undefined) {
      setItems([...initialItems]);
    }
  }, [initialItems, items]);

  return [items, setItems] as const;
}

function moveSelected({
  sourceItems,
  destinationItems,
  selected,
  direction,
}: MoveSelection): MoveSelectionResult {
  const moves: PendingMove[] = [];
  const nextSource = [...sourceItems];
  const nextDestination = [...destinationItems];

  for (const [itemId, selectedQuantity] of selected) {
    const sourceIndex = nextSource.findIndex((item) => item.itemId === itemId);
    if (sourceIndex < 0) {
      continue;
    }
    const item = nextSource[sourceIndex];
    const amount = Math.min(selectedQuantity, Number(item.quantity));
    if (amount <= 0) {
      continue;
    }

    moves.push({ itemId, quantity: amount, direction });
    if (amount === Number(item.quantity)) {
      nextSource.splice(sourceIndex, 1);
    } else {
      nextSource[sourceIndex] = { ...item, quantity: Number(item.quantity) - amount };
    }
    addToInventory(nextDestination, { ...item, quantity: amount, equippedSlot: null });
  }

  return { sourceItems: nextSource, destinationItems: nextDestination, moves };
}

function addToInventory(items: ItemDetail[], item: ItemDetail) {
  const existingIndex = item.isStackable
    ? items.findIndex((existing) => existing.itemId === item.itemId)
    : -1;
  if (existingIndex < 0) {
    items.push(item);
    return;
  }

  const existing = items[existingIndex];
  items[existingIndex] = {
    ...existing,
    quantity: Number(existing.quantity) + Number(item.quantity),
  };
}

function netMoves(moves: readonly PendingMove[], direction: Direction): Map<string, number> {
  const quantities = new Map<string, number>();
  for (const move of moves) {
    const multiplier = move.direction === direction ? 1 : -1;
    quantities.set(move.itemId, (quantities.get(move.itemId) ?? 0) + move.quantity * multiplier);
  }
  return new Map([...quantities].filter(([, quantity]) => quantity > 0));
}
