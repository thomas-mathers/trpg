import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useScene } from '@/features/game/contexts/scene-context';
import { durationUntilNextTime } from '@/features/game/game-clock';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useGameTimeReader } from '@/features/game/hooks/use-game-clock';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';

export interface WaitDialogProps {
  open: boolean;
  onClose: () => void;
}

function formatCurrentHour(hour: number): string {
  return `${hour.toString().padStart(2, '0')}:00`;
}

function parseTime(value: string): { hour: number; minute: number } {
  const [hour, minute] = value.split(':').map(Number);
  return { hour, minute };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

export function WaitDialog({ open, onClose }: WaitDialogProps) {
  const scene = useScene();
  const chatHub = useChatHub();
  const { submitNarratedTurn } = useGameChat();
  const readGameTime = useGameTimeReader();
  const [targetTime, setTargetTime] = useState('08:00');

  useEffect(() => {
    const gameTime = readGameTime();
    if (open && gameTime) {
      setTargetTime(formatCurrentHour(gameTime.hour));
    }
  }, [open, readGameTime]);

  useEffect(() => {
    if (open && scene && scene.playerStatus.state !== 'Sitting') {
      onClose();
    }
  }, [onClose, open, scene]);

  if (!scene || scene.playerStatus.state !== 'Sitting') {
    return null;
  }

  const { hour, minute } = parseTime(targetTime);

  const setHour = (value: number) => {
    if (Number.isNaN(value)) {
      return;
    }
    setTargetTime(
      `${clamp(value, 0, 23).toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`,
    );
  };

  const setMinute = (value: number) => {
    if (Number.isNaN(value)) {
      return;
    }
    setTargetTime(
      `${hour.toString().padStart(2, '0')}:${clamp(value, 0, 59).toString().padStart(2, '0')}`,
    );
  };

  const handleConfirm = () => {
    const gameTime = readGameTime();
    if (!gameTime) {
      return;
    }
    const { hour: targetHour, minute: targetMinute } = parseTime(targetTime);
    const { hours, minutes } = durationUntilNextTime(gameTime, targetHour, targetMinute);

    submitNarratedTurn(`Wait until ${targetTime}`, chatHub.sendWait(hours, minutes));
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Wait</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-2">
          <Label htmlFor="wait-target-hour">Wait until</Label>
          <div className="flex items-center gap-1">
            <Input
              type="number"
              id="wait-target-hour"
              min={0}
              max={23}
              value={hour}
              onChange={(event) => setHour(Number(event.target.value))}
              className="bg-card w-16 text-center"
            />
            <span aria-hidden="true">:</span>
            <Input
              type="number"
              id="wait-target-minute"
              aria-label="Minute"
              min={0}
              max={59}
              value={minute.toString().padStart(2, '0')}
              onChange={(event) => setMinute(Number(event.target.value))}
              className="bg-card w-16 text-center"
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleConfirm}>Wait</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
