import type { EntityType } from '@/api/client';
import { ENTITY_TYPE_COLORS } from '@/features/game/entity-type-colors';
import type { NarrationSegment } from '@/features/game/narration-markup';

import { EntityTooltip } from './entity-tooltip';

interface EntityLinkProps {
  id: string;
  name: string;
  entityType: EntityType;
}

export function EntityLink({ id, name, entityType }: EntityLinkProps) {
  return (
    <EntityTooltip id={id} name={name} entityType={entityType}>
      <span
        className="cursor-help font-bold whitespace-nowrap not-italic"
        style={{ color: ENTITY_TYPE_COLORS[entityType] }}
      >
        [{name}]
      </span>
    </EntityTooltip>
  );
}

export function NarrationText({ segments }: { segments: NarrationSegment[] }) {
  return (
    <>
      {segments.map((segment, index) =>
        segment.type === 'entity' ? (
          <EntityLink
            key={index}
            id={segment.id}
            name={segment.name}
            entityType={segment.entityType}
          />
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </>
  );
}
