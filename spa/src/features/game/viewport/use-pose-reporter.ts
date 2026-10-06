import { useCallback, useEffect, useRef } from 'react';

import type { PlacementWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export const POSE_REPORT_INTERVAL_MS = 250;
const POSITION_EPSILON = 0.01;
const ANGLE_EPSILON = 0.01;

interface ReportedPose {
  locationId: string;
  pose: PlacementWire;
  at: number;
}

const differs = (a: PlacementWire, b: PlacementWire) =>
  Math.abs(a.x - b.x) > POSITION_EPSILON ||
  Math.abs(a.y - b.y) > POSITION_EPSILON ||
  Math.abs(a.angle - b.angle) > ANGLE_EPSILON;

export function usePoseReporter(locationId: string, report: (pose: PlacementWire) => void) {
  const reportRef = useRef(report);
  const lastReported = useRef<ReportedPose>(undefined);

  useEffect(() => {
    reportRef.current = report;
  }, [report]);

  return useCallback(
    (pose: PlacementWire, nowMs: number) => {
      const last = lastReported.current;
      if (last?.locationId !== locationId) {
        lastReported.current = { locationId, pose, at: nowMs };
        return;
      }
      if (nowMs - last.at < POSE_REPORT_INTERVAL_MS || !differs(last.pose, pose)) {
        return;
      }

      lastReported.current = { locationId, pose, at: nowMs };
      reportRef.current(pose);
    },
    [locationId],
  );
}
