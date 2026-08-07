import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import type { ConsumableSummary } from '@/api/client';
import {
  getCreatureConsumablesOptions,
  getCreatureConsumablesQueryKey,
} from '@/api/client';
import { SearchInput } from '@/components/search-input';
import { EmptyNote } from '@/features/combat/components/empty-note';
import { PickerHeader } from '@/features/combat/components/picker-header';
import { gameEventBus } from '@/lib/game-event-bus';

const SEARCH_THRESHOLD = 6;
const RESOURCE_LABEL: Record<string, string> = { Hp: 'HP', Ap: 'AP', Mp: 'MP' };

interface ItemPickerProps {
  playerId: string;
  onBack: () => void;
  onChoose: (item: ConsumableSummary) => void;
}

export function ItemPicker({ playerId, onBack, onChoose }: ItemPickerProps) {
  const [query, setQuery] = useState('');
  const queryClient = useQueryClient();

  const itemsQuery = useQuery(
    getCreatureConsumablesOptions({ path: { creatureId: playerId } }),
  );

  useEffect(() => {
    const key = getCreatureConsumablesQueryKey({ path: { creatureId: playerId } });
    const invalidate = () => queryClient.invalidateQueries({ queryKey: key });
    const offUpdated = gameEventBus.on('CombatUpdated', invalidate);
    const offStarted = gameEventBus.on('CombatStarted', invalidate);
    return () => {
      offUpdated();
      offStarted();
    };
  }, [playerId, queryClient]);

  const items = itemsQuery.data ?? [];
  const normalizedQuery = query.trim().toLowerCase();
  const filtered = normalizedQuery
    ? items.filter((i) => i.name.toLowerCase().includes(normalizedQuery))
    : items;

  return (
    <div className="p-2.5">
      <PickerHeader onBack={onBack} title="Choose an item" />

      {items.length > SEARCH_THRESHOLD && (
        <SearchInput value={query} onChange={setQuery} placeholder="Search items…" />
      )}

      {items.length === 0 ? (
        <EmptyNote>No usable items.</EmptyNote>
      ) : filtered.length === 0 ? (
        <EmptyNote>No matches for &ldquo;{query.trim()}&rdquo;.</EmptyNote>
      ) : (
        <div className="flex flex-col gap-1.5">
          {filtered.map((item) => (
            <button
              key={item.itemId}
              type="button"
              onClick={() => onChoose(item)}
              className="border-border bg-card hover:bg-accent flex items-center justify-between gap-2.5 rounded-md border px-2.5 py-2 text-left shadow-sm"
            >
              <span className="min-w-0">
                <span className="block text-[13px] font-medium">{item.name}</span>
                <span className="text-muted-foreground block text-[11px]">
                  +{item.restoreAmount} {RESOURCE_LABEL[item.resource] ?? item.resource}
                </span>
              </span>
              <span className="text-muted-foreground shrink-0 text-[11px] tabular-nums">
                ×{item.quantity}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
