import type { EntityType } from '@/api/client';

export interface TextSegment {
  type: 'text';
  text: string;
}

export interface EntitySegment {
  type: 'entity';
  id: string;
  name: string;
  entityType: EntityType;
}

export type NarrationSegment = TextSegment | EntitySegment;

const ENTITY_MARKUP_PATTERN = /\[([^\]]+)\]\(entity:\/\/(\w+)\/([^)]+)\)/g;

export function parseNarration(content: string): NarrationSegment[] {
  const segments: NarrationSegment[] = [];
  let lastIndex = 0;

  for (const match of content.matchAll(ENTITY_MARKUP_PATTERN)) {
    const [fullMatch, name, entityType, id] = match;
    const index = match.index;

    if (index > lastIndex) {
      segments.push({ type: 'text', text: content.slice(lastIndex, index) });
    }

    segments.push({ type: 'entity', id, name, entityType: entityType as EntityType });
    lastIndex = index + fullMatch.length;
  }

  if (lastIndex < content.length) {
    segments.push({ type: 'text', text: content.slice(lastIndex) });
  }

  return segments;
}
