import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { POSE_REPORT_INTERVAL_MS, usePoseReporter } from './use-pose-reporter';

const pose = (x: number, y = 0, angle = 0) => ({ x, y, angle });

describe('usePoseReporter', () => {
  it('does not report the first pose, which only sets the baseline', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));

    result.current(pose(1), 0);

    expect(report).not.toHaveBeenCalled();
  });

  it('reports a changed pose once the interval has elapsed', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));
    result.current(pose(1), 0);

    result.current(pose(2), POSE_REPORT_INTERVAL_MS);

    expect(report).toHaveBeenCalledExactlyOnceWith(pose(2));
  });

  it('holds back a change that arrives before the interval has elapsed', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));
    result.current(pose(1), 0);

    result.current(pose(2), POSE_REPORT_INTERVAL_MS - 1);

    expect(report).not.toHaveBeenCalled();
  });

  it('sends the final pose on the first tick after the interval when movement stops', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));
    result.current(pose(1), 0);
    result.current(pose(2), 100);

    result.current(pose(2), POSE_REPORT_INTERVAL_MS);

    expect(report).toHaveBeenCalledExactlyOnceWith(pose(2));
  });

  it('does not repeat a pose that has not changed', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));
    result.current(pose(1), 0);
    result.current(pose(2), POSE_REPORT_INTERVAL_MS);

    result.current(pose(2), POSE_REPORT_INTERVAL_MS * 2);

    expect(report).toHaveBeenCalledOnce();
  });

  it('reports a turn on the spot', () => {
    const report = vi.fn();
    const { result } = renderHook(() => usePoseReporter('hall', report));
    result.current(pose(1), 0);

    result.current(pose(1, 0, 0.5), POSE_REPORT_INTERVAL_MS);

    expect(report).toHaveBeenCalledExactlyOnceWith(pose(1, 0, 0.5));
  });

  it('starts a fresh baseline when the location changes', () => {
    const report = vi.fn();
    const { result, rerender } = renderHook(({ id }) => usePoseReporter(id, report), {
      initialProps: { id: 'hall' },
    });
    result.current(pose(1), 0);
    rerender({ id: 'cellar' });

    result.current(pose(8), POSE_REPORT_INTERVAL_MS);

    expect(report).not.toHaveBeenCalled();
  });
});
