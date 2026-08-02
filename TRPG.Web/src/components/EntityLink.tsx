import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { getSessionsBySessionIdNamedEntitiesByEntityIdOptions } from '@/api/client';
import type { EntityType } from '@/api/client';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

const ENTITY_TYPE_COLORS: Record<EntityType, string> = {
  Creature: '#E8A33D',
  Building: '#C9A66B',
  District: '#6BBF59',
  World: '#4DD0C4',
  Country: '#5B9BD9',
  State: '#A67BD9',
  City: '#D97BB0',
};

interface EntityLinkProps {
  sessionId: string;
  id: string;
  name: string;
  entityType: EntityType;
}

export function EntityLink({ sessionId, id, name, entityType }: EntityLinkProps) {
  const [open, setOpen] = useState(false);
  const query = useQuery({
    ...getSessionsBySessionIdNamedEntitiesByEntityIdOptions({
      path: { sessionId, entityId: id },
    }),
    enabled: open,
    staleTime: Infinity,
  });

  return (
    <Tooltip onOpenChange={setOpen}>
      <TooltipTrigger asChild>
        <span
          className="cursor-help font-bold whitespace-nowrap"
          style={{ color: ENTITY_TYPE_COLORS[entityType] }}
        >
          [{name}]
        </span>
      </TooltipTrigger>
      <TooltipContent className="flex-col items-start gap-1 text-left whitespace-normal">
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
      </TooltipContent>
    </Tooltip>
  );
}
