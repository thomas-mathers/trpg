import { HubConnectionState } from '@microsoft/signalr';
import { Droplet, Heart, Zap } from 'lucide-react';
import type { ReactNode } from 'react';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useScene } from '@/features/game/contexts/scene-context';
import { formatLocation } from '@/features/game/scene-format';

interface StatusBarProps {
  isInCombat?: boolean;
  connectionStatus: HubConnectionState;
  controls: ReactNode;
}

const connectionStatusStyles: Record<HubConnectionState, { className: string; label: string }> = {
  [HubConnectionState.Connected]: { className: 'bg-green-500', label: 'Connected' },
  [HubConnectionState.Connecting]: {
    className: 'animate-pulse bg-amber-500',
    label: 'Connectingâ€¦',
  },
  [HubConnectionState.Reconnecting]: {
    className: 'animate-pulse bg-amber-500',
    label: 'Reconnectingâ€¦',
  },
  [HubConnectionState.Disconnecting]: { className: 'bg-destructive', label: 'Connection lost' },
  [HubConnectionState.Disconnected]: { className: 'bg-destructive', label: 'Connection lost' },
};

export function StatusBar({ isInCombat = false, connectionStatus, controls }: StatusBarProps) {
  const scene = useScene();

  return (
    <div className="flex w-full flex-wrap items-center gap-x-4 gap-y-1 text-sm lg:grid lg:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] lg:gap-4">
      <div className="flex min-w-0 shrink-0 items-center gap-3 lg:col-start-1 lg:row-start-1">
        {scene && (
          <>
            <PlayerName
              connectionStatus={connectionStatus}
              name={scene.playerStatus.name}
              level={scene.playerStatus.level}
            />
            {!isInCombat && (
              <div className="hidden lg:block">
                <ExperienceProgress
                  current={scene.playerStatus.experienceCurrent}
                  toNextLevel={scene.playerStatus.experienceToNextLevel}
                />
              </div>
            )}
          </>
        )}
      </div>
      <div className="text-muted-foreground flex min-w-0 flex-wrap items-center gap-2 text-xs max-sm:basis-full sm:gap-4 sm:text-sm lg:col-start-2 lg:row-start-1 lg:justify-self-center">
        {scene && !isInCombat && (
          <>
            <span className="min-w-0 truncate">{formatLocation(scene)}</span>
            <span className="shrink-0">{formatTime(scene)}</span>
          </>
        )}
      </div>
      <div className="ml-auto flex shrink-0 items-center gap-4 lg:col-start-3 lg:row-start-1 lg:ml-0 lg:justify-self-end">
        {scene && !isInCombat && (
          <div className="hidden shrink-0 items-center gap-4 lg:flex">
            <span className="flex items-center gap-1 text-red-500">
              <Heart className="h-4 w-4" />
              {scene.playerStatus.currentHp}/{scene.playerStatus.maximumHp}
            </span>
            <span className="flex items-center gap-1 text-amber-500">
              <Zap className="h-4 w-4" />
              {scene.playerStatus.currentAp}/{scene.playerStatus.maximumAp}
            </span>
            <span className="flex items-center gap-1 text-blue-500">
              <Droplet className="h-4 w-4" />
              {scene.playerStatus.currentMp}/{scene.playerStatus.maximumMp}
            </span>
          </div>
        )}
        {controls}
      </div>
    </div>
  );
}

function PlayerName({
  name,
  level,
  connectionStatus,
}: {
  name: string;
  level: number | string;
  connectionStatus: HubConnectionState;
}) {
  const status = connectionStatusStyles[connectionStatus];

  return (
    <span className="flex shrink-0 items-center gap-1.5 font-bold">
      {name}
      <span className="text-muted-foreground text-xs font-medium">Lv {level}</span>
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              aria-label={status.label}
              className={`h-2 w-2 rounded-full ${status.className}`}
            />
          </TooltipTrigger>
          <TooltipContent>{status.label}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    </span>
  );
}

function ExperienceProgress({
  current,
  toNextLevel,
}: {
  current: number | string;
  toNextLevel: number | string;
}) {
  const value = Number(current);
  const maximum = Number(toNextLevel);
  const percent = maximum > 0 ? Math.min(100, (value / maximum) * 100) : 0;

  return (
    <div className="flex items-center gap-1.5" title={`${value}/${maximum} XP`}>
      <div className="bg-muted h-1.5 w-16 overflow-hidden rounded-full">
        <div
          className="h-full rounded-full bg-amber-500 transition-[width] duration-500"
          style={{ width: `${percent}%` }}
        />
      </div>
      <span className="text-muted-foreground text-[10px] font-medium tabular-nums">
        {value}/{maximum} XP
      </span>
    </div>
  );
}

function formatTime(scene: SceneSnapshot): string {
  return `${scene.weekdayName}, ${scene.monthName} ${scene.day} â€” ${scene.hour}:00`;
}
