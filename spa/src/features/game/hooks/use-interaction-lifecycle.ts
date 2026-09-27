import { useCallback, useEffect, useRef } from 'react';

import {
  beginCaravanInteraction,
  beginCreatureInteraction,
  endCaravanInteraction,
  endCreatureInteraction,
} from '@/api/client';

interface InteractionTarget {
  key: string;
  begin: () => Promise<unknown>;
  end: () => Promise<unknown>;
}

interface InteractionLifecycle {
  release: () => Promise<void>;
}

// Steps run strictly in order, so a quick close or a StrictMode remount can never release before
// the begin has finished or begin again before the release has.
function useInteractionLifecycle(target: InteractionTarget | undefined): InteractionLifecycle {
  const queue = useRef<Promise<void>>(Promise.resolve());
  const releaseCurrent = useRef<() => Promise<void>>(() => Promise.resolve());
  const latestTarget = useRef(target);
  const targetKey = target?.key;

  useEffect(() => {
    latestTarget.current = target;
  });

  useEffect(() => {
    const target = latestTarget.current;
    if (!target) return;

    let began = false;
    const step = (action: () => Promise<void>) => {
      queue.current = queue.current.then(action);
      return queue.current;
    };

    // A refused begin (for example the creature is already in a conversation) leaves the panel
    // usable and skips the release, so an interaction this panel never started is not ended.
    void step(async () => {
      began = await target.begin().then(
        () => true,
        () => false,
      );
    });
    const release = () =>
      step(async () => {
        if (!began) return;
        began = false;
        await target.end().catch(() => {});
      });

    releaseCurrent.current = release;
    return () => {
      releaseCurrent.current = () => Promise.resolve();
      void release();
    };
  }, [targetKey]);

  const release = useCallback(() => releaseCurrent.current(), []);

  return { release };
}

interface CreatureInteractionOptions {
  playerId: string;
  worldId: string;
  creatureId: string | undefined;
}

export function useCreatureInteraction({
  playerId,
  worldId,
  creatureId,
}: CreatureInteractionOptions): InteractionLifecycle {
  const request = creatureId && {
    path: { playerId, creatureId },
    query: { worldId },
    throwOnError: true as const,
  };

  return useInteractionLifecycle(
    request
      ? {
          key: `${worldId}:${playerId}:${creatureId}`,
          begin: () => beginCreatureInteraction(request),
          end: () => endCreatureInteraction(request),
        }
      : undefined,
  );
}

interface CaravanInteractionOptions {
  playerId: string;
  worldId: string;
  caravanId: string | undefined;
}

export function useCaravanInteraction({
  playerId,
  worldId,
  caravanId,
}: CaravanInteractionOptions): InteractionLifecycle {
  const request = caravanId && {
    path: { playerId, caravanId },
    query: { worldId },
    throwOnError: true as const,
  };

  return useInteractionLifecycle(
    request
      ? {
          key: `${worldId}:${playerId}:${caravanId}`,
          begin: () => beginCaravanInteraction(request),
          end: () => endCaravanInteraction(request),
        }
      : undefined,
  );
}
