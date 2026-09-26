import { HttpResponse } from 'msw';

import {
  handleBeginCaravanInteraction,
  handleBeginCreatureInteraction,
  handleEndCaravanInteraction,
  handleEndCreatureInteraction,
} from '@/api/client/msw.gen';

import { server } from './server';

interface InteractionCalls {
  calls: string[];
}

// Records begin/end requests in order as `begin:creature:<id>`, `end:caravan:<id>`, and so on.
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
    handleBeginCaravanInteraction(({ params }) => {
      calls.push(`begin:caravan:${params.caravanId}`);
      return refuseBegin ? refused() : noContent();
    }),
    handleEndCaravanInteraction(({ params }) => {
      calls.push(`end:caravan:${params.caravanId}`);
      return noContent();
    }),
  );

  return { calls };
}
