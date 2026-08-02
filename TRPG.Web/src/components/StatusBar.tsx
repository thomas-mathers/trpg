import { Droplet, Heart, Zap } from 'lucide-react';

import type { SceneSnapshot } from '@/api/client';
import { useSceneQuery } from '@/hooks/useSceneQuery';
import { formatLocation } from '@/lib/scene-format';

interface StatusBarProps {
  sessionId: string;
}

export function StatusBar({ sessionId }: StatusBarProps) {
  const query = useSceneQuery(sessionId);

  if (!query.data) {
    return null;
  }

  const { playerStatus } = query.data;

  return (
    <div className="flex flex-1 flex-wrap items-center gap-x-4 gap-y-1 text-sm">
      <span className="shrink-0 font-bold">{playerStatus.name}</span>
      <div className="text-muted-foreground flex min-w-0 flex-wrap items-center gap-2 text-xs max-sm:basis-full sm:gap-4 sm:text-sm">
        <span className="min-w-0 truncate">{formatLocation(query.data)}</span>
        <span className="shrink-0">{formatTime(query.data)}</span>
      </div>
      <div className="ml-auto hidden shrink-0 items-center gap-4 lg:flex">
        <span className="flex items-center gap-1 text-red-500">
          <Heart className="h-4 w-4" />
          {playerStatus.currentHp}/{playerStatus.maximumHp}
        </span>
        <span className="flex items-center gap-1 text-amber-500">
          <Zap className="h-4 w-4" />
          {playerStatus.currentAp}/{playerStatus.maximumAp}
        </span>
        <span className="flex items-center gap-1 text-blue-500">
          <Droplet className="h-4 w-4" />
          {playerStatus.currentMp}/{playerStatus.maximumMp}
        </span>
      </div>
    </div>
  );
}

function formatTime(scene: SceneSnapshot): string {
  return `${scene.weekdayName}, ${scene.monthName} ${scene.day} — ${scene.hour}:00`;
}
