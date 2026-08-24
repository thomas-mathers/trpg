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

export function NarrationText({
  segments,
  dropCap = false,
}: {
  segments: NarrationSegment[];
  dropCap?: boolean;
}) {
  const firstSegment = segments[0];
  const showDropCap = dropCap && firstSegment?.type === 'text' && firstSegment.text.length > 0;

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
        ) : showDropCap && index === 0 ? (
          <span key={index}>
            <span className="drop-cap">{segment.text.charAt(0)}</span>
            {segment.text.slice(1)}
          </span>
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </>
  );
}
