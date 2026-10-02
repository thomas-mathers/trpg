import type { IStreamResult, IStreamSubscriber } from '@microsoft/signalr';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { toastReply } from './reply-toast';

const toastCustom = vi.hoisted(() => vi.fn());
vi.mock('sonner', () => ({ toast: { custom: toastCustom, dismiss: vi.fn() } }));

function streamOf(...tokens: string[]): IStreamResult<string> {
  return {
    subscribe(subscriber: IStreamSubscriber<string>) {
      tokens.forEach((token) => subscriber.next(token));
      subscriber.complete();
      return { dispose: vi.fn() };
    },
  };
}

const drain = (stream: IStreamResult<string>) =>
  stream.subscribe({ next: vi.fn(), error: vi.fn(), complete: vi.fn() });

describe('toastReply', () => {
  beforeEach(() => toastCustom.mockClear());

  it('toasts the streamed text once the stream completes', () => {
    drain(toastReply(streamOf('The door is ', 'locked.')));

    expect(toastCustom).toHaveBeenCalledTimes(1);
  });

  it('stays silent when the stream carries no text', () => {
    drain(toastReply(streamOf()));

    expect(toastCustom).not.toHaveBeenCalled();
  });
});
