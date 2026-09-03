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

export interface SleepDialogProps {
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

export function SleepDialog({ open, onClose }: SleepDialogProps) {
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

  const handleConfirm = () => {
    const { hour: targetHour, minute: targetMinute } = parseTime(targetTime);
    let deltaMinutes = targetHour * 60 + targetMinute - scene.hour * 60;
    if (deltaMinutes <= 0) {
      // Picking a time at or before the current hour means "sleep until that time tomorrow".
      deltaMinutes += MINUTES_PER_DAY;
    }
    const hours = Math.floor(deltaMinutes / 60);
    const minutes = deltaMinutes % 60;

    submitNarratedTurn(`Sleep until ${targetTime}`, chatHub.sendSleep(hours, minutes));
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Sleep</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-2">
          <Label htmlFor="sleep-target-time">Sleep until</Label>
          <Input
            type="time"
            id="sleep-target-time"
            value={targetTime}
            onChange={(event) => setTargetTime(event.target.value)}
            className="bg-card appearance-none [&::-webkit-calendar-picker-indicator]:hidden [&::-webkit-calendar-picker-indicator]:appearance-none"
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleConfirm}>Sleep</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
