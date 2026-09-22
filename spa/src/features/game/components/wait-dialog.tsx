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
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';

export interface WaitDialogProps {
  open: boolean;
  onClose: () => void;
}

const MINUTES_PER_DAY = 24 * 60;

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
  const [targetTime, setTargetTime] = useState('08:00');

  useEffect(() => {
    if (open && scene) {
      setTargetTime(formatCurrentHour(scene.hour));
    }
  }, [open, scene]);

  if (!scene) {
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
    const { hour: targetHour, minute: targetMinute } = parseTime(targetTime);
    let deltaMinutes = targetHour * 60 + targetMinute - scene.hour * 60;
    if (deltaMinutes <= 0) {
      // Picking a time at or before the current hour means "wait until that time tomorrow".
      deltaMinutes += MINUTES_PER_DAY;
    }
    const hours = Math.floor(deltaMinutes / 60);
    const minutes = deltaMinutes % 60;

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
