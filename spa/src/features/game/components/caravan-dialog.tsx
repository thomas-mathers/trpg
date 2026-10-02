import type { CaravanDestinationSnapshot, NearbyCaravanSnapshot } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useScene } from '@/features/game/contexts/scene-context';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useCaravanInteraction } from '@/features/game/hooks/use-interaction-lifecycle';
import { useAction } from '@/features/game/run-action';

interface CaravanDialogProps {
  caravan: NearbyCaravanSnapshot | null;
  onClose: () => void;
}

export function CaravanDialog({ caravan, onClose }: CaravanDialogProps) {
  const chatHub = useChatHub();
  const { pending, run } = useAction();
  const { scene } = useScene();
  useCaravanInteraction({
    playerId: scene?.playerStatus.id ?? '',
    worldId: scene?.worldId ?? '',
    caravanId: scene && caravan ? caravan.caravanId : undefined,
  });

  if (!caravan) {
    return null;
  }

  const handlePurchase = (destination: CaravanDestinationSnapshot) =>
    void run(chatHub.sendPurchaseCaravanTicket(caravan.caravanId, destination.locationId));

  const handleBoard = async () => {
    const result = await run(chatHub.sendBoardCaravan(caravan.caravanId));
    if (result.succeeded) {
      onClose();
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{caravan.routeName}</DialogTitle>
        </DialogHeader>
        <DialogDescription>
          {caravan.passengerServiceAvailable
            ? `Departing in ${caravan.minutesUntilDeparture} min. A ticket to any stop costs ${caravan.ticketFeeGold} gold.`
            : 'Passenger service is suspended until the weather improves.'}
        </DialogDescription>

        <div className="divide-border divide-y">
          {caravan.destinations.map((destination) => (
            <div
              key={destination.locationId}
              className="flex items-center justify-between gap-2 py-2"
            >
              <span className="min-w-0">
                <span className="block truncate font-medium">{destination.locationName}</span>
                <span className="text-muted-foreground text-xs">
                  {destination.travelTimeHours}h travel
                </span>
              </span>
              <Button
                size="xs"
                variant={destination.hasTicket ? 'default' : 'outline'}
                disabled={pending || !caravan.passengerServiceAvailable}
                onClick={() =>
                  destination.hasTicket ? void handleBoard() : handlePurchase(destination)
                }
              >
                {destination.hasTicket ? 'Board' : 'Buy ticket'}
              </Button>
            </div>
          ))}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            No thanks
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
