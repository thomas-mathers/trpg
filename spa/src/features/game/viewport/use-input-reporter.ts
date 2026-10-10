import { useCallback, useEffect, useRef } from 'react';

export const INPUT_HEARTBEAT_INTERVAL_MS = 250;
const HEADING_EPSILON = 0.01;

export interface InputFrame {
  forward: number;
  strafe: number;
  heading: number;
  x: number;
  y: number;
}

interface SentInput {
  locationId: string;
  frame: InputFrame;
  at: number;
}

const axesChanged = (a: InputFrame, b: InputFrame) =>
  a.forward !== b.forward || a.strafe !== b.strafe;

const isMoving = ({ forward, strafe }: InputFrame) => forward !== 0 || strafe !== 0;

export function useInputReporter(locationId: string, send: (frame: InputFrame) => void) {
  const sendRef = useRef(send);
  const lastSent = useRef<SentInput>(undefined);
  const lastFrame = useRef<InputFrame>(undefined);

  useEffect(() => {
    sendRef.current = send;
  }, [send]);

  const track = useCallback(
    (frame: InputFrame, nowMs: number) => {
      lastFrame.current = frame;
      const last = lastSent.current;
      if (last?.locationId !== locationId) {
        // The server assumes a standing player until told otherwise.
        lastSent.current = { locationId, frame: { ...frame, forward: 0, strafe: 0 }, at: nowMs };
        if (!isMoving(frame)) return;
      } else {
        const elapsed = nowMs - last.at;
        const due =
          axesChanged(last.frame, frame) ||
          (elapsed >= INPUT_HEARTBEAT_INTERVAL_MS &&
            (isMoving(frame) || Math.abs(frame.heading - last.frame.heading) > HEADING_EPSILON));
        if (!due) return;
      }

      lastSent.current = { locationId, frame, at: nowMs };
      sendRef.current(frame);
    },
    [locationId],
  );

  const halt = useCallback(() => {
    const last = lastSent.current;
    const frame = lastFrame.current;
    if (last?.locationId !== locationId || !frame || !isMoving(last.frame)) return;

    const stopped = { ...frame, forward: 0, strafe: 0 };
    lastSent.current = { locationId, frame: stopped, at: last.at };
    sendRef.current(stopped);
  }, [locationId]);

  return { track, halt };
}
