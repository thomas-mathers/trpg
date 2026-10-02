import { act, renderHook, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Toaster } from '@/components/ui/sonner';
import { renderWithProviders } from '@/test/test-utils';

import { runAction, useAction } from './run-action';

describe('runAction', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.stubGlobal(
      'matchMedia',
      vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
    );
  });

  it('toasts the copy for the failure reason', async () => {
    renderWithProviders(<Toaster />);

    await runAction(Promise.resolve({ succeeded: false, reason: 'Locked' }));

    expect(await screen.findByText('The door is locked.')).toBeInTheDocument();
  });

  it('toasts a generic message when the call rejects', async () => {
    renderWithProviders(<Toaster />);

    const result = await runAction(Promise.reject(new Error('boom')));

    expect(result.succeeded).toBe(false);
    expect(await screen.findByText('Something went wrong.')).toBeInTheDocument();
  });

  it('stays silent when the action succeeds', async () => {
    renderWithProviders(<Toaster />);

    await runAction(Promise.resolve({ succeeded: true }));

    expect(screen.queryByText('Blocked')).not.toBeInTheDocument();
  });
});

describe('useAction', () => {
  it('is pending until the call settles', async () => {
    let finish: (result: ActionResult) => void = () => {};
    const call = new Promise<ActionResult>((resolve) => {
      finish = resolve;
    });
    const { result } = renderHook(() => useAction());

    act(() => void result.current.run(call));
    expect(result.current.pending).toBe(true);

    await act(async () => finish({ succeeded: true }));
    expect(result.current.pending).toBe(false);
  });
});
