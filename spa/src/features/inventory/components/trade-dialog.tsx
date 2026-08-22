import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CircleCheck, CircleX, UserRound, X } from 'lucide-react';
import { useEffect, useState, type Dispatch, type SetStateAction } from 'react';
import type { IconType } from 'react-icons';
import { GiOpenTreasureChest, GiShop, GiTwoCoins, GiWeight } from 'react-icons/gi';

import {
  completeTradeMutation,
  getTradeOptions,
  proposeTradeMutation,
  type ItemDetail,
} from '@/api/client';
import { NumericStepper } from '@/components/numeric-stepper';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Empty, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { ItemName } from '@/features/inventory/components/item-name';
import { ItemTable } from '@/features/inventory/components/item-table';
import { useItemTable } from '@/features/inventory/hooks/use-item-table';

export interface TradeDialogProps {
  open: boolean;
  playerId: string;
  worldId: string;
  workstationId: string;
  workerName: string;
  shopName: string;
  onClose: () => void;
}

export type TradeProposalStatus = 'accepted' | 'rejected' | 'refused';

function toTradeRequest(playerOffer: readonly ItemDetail[], shopOffer: readonly ItemDetail[]) {
  return {
    playerOffer: playerOffer.map((item) => ({
      itemId: item.itemId,
      quantity: Number(item.quantity),
    })),
    shopOffer: shopOffer.map((item) => ({
      itemId: item.itemId,
      quantity: Number(item.quantity),
    })),
  };
}

export function TradeDialog({
  playerId,
  worldId,
  workstationId,
  workerName,
  shopName,
  open,
  onClose,
}: TradeDialogProps) {
  const queryClient = useQueryClient();
  const trade = useQuery({
    ...getTradeOptions({ path: { playerId, workstationId } }),
    enabled: open,
  });
  const propose = useMutation(proposeTradeMutation());
  const complete = useMutation(completeTradeMutation());
  const [playerOffer, setPlayerOffer] = useState<ItemDetail[]>([]);
  const [shopOffer, setShopOffer] = useState<ItemDetail[]>([]);
  const [status, setStatus] = useState<TradeProposalStatus>();
  useEffect(() => {
    if (open) {
      setPlayerOffer([]);
      setShopOffer([]);
      setStatus(undefined);
    }
  }, [open, workstationId]);
  if (!trade.data) return null;
  const {
    playerInventory: { items: playerItems },
    shopInventory: { items: shopItems },
  } = trade.data;
  const add = (set: Dispatch<SetStateAction<ItemDetail[]>>) => (item: ItemDetail) => {
    set((current) =>
      current.some((value) => value.itemId === item.itemId) ? current : [...current, item],
    );
    setStatus(undefined);
  };
  const changeQuantity =
    (set: Dispatch<SetStateAction<ItemDetail[]>>) => (itemId: string, quantity: number) => {
      set((current) =>
        current.map((item) => (item.itemId === itemId ? { ...item, quantity } : item)),
      );
      setStatus(undefined);
    };
  const remove = (set: Dispatch<SetStateAction<ItemDetail[]>>) => (itemId: string) => {
    set((current) => current.filter((item) => item.itemId !== itemId));
    setStatus(undefined);
  };
  const hasOffer = playerOffer.length > 0 || shopOffer.length > 0;
  const proposeTrade = async () => {
    const result = await propose.mutateAsync({
      path: { playerId, workstationId },
      body: toTradeRequest(playerOffer, shopOffer),
    });
    setStatus(result.status.toLowerCase() as TradeProposalStatus);
  };
  const completeTrade = async () => {
    await complete.mutateAsync({
      path: { playerId, workstationId },
      query: { worldId },
      body: toTradeRequest(playerOffer, shopOffer),
    });
    await queryClient.invalidateQueries({
      queryKey: getTradeOptions({ path: { playerId, workstationId } }).queryKey,
    });
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex h-[min(94dvh,880px)] w-[calc(100vw-2rem)] max-w-none flex-col gap-4 sm:max-w-none">
        <DialogHeader>
          <DialogTitle>Trade with {workerName}</DialogTitle>
          <DialogDescription>{shopName}</DialogDescription>
        </DialogHeader>

        <div className="grid min-h-0 flex-1 gap-3 overflow-y-auto lg:grid-cols-[minmax(0,1fr)_minmax(15rem,0.65fr)_minmax(0,1fr)] lg:overflow-hidden">
          <InventoryPanel
            icon={UserRound}
            title="Your inventory"
            items={playerItems}
            offerItems={playerOffer}
            ariaLabel="Your available items"
            filterKey={playerId}
            onAdd={add(setPlayerOffer)}
          />

          <div className="flex min-h-0 flex-col gap-3">
            <OfferPanel
              title="You offer"
              items={playerOffer}
              sourceItems={playerItems}
              emptyMessage="Add items from your inventory"
              onQuantityChange={changeQuantity(setPlayerOffer)}
              onRemove={remove(setPlayerOffer)}
            />
            <OfferPanel
              title="You receive"
              items={shopOffer}
              sourceItems={shopItems}
              emptyMessage="Add items from the shop"
              onQuantityChange={changeQuantity(setShopOffer)}
              onRemove={remove(setShopOffer)}
            />
          </div>

          <InventoryPanel
            icon={GiShop}
            title="Shop inventory"
            items={shopItems}
            offerItems={shopOffer}
            ariaLabel="Shop available items"
            filterKey={workstationId}
            onAdd={add(setShopOffer)}
          />
        </div>

        <DialogFooter className="flex-col items-stretch gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-1.5">
            {status && <TradeResponse status={status} workerName={workerName} />}
            <TradeSummary playerOffer={playerOffer} shopOffer={shopOffer} />
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>
              {status === 'accepted' ? 'Close' : 'Cancel'}
            </Button>
            <Button
              disabled={status !== 'accepted' && !hasOffer}
              onClick={status === 'accepted' ? completeTrade : proposeTrade}
            >
              {status === 'accepted' ? 'Complete trade' : 'Propose trade'}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function InventoryPanel({
  icon: Icon,
  title,
  items,
  offerItems,
  ariaLabel,
  filterKey,
  onAdd,
}: {
  icon: IconType;
  title: string;
  items: readonly ItemDetail[];
  offerItems: readonly ItemDetail[];
  ariaLabel: string;
  filterKey: string;
  onAdd: (item: ItemDetail) => void;
}) {
  const itemTable = useItemTable(items, { resetKey: filterKey });
  const totalWeight = items.reduce(
    (sum, item) => sum + Number(item.weight) * Number(item.quantity),
    0,
  );
  const offeredItemIds = new Set(offerItems.map((item) => item.itemId));

  return (
    <section
      className="bg-muted/40 flex min-h-72 min-w-0 flex-col overflow-hidden rounded-lg border lg:min-h-0"
      aria-label={ariaLabel}
    >
      <div className="border-b px-3 py-2">
        <h2 className="flex items-center gap-1.5 text-sm font-semibold">
          <Icon className="text-muted-foreground size-4" />
          {title}
        </h2>
        <p className="text-muted-foreground mt-0.5 flex items-center gap-1 text-xs">
          {items.length} items · {totalWeight} <GiWeight className="size-4 shrink-0" /> total
        </p>
      </div>
      <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
        <ItemTable
          table={itemTable}
          renderItemName={(item) => <ItemName item={item} />}
          renderAction={(item) =>
            offeredItemIds.has(item.itemId) ? (
              <div className="h-8" />
            ) : (
              <Button
                variant="outline"
                aria-label={`Add ${item.name} to offer`}
                onClick={() => onAdd(item)}
              >
                Add
              </Button>
            )
          }
        />
      </div>
    </section>
  );
}

function OfferPanel({
  title,
  items,
  sourceItems,
  emptyMessage,
  onQuantityChange,
  onRemove,
}: {
  title: string;
  items: readonly ItemDetail[];
  sourceItems: readonly ItemDetail[];
  emptyMessage: string;
  onQuantityChange: (itemId: string, quantity: number) => void;
  onRemove: (itemId: string) => void;
}) {
  const total = items.reduce(
    (sum, item) => sum + Number(item.goldValue) * Number(item.quantity),
    0,
  );

  return (
    <section className="bg-muted/40 flex min-h-0 flex-1 flex-col overflow-hidden rounded-lg border">
      <div className="flex items-center justify-between border-b px-3 py-2">
        <h2 className="text-sm font-semibold">{title}</h2>
        <span className="text-muted-foreground flex items-center gap-1 font-mono text-xs tabular-nums">
          {total} <GiTwoCoins className="size-4" />
        </span>
      </div>
      {items.length === 0 ? (
        <Empty className="min-h-28">
          <EmptyMedia variant="icon">
            <GiOpenTreasureChest />
          </EmptyMedia>
          <EmptyTitle>{emptyMessage}</EmptyTitle>
        </Empty>
      ) : (
        <ul className="divide-border divide-y">
          {items.map((item) => (
            <li key={item.itemId} className="flex items-center gap-2 px-3 py-2">
              <div className="min-w-0 flex-1">
                <ItemName item={item} />
              </div>
              {item.isStackable ? (
                <NumericStepper
                  value={Number(item.quantity)}
                  onChange={(quantity) => onQuantityChange(item.itemId, quantity)}
                  min={1}
                  max={
                    sourceItems.find((sourceItem) => sourceItem.itemId === item.itemId)?.quantity ??
                    1
                  }
                  ariaLabel={`${item.name} quantity`}
                />
              ) : null}
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label={`Remove ${item.name} from offer`}
                onClick={() => onRemove(item.itemId)}
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function TradeSummary({
  playerOffer,
  shopOffer,
}: {
  playerOffer: readonly ItemDetail[];
  shopOffer: readonly ItemDetail[];
}) {
  const playerOfferValue = playerOffer.reduce(
    (sum, item) => sum + Number(item.goldValue) * Number(item.quantity),
    0,
  );
  const shopOfferValue = shopOffer.reduce(
    (sum, item) => sum + Number(item.goldValue) * Number(item.quantity),
    0,
  );
  const difference = playerOfferValue - shopOfferValue;
  const summary =
    difference === 0
      ? 'This is an even trade.'
      : difference > 0
        ? `You are offering ${difference} more in value.`
        : `You are requesting ${Math.abs(difference)} more in value.`;

  return (
    <p className="text-muted-foreground flex items-center gap-1.5 text-sm">
      <GiTwoCoins className="size-4" />
      {summary}
    </p>
  );
}

function TradeResponse({
  status,
  workerName,
}: {
  status: TradeProposalStatus;
  workerName: string;
}) {
  const accepted = status === 'accepted';
  const Icon = accepted ? CircleCheck : CircleX;
  const message =
    status === 'accepted'
      ? `${workerName} accepts your offer.`
      : status === 'refused'
        ? `${workerName} refuses to deal with you.`
        : `${workerName} declines your offer.`;

  return (
    <p
      className={
        accepted
          ? 'text-heal flex items-center gap-1.5 text-sm'
          : 'text-destructive flex items-center gap-1.5 text-sm'
      }
      role="status"
    >
      <Icon className="size-4" />
      {message}
    </p>
  );
}
