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
          <Label htmlFor="wait-target-time">Wait until</Label>
          <Input
            type="time"
            id="wait-target-time"
            value={targetTime}
            onChange={(event) => setTargetTime(event.target.value)}
            className="bg-background appearance-none [&::-webkit-calendar-picker-indicator]:hidden [&::-webkit-calendar-picker-indicator]:appearance-none"
          />
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
