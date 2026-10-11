import { HttpResponse } from 'msw';

import { handleBeginCreatureInteraction, handleEndCreatureInteraction } from '@/api/client/msw.gen';

import { server } from './server';

interface InteractionCalls {
  calls: string[];
}

// Records begin/end requests in order as `begin:creature:<id>`, `end:creature:<id>`, and so on.
export function recordInteractions({
  refuseBegin = false,
}: { refuseBegin?: boolean } = {}): InteractionCalls {
  const calls: string[] = [];
  const noContent = () => new HttpResponse(null, { status: 204 });
  const refused = () => new HttpResponse(null, { status: 400 });

  server.use(
    handleBeginCreatureInteraction(({ params }) => {
      calls.push(`begin:creature:${params.creatureId}`);
      return refuseBegin ? refused() : noContent();
    }),
    handleEndCreatureInteraction(({ params }) => {
      calls.push(`end:creature:${params.creatureId}`);
      return noContent();
    }),
  );

  return { calls };
}
