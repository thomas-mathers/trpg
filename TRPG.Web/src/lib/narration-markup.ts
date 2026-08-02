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

const ENTITY_MARKUP_PATTERN = /^\[([^\]]+)\]\(entity:\/\/(\w+)\/([^)]+)\)$/;

export function appendNarrationToken(
  segments: NarrationSegment[],
  token: string,
): NarrationSegment[] {
  const match = token.match(ENTITY_MARKUP_PATTERN);
  if (match) {
    const [, name, entityType, id] = match;
    return [...segments, { type: 'entity', id, name, entityType: entityType as EntityType }];
  }

  // Merge into the previous segment if it's also text, instead of accumulating one segment per token.
  const last = segments[segments.length - 1];
  if (last?.type === 'text') {
    return [...segments.slice(0, -1), { type: 'text', text: last.text + token }];
  }

  return [...segments, { type: 'text', text: token }];
}
