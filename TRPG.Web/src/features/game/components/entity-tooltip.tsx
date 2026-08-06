import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { getSessionsBySessionIdNamedEntitiesByEntityIdOptions } from '@/api/client';
import type { EntityType } from '@/api/client';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTrigger,
} from '@/components/ui/hover-popover';

export const ENTITY_TYPE_COLORS: Record<EntityType, string> = {
  Creature: '#E8A33D',
  Building: '#C9A66B',
  District: '#6BBF59',
  World: '#4DD0C4',
  Country: '#5B9BD9',
  State: '#A67BD9',
  City: '#D97BB0',
};

interface EntityTooltipProps {
  sessionId: string;
  id: string;
  name: string;
  entityType: EntityType;
  side?: 'top' | 'right' | 'bottom' | 'left';
  forceClosed?: boolean;
  children: React.ReactNode;
}

export function EntityTooltip({
  sessionId,
  id,
  name,
  entityType,
  side,
  forceClosed = false,
  children,
}: EntityTooltipProps) {
  const [open, setOpen] = useState(false);
  const isOpen = open && !forceClosed;
  const query = useQuery({
    ...getSessionsBySessionIdNamedEntitiesByEntityIdOptions({
      path: { sessionId, entityId: id },
    }),
    enabled: isOpen,
    staleTime: Infinity,
  });

  return (
    <HoverPopover open={isOpen} onOpenChange={setOpen}>
      <HoverPopoverTrigger asChild>{children}</HoverPopoverTrigger>
      <HoverPopoverContent
        side={side}
        className="flex flex-col items-start gap-1 text-left whitespace-normal"
      >
        <span className="font-bold" style={{ color: ENTITY_TYPE_COLORS[entityType] }}>
          {name}
        </span>
        {query.data ? (
          <>
            <span className="text-background/70 text-[10px]">
              {entityType}
              {query.data.subtype ? ` · ${query.data.subtype}` : ''}
            </span>
            {query.data.description && <span>{query.data.description}</span>}
          </>
        ) : (
          <span className="text-background/70 text-[10px] italic">Loading…</span>
        )}
      </HoverPopoverContent>
    </HoverPopover>
  );
}
