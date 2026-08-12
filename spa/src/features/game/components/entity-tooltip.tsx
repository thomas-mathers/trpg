import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { getSessionItemOptions, getSessionLoreAnchorOptions } from '@/api/client';
import type { EntityType } from '@/api/client';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTrigger,
} from '@/components/ui/hover-popover';
import { useSessionId } from '@/features/game/contexts/scene-context';
import { ItemTooltip } from '@/features/inventory/components/item-tooltip';

import { ENTITY_TYPE_COLORS } from '../entity-type-colors';

interface EntityTooltipProps {
  id: string;
  name: string;
  entityType: EntityType;
  side?: 'top' | 'right' | 'bottom' | 'left';
  forceClosed?: boolean;
  children: React.ReactNode;
}

export function EntityTooltip({
  id,
  name,
  entityType,
  side,
  forceClosed = false,
  children,
}: EntityTooltipProps) {
  const sessionId = useSessionId();
  const [open, setOpen] = useState(false);
  const isOpen = open && !forceClosed;
  const loreAnchor = useQuery({
    ...getSessionLoreAnchorOptions({ path: { sessionId, anchorId: id } }),
    enabled: isOpen && entityType !== 'Item',
    staleTime: Infinity,
  });
  const item = useQuery({
    ...getSessionItemOptions({ path: { sessionId, itemId: id } }),
    enabled: isOpen && entityType === 'Item',
    staleTime: Infinity,
  });

  return (
    <HoverPopover open={isOpen} onOpenChange={setOpen}>
      <HoverPopoverTrigger asChild>{children}</HoverPopoverTrigger>
      <HoverPopoverContent
        side={side}
        className="flex flex-col items-start gap-1 text-left whitespace-normal"
      >
        {item.data ? (
          <ItemTooltip item={item.data} />
        ) : loreAnchor.data ? (
          <>
            <span className="font-bold" style={{ color: ENTITY_TYPE_COLORS[entityType] }}>
              {name}
            </span>
            <span className="text-background/70 text-[10px]">
              {entityType}
              {loreAnchor.data.subtype ? ` · ${loreAnchor.data.subtype}` : ''}
            </span>
            {loreAnchor.data.description && <span>{loreAnchor.data.description}</span>}
          </>
        ) : (
          <span className="text-background/70 text-[10px] italic">Loading…</span>
        )}
      </HoverPopoverContent>
    </HoverPopover>
  );
}
