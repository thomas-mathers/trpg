import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { getSessionsBySessionIdNamedEntitiesByEntityIdOptions } from '@/api/client';
import type { EntityType } from '@/api/client';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { ENTITY_TYPE_COLORS } from '@/lib/entity-colors';

interface EntityTooltipProps {
  sessionId: string;
  id: string;
  name: string;
  entityType: EntityType;
  side?: 'top' | 'right' | 'bottom' | 'left';
  children: React.ReactNode;
}

export function EntityTooltip({
  sessionId,
  id,
  name,
  entityType,
  side,
  children,
}: EntityTooltipProps) {
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
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent
        side={side}
        className="flex-col items-start gap-1 text-left whitespace-normal"
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
      </TooltipContent>
    </Tooltip>
  );
}

interface EntityLinkProps {
  sessionId: string;
  id: string;
  name: string;
  entityType: EntityType;
}

export function EntityLink({ sessionId, id, name, entityType }: EntityLinkProps) {
  return (
    <EntityTooltip sessionId={sessionId} id={id} name={name} entityType={entityType}>
      <span
        className="cursor-help font-bold whitespace-nowrap not-italic"
        style={{ color: ENTITY_TYPE_COLORS[entityType] }}
      >
        [{name}]
      </span>
    </EntityTooltip>
  );
}
