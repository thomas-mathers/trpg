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
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useCaravanInteraction } from '@/features/game/hooks/use-interaction-lifecycle';

interface CaravanDialogProps {
  caravan: NearbyCaravanSnapshot | null;
  onClose: () => void;
}

export function CaravanDialog({ caravan, onClose }: CaravanDialogProps) {
  const chatHub = useChatHub();
  const { submitNarratedTurn, isStreaming } = useGameChat();
  const scene = useScene();
  useCaravanInteraction({
    playerId: scene?.playerStatus.id ?? '',
    worldId: scene?.worldId ?? '',
    caravanId: scene && caravan ? caravan.caravanId : undefined,
  });

  if (!caravan) {
    return null;
  }

  const handlePurchase = (destination: CaravanDestinationSnapshot) => {
    submitNarratedTurn(
      `Buy a caravan ticket to ${destination.locationName}`,
      chatHub.sendPurchaseCaravanTicket(caravan.caravanId, destination.locationId),
    );
  };

  const handleBoard = (destination: CaravanDestinationSnapshot) => {
    submitNarratedTurn(
      `Board the caravan to ${destination.locationName}`,
      chatHub.sendBoardCaravan(caravan.caravanId),
    );
    onClose();
  };

  const handleDecline = () => {
    submitNarratedTurn('Decline the caravan ticket', chatHub.sendDeclineCaravanTicket());
    onClose();
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
                disabled={isStreaming || !caravan.passengerServiceAvailable}
                onClick={() =>
                  destination.hasTicket ? handleBoard(destination) : handlePurchase(destination)
                }
              >
                {destination.hasTicket ? 'Board' : 'Buy ticket'}
              </Button>
            </div>
          ))}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleDecline} disabled={isStreaming}>
            No thanks
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
