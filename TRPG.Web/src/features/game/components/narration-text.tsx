import { EntityLink } from '@/features/game/components/entity-link';
import type { NarrationSegment } from '@/features/game/narration-markup';

interface NarrationTextProps {
  sessionId: string;
  segments: NarrationSegment[];
}

export function NarrationText({ sessionId, segments }: NarrationTextProps) {
  return (
    <>
      {segments.map((segment, index) =>
        segment.type === 'entity' ? (
          <EntityLink
            key={index}
            sessionId={sessionId}
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
