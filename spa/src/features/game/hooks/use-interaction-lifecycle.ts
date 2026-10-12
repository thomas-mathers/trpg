import { useCallback, useEffect, useRef } from 'react';

import { beginCreatureInteraction, endCreatureInteraction } from '@/api/client';

interface InteractionTarget {
  key: string;
  begin: () => Promise<unknown>;
  end: () => Promise<unknown>;
}

interface InteractionLifecycle {
  release: () => Promise<void>;
  // Passes the engagement on to whatever the caller starts next; returns the way to end it if that fails.
  handOff: () => () => Promise<void>;
}

// Steps run strictly in order, so a quick close or a StrictMode remount can never release before
// the begin has finished or begin again before the release has.
function useInteractionLifecycle(target: InteractionTarget | undefined): InteractionLifecycle {
  const queue = useRef<Promise<void>>(Promise.resolve());
  const releaseCurrent = useRef<() => Promise<void>>(() => Promise.resolve());
  const handOffCurrent = useRef<() => () => Promise<void>>(() => () => Promise.resolve());
  const latestTarget = useRef(target);
  const targetKey = target?.key;

  useEffect(() => {
    latestTarget.current = target;
  });

  useEffect(() => {
    const target = latestTarget.current;
    if (!target) return;

    let began = false;
    let handedOff = false;
    const step = (action: () => Promise<void>) => {
      queue.current = queue.current.then(action);
      return queue.current;
    };
    const endNow = () =>
      step(async () => {
        if (!began) return;
        began = false;
        await target.end().catch(() => {});
      });

    // A refused begin (for example the creature is already in a conversation) leaves the panel
    // usable and skips the release, so an interaction this panel never started is not ended.
    // Deferring the begin lets a StrictMode remount or an instant close cancel it before any request.
    const beginTimer = setTimeout(() => {
      void step(async () => {
        began = await target.begin().then(
          () => true,
          () => false,
        );
      });
    }, 0);
    const release = () => {
      clearTimeout(beginTimer);
      return handedOff ? Promise.resolve() : endNow();
    };
    const handOff = () => {
      clearTimeout(beginTimer);
      handedOff = true;
      return endNow;
    };

    releaseCurrent.current = release;
    handOffCurrent.current = handOff;
    return () => {
      releaseCurrent.current = () => Promise.resolve();
      handOffCurrent.current = () => () => Promise.resolve();
      void release();
    };
  }, [targetKey]);

  const release = useCallback(() => releaseCurrent.current(), []);
  const handOff = useCallback(() => handOffCurrent.current(), []);

  return { release, handOff };
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
