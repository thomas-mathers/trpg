import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { recordInteractions } from '@/test/interaction-handlers';

import { useCreatureInteraction } from './use-interaction-lifecycle';

const idle = () => new Promise((resolve) => setTimeout(resolve, 50));

describe('useCreatureInteraction', () => {
  it('does nothing while no creature is selected', async () => {
    const { calls } = recordInteractions();

    renderHook(() =>
      useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId: undefined }),
    );
    await idle();

    expect(calls).toEqual([]);
  });

  it('begins on selection and releases when the selection clears', async () => {
    const { calls } = recordInteractions();
    const { rerender } = renderHook(
      ({ creatureId }: { creatureId: string | undefined }) =>
        useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId }),
      { initialProps: { creatureId: 'creature-id' as string | undefined } },
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:creature-id']));

    rerender({ creatureId: undefined });

    await waitFor(() =>
      expect(calls).toEqual(['begin:creature:creature-id', 'end:creature:creature-id']),
    );
  });

  it('releases the previous creature before engaging the next one', async () => {
    const { calls } = recordInteractions();
    const { rerender } = renderHook(
      ({ creatureId }: { creatureId: string }) =>
        useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId }),
      { initialProps: { creatureId: 'first-id' } },
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:first-id']));

    rerender({ creatureId: 'second-id' });

    await waitFor(() =>
      expect(calls).toEqual([
        'begin:creature:first-id',
        'end:creature:first-id',
        'begin:creature:second-id',
      ]),
    );
  });

  it('sends one begin when reselected before the begin goes out', async () => {
    const { calls } = recordInteractions();
    const { rerender, unmount } = renderHook(
      ({ creatureId }: { creatureId: string | undefined }) =>
        useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId }),
      { initialProps: { creatureId: 'a' as string | undefined } },
    );

    rerender({ creatureId: undefined });
    rerender({ creatureId: 'a' });
    await waitFor(() => expect(calls).toEqual(['begin:creature:a']));
    unmount();

    await waitFor(() => expect(calls).toEqual(['begin:creature:a', 'end:creature:a']));
  });

  it('sends a single begin and no release under a StrictMode remount', async () => {
    const { calls } = recordInteractions();

    renderHook(
      () => useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId: 'a' }),
      { reactStrictMode: true },
    );
    await idle();

    expect(calls).toEqual(['begin:creature:a']);
  });

  it('releases immediately on request and does not release a second time on unmount', async () => {
    const { calls } = recordInteractions();
    const { result, unmount } = renderHook(() =>
      useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId: 'a' }),
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:a']));

    await result.current.release();
    unmount();
    await idle();

    expect(calls).toEqual(['begin:creature:a', 'end:creature:a']);
  });

  it('keeps the engagement through a hand-off when the selection clears', async () => {
    const { calls } = recordInteractions();
    const { result, rerender } = renderHook(
      ({ creatureId }: { creatureId: string | undefined }) =>
        useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId }),
      { initialProps: { creatureId: 'a' as string | undefined } },
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:a']));

    result.current.handOff();
    rerender({ creatureId: undefined });
    await idle();

    expect(calls).toEqual(['begin:creature:a']);
  });

  it('ends a handed-off engagement on request', async () => {
    const { calls } = recordInteractions();
    const { result, rerender } = renderHook(
      ({ creatureId }: { creatureId: string | undefined }) =>
        useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId }),
      { initialProps: { creatureId: 'a' as string | undefined } },
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:a']));
    const endHandedOff = result.current.handOff();
    rerender({ creatureId: undefined });

    await endHandedOff();

    expect(calls).toEqual(['begin:creature:a', 'end:creature:a']);
  });

  it('does not release an interaction the server refused to begin', async () => {
    const { calls } = recordInteractions({ refuseBegin: true });
    const { result, unmount } = renderHook(() =>
      useCreatureInteraction({ playerId: 'player-id', worldId: 'world-id', creatureId: 'a' }),
    );
    await waitFor(() => expect(calls).toEqual(['begin:creature:a']));

    await result.current.release();
    unmount();
    await idle();

    expect(calls).toEqual(['begin:creature:a']);
  });
});
