import { EntityLink } from '@/components/EntityLink';
import { parseNarration } from '@/lib/narration-markup';

interface NarrationTextProps {
  sessionId: string;
  content: string;
}

export function NarrationText({ sessionId, content }: NarrationTextProps) {
  const segments = parseNarration(content);

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
