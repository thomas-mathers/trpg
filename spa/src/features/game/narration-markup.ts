import type { EntityType } from '@/api/client';

export interface TextSegment {
  type: 'text';
  text: string;
  insideQuote: boolean;
}

export interface EntitySegment {
  type: 'entity';
  id: string;
  name: string;
  entityType: EntityType;
  insideQuote: boolean;
}

export type NarrationSegment = TextSegment | EntitySegment;

const ENTITY_MARKUP_PATTERN = /^\[([^\]]+)\]\(entity:\/\/(\w+)\/([^)]+)\)$/;
const ENTITY_MARKUP_TOKEN_PATTERN = /(\[[^\]]+\]\(entity:\/\/\w+\/[^)]+\))/;

function appendText(
  segments: NarrationSegment[],
  text: string,
  insideQuote: boolean,
): NarrationSegment[] {
  if (text === '') {
    return segments;
  }

  const last = segments[segments.length - 1];

  if (last?.type === 'text') {
    return [...segments.slice(0, -1), { type: 'text', text: last.text + text, insideQuote }];
  }

  return [...segments, { type: 'text', text, insideQuote }];
}

export function appendTokenToNarrationSegments(
  segments: NarrationSegment[],
  token: string,
): NarrationSegment[] {
  const insideQuote = segments[segments.length - 1]?.insideQuote ?? false;

  const match = token.match(ENTITY_MARKUP_PATTERN);
  if (match) {
    const [, name, entityType, id] = match;
    return [
      ...segments,
      { type: 'entity', id, name, entityType: entityType as EntityType, insideQuote },
    ];
  }

  const parts = token.split('"');

  let result = segments;
  let quoted = insideQuote;

  for (let i = 0; i < parts.length; i++) {
    const isLastPart = i === parts.length - 1;
    const text = !isLastPart && quoted ? parts[i] + '”' : parts[i];

    result = appendText(result, text, quoted);

    if (!isLastPart) {
      if (!quoted) {
        parts[i + 1] = '“' + parts[i + 1];
      }
      quoted = !quoted;
    }
  }

  return result;
}

export function parseNarrationMarkup(markup: string): NarrationSegment[] {
  return markup
    .split(ENTITY_MARKUP_TOKEN_PATTERN)
    .reduce<NarrationSegment[]>(
      (segments, token) => appendTokenToNarrationSegments(segments, token),
      [],
    );
}
