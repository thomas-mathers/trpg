import { useQuery } from '@tanstack/react-query';
import { Droplet, Heart, Zap } from 'lucide-react';
import { useEffect, useRef } from 'react';

import { getSessionsBySessionIdSceneOptions } from '@/api/client';
import type { SceneSnapshot } from '@/api/client';

interface StatusBarProps {
  sessionId: string;
  turnCount: number;
}

export function StatusBar({ sessionId, turnCount }: StatusBarProps) {
  const query = useQuery(getSessionsBySessionIdSceneOptions({ path: { sessionId } }));

  const previousTurnCount = useRef(turnCount);
  useEffect(() => {
    if (turnCount !== previousTurnCount.current) {
      previousTurnCount.current = turnCount;
      query.refetch();
    }
  }, [turnCount, query]);

  if (!query.data) {
    return null;
  }

  const { playerStatus } = query.data;

  return (
    <div className="flex flex-1 flex-wrap items-center gap-x-4 gap-y-1 text-sm">
      <span className="font-bold">{playerStatus.name}</span>
      <div className="flex items-center gap-4 max-sm:order-10 max-sm:basis-full">
        <span className="flex items-center gap-1">
          <Heart className="h-4 w-4 text-red-500" />
          {playerStatus.currentHp}/{playerStatus.maximumHp}
        </span>
        <span className="flex items-center gap-1">
          <Zap className="h-4 w-4 text-amber-500" />
          {playerStatus.currentAp}/{playerStatus.maximumAp}
        </span>
        <span className="flex items-center gap-1">
          <Droplet className="h-4 w-4 text-blue-500" />
          {playerStatus.currentMp}/{playerStatus.maximumMp}
        </span>
      </div>
      <div className="text-muted-foreground ml-auto hidden items-center gap-4 sm:flex">
        <span>{formatLocation(query.data)}</span>
        <span>{formatTime(query.data)}</span>
      </div>
    </div>
  );
}

function formatLocation(scene: SceneSnapshot): string {
  const levels = [scene.buildingName, scene.districtName, scene.cityName, scene.stateName].filter(
    (level): level is string => Boolean(level),
  );
  return levels.slice(0, 2).join(', ');
}

function formatTime(scene: SceneSnapshot): string {
  return `${scene.weekdayName}, ${scene.monthName} ${scene.day} — ${scene.hour}:00`;
}
