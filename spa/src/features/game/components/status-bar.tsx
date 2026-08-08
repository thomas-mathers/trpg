import { Droplet, Heart, Zap } from 'lucide-react';

import type { SceneSnapshot } from '@/api/client';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useScene } from '@/features/game/contexts/scene-context';
import { formatLocation } from '@/features/game/scene-format';
import type { ConnectionStatus } from '@/lib/game-event-bus';

interface StatusBarProps {
  isInCombat?: boolean;
  connectionStatus: ConnectionStatus;
}

const connectionStatusStyles: Record<ConnectionStatus, { className: string; label: string }> = {
  connected: { className: 'bg-green-500', label: 'Connected' },
  reconnecting: { className: 'animate-pulse bg-amber-500', label: 'Reconnecting…' },
  reconnected: { className: 'bg-green-500', label: 'Connected' },
  disconnected: { className: 'bg-destructive', label: 'Connection lost' },
};

export function StatusBar({ isInCombat = false, connectionStatus }: StatusBarProps) {
  const scene = useScene();

  if (!scene) {
    return null;
  }

  const { playerStatus } = scene;

  if (isInCombat) {
    return (
      <div className="flex flex-1 items-center gap-2 text-sm">
        <PlayerName connectionStatus={connectionStatus} name={playerStatus.name} />
      </div>
    );
  }

  return (
    <div className="flex flex-1 flex-wrap items-center gap-x-4 gap-y-1 text-sm">
      <PlayerName connectionStatus={connectionStatus} name={playerStatus.name} />
      <div className="text-muted-foreground flex min-w-0 flex-wrap items-center gap-2 text-xs max-sm:basis-full sm:gap-4 sm:text-sm">
        <span className="min-w-0 truncate">{formatLocation(scene)}</span>
        <span className="shrink-0">{formatTime(scene)}</span>
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

function PlayerName({
  name,
  connectionStatus,
}: {
  name: string;
  connectionStatus: ConnectionStatus;
}) {
  const status = connectionStatusStyles[connectionStatus];

  return (
    <span className="flex shrink-0 items-center gap-1.5 font-bold">
      {name}
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

function formatTime(scene: SceneSnapshot): string {
  return `${scene.weekdayName}, ${scene.monthName} ${scene.day} — ${scene.hour}:00`;
}
