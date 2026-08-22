import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  Eye,
  Skull,
  User,
  Weight,
} from 'lucide-react';
import type { ReactNode } from 'react';

import {
  getContainerInventoryOptions,
  getCreatureInventoryOptions,
  getTheftDetectionChance,
  transferInventoryMutation,
} from '@/api/client';
import type { ItemDetail, OwnerType } from '@/api/client';
import { NumericStepper } from '@/components/numeric-stepper';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogTitle } from '@/components/ui/dialog';
import { ItemName } from '@/features/inventory/components/item-name';
import { ItemTable } from '@/features/inventory/components/item-table';
import { useItemTable } from '@/features/inventory/hooks/use-item-table';
import { useTransferDraft } from '@/features/inventory/hooks/use-transfer-draft';

interface TransferTarget {
  id: string;
  name: string;
  ownerType: OwnerType;
}

export interface TransferItemDialogProps {
  playerId: string;
  target: TransferTarget | null;
  open: boolean;
  transfersEnabled?: boolean;
  onClose: () => void;
  onTheftEncounter?: (encounterId: string) => void;
}

export function TransferItemDialog({
  playerId,
  target,
  open,
  transfersEnabled = true,
  onClose,
  onTheftEncounter,
}: TransferItemDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-[min(96vw,96rem)]"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        {target && (
          <DialogTitle>{transfersEnabled ? 'Transfer Items' : 'Inspect Inventory'}</DialogTitle>
        )}
        {target && (
          <TransferDialogBody
            key={target.id}
            playerId={playerId}
            target={target}
            transfersEnabled={transfersEnabled}
            onClose={onClose}
            onTheftEncounter={onTheftEncounter}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function TransferDialogBody({
  playerId,
  target,
  transfersEnabled,
  onClose,
  onTheftEncounter,
}: {
  playerId: string;
  target: TransferTarget;
  transfersEnabled: boolean;
  onClose: () => void;
  onTheftEncounter?: (encounterId: string) => void;
}) {
  const queryClient = useQueryClient();
  const playerOwner = { id: playerId, type: 'Creature' as const };
  const targetOwner = { id: target.id, type: target.ownerType };
  const targetCreatureInventory = useQuery({
    ...getCreatureInventoryOptions({ path: { creatureId: target.id } }),
    enabled: target.ownerType === 'Creature',
  });
  const targetContainerInventory = useQuery({
    ...getContainerInventoryOptions({ path: { containerId: target.id } }),
    enabled: target.ownerType === 'Container',
  });
  const targetInventoryOptions =
    target.ownerType === 'Creature'
      ? getCreatureInventoryOptions({ path: { creatureId: target.id } })
      : getContainerInventoryOptions({ path: { containerId: target.id } });
  const playerInventory = useQuery(getCreatureInventoryOptions({ path: { creatureId: playerId } }));
  const targetInventory =
    target.ownerType === 'Creature' ? targetCreatureInventory : targetContainerInventory;
  const transfer = useMutation(transferInventoryMutation());
  const transferDraft = useTransferDraft(playerInventory.data?.items, targetInventory.data?.items);
  const theftItems = [...transferDraft.otherSelection].map(([itemId, quantity]) => ({
    itemId,
    quantity,
  }));
  const theftDetectionChance = useQuery({
    queryKey: ['theft-detection-chance', playerId, target.id, target.ownerType, theftItems],
    queryFn: async () => {
      const response = await getTheftDetectionChance({
        path: { playerId },
        body: { from: targetOwner, items: theftItems },
      });
      return response.data?.successChance ?? null;
    },
    enabled: transfersEnabled && theftItems.length > 0,
  });

  const handleConfirm = async () => {
    let theftEncounterId: string | null = null;

    if (transferDraft.playerToOther.size > 0) {
      const response = await transfer.mutateAsync({
        path: { playerId },
        body: {
          from: playerOwner,
          to: targetOwner,
          items: [...transferDraft.playerToOther].map(([itemId, quantity]) => ({
            itemId,
            quantity,
          })),
        },
      });
      theftEncounterId ??= response.theftEncounterId;
    }
    if (transferDraft.otherToPlayer.size > 0) {
      const response = await transfer.mutateAsync({
        path: { playerId },
        body: {
          from: targetOwner,
          to: playerOwner,
          items: [...transferDraft.otherToPlayer].map(([itemId, quantity]) => ({
            itemId,
            quantity,
          })),
        },
      });
      theftEncounterId ??= response.theftEncounterId;
    }

    await queryClient.invalidateQueries({
      queryKey: getCreatureInventoryOptions({ path: { creatureId: playerId } }).queryKey,
    });
    await queryClient.invalidateQueries({ queryKey: targetInventoryOptions.queryKey });
    onClose();
    if (theftEncounterId) {
      onTheftEncounter?.(theftEncounterId);
    }
  };

  return (
    <>
      <div className="flex min-h-0 flex-1 flex-col items-stretch gap-3 overflow-hidden md:flex-row">
        <InventorySidePanel
          title={
            <>
              <User className="text-muted-foreground size-4" /> You
            </>
          }
          ariaLabel="Your inventory"
          items={transferDraft.playerItems ?? []}
          loading={transferDraft.playerItems === null}
          selected={transferDraft.playerSelection}
          selectionEnabled={transfersEnabled}
          onSelectedChange={transferDraft.setPlayerSelection}
        />

        <div className="flex flex-shrink-0 flex-row items-center justify-center gap-2 md:flex-col">
          <Button
            variant="outline"
            size="icon"
            className="relative rounded-full"
            onClick={transferDraft.moveToPlayer}
            disabled={!transfersEnabled || transferDraft.otherSelection.size === 0}
            title={`Move selected items to your inventory`}
          >
            <ArrowUp className="md:hidden" />
            <ArrowLeft className="hidden md:inline" />
            {transferDraft.otherSelection.size > 0 && (
              <ArrowBadge count={transferDraft.otherSelection.size} />
            )}
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="relative rounded-full"
            onClick={transferDraft.moveToOther}
            disabled={!transfersEnabled || transferDraft.playerSelection.size === 0}
            title={`Move selected items to ${target.name}`}
          >
            <ArrowDown className="md:hidden" />
            <ArrowRight className="hidden md:inline" />
            {transferDraft.playerSelection.size > 0 && (
              <ArrowBadge count={transferDraft.playerSelection.size} />
            )}
          </Button>
        </div>

        <InventorySidePanel
          title={
            <>
              {target.ownerType === 'Creature' ? (
                <Skull className="text-muted-foreground size-4" />
              ) : (
                <Archive className="text-muted-foreground size-4" />
              )}{' '}
              {target.name}
            </>
          }
          ariaLabel={`${target.name}'s inventory`}
          items={transferDraft.otherItems ?? []}
          loading={transferDraft.otherItems === null}
          selected={transferDraft.otherSelection}
          selectionEnabled={transfersEnabled}
          onSelectedChange={transferDraft.setOtherSelection}
        />
      </div>

      <DialogFooter className="flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-1">
          <p className="text-muted-foreground text-sm">
            {transferDraft.changedCount === 0
              ? 'No changes yet.'
              : `${transferDraft.changedCount} stack${transferDraft.changedCount === 1 ? '' : 's'} to transfer.`}
          </p>
          {theftDetectionChance.data != null && (
            <p className="flex items-center gap-1.5 text-sm text-amber-600 dark:text-amber-400">
              <Eye className="size-4" />
              {Math.round(theftDetectionChance.data * 100)}% chance to avoid detection.
            </p>
          )}
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={!transfersEnabled || transferDraft.changedCount === 0 || transfer.isPending}
          >
            Confirm Transfer
          </Button>
        </div>
      </DialogFooter>
    </>
  );
}

function ArrowBadge({ count }: { count: number }) {
  return (
    <span className="bg-primary text-primary-foreground absolute -top-1 -right-1 flex size-4 items-center justify-center rounded-full text-[10px] font-bold">
      {count}
    </span>
  );
}

interface InventorySidePanelProps {
  title: ReactNode;
  ariaLabel: string;
  items: readonly ItemDetail[];
  loading: boolean;
  selected: Map<string, number>;
  selectionEnabled: boolean;
  onSelectedChange: (selected: Map<string, number>) => void;
}

function InventorySidePanel({
  title,
  ariaLabel,
  items,
  loading,
  selected,
  selectionEnabled,
  onSelectedChange,
}: InventorySidePanelProps) {
  const itemTable = useItemTable(items);
  const totalWeight = items.reduce(
    (sum, item) => sum + Number(item.weight) * Number(item.quantity),
    0,
  );
  const allSelected =
    itemTable.visibleItems.length > 0 &&
    itemTable.visibleItems.every((item) => selected.has(item.itemId));
  const someSelected = itemTable.visibleItems.some((item) => selected.has(item.itemId));

  const toggleSelectAll = () => {
    const next = new Map(selected);
    if (allSelected) {
      for (const item of itemTable.visibleItems) {
        next.delete(item.itemId);
      }
    } else {
      for (const item of itemTable.visibleItems) {
        next.set(item.itemId, Number(item.quantity));
      }
    }
    onSelectedChange(next);
  };

  const toggleRow = (item: ItemDetail) => {
    const next = new Map(selected);
    if (next.has(item.itemId)) {
      next.delete(item.itemId);
    } else {
      next.set(item.itemId, Number(item.quantity));
    }
    onSelectedChange(next);
  };

  const setRowQuantity = (itemId: string, quantity: number) => {
    const next = new Map(selected);
    next.set(itemId, quantity);
    onSelectedChange(next);
  };

  return (
    <div
      className="bg-muted/40 flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden rounded-lg border"
      role="region"
      aria-label={ariaLabel}
    >
      <div className="border-b px-3 py-2">
        <div className="flex items-center gap-1.5 text-sm font-semibold">{title}</div>
        <p className="text-muted-foreground mt-0.5 flex items-center gap-1 text-xs">
          {items.length} items · {totalWeight} <Weight className="size-3 shrink-0" /> total
        </p>
      </div>

      <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
        <ItemTable
          table={itemTable}
          renderItemName={(item) => (
            <TransferItemName
              item={item}
              selectedQuantity={selected.get(item.itemId)}
              selectionEnabled={selectionEnabled}
              onSelectedQuantityChange={(quantity) => setRowQuantity(item.itemId, quantity)}
            />
          )}
          loading={loading}
          isSelected={(item) => selected.has(item.itemId)}
          renderLeadingHeader={
            <div className="flex items-center">
              <input
                type="checkbox"
                className="accent-primary size-4"
                checked={allSelected}
                ref={(element) => {
                  if (element) {
                    element.indeterminate = !allSelected && someSelected;
                  }
                }}
                disabled={!selectionEnabled || itemTable.visibleItems.length === 0}
                onChange={toggleSelectAll}
                aria-label="Select all"
              />
            </div>
          }
          renderLeadingCell={(item) => (
            <TransferSelectionCell
              item={item}
              selectedQuantity={selected.get(item.itemId)}
              selectionEnabled={selectionEnabled}
              onToggle={() => toggleRow(item)}
            />
          )}
        />
      </div>
    </div>
  );
}

function TransferItemName({
  item,
  selectedQuantity,
  selectionEnabled,
  onSelectedQuantityChange,
}: {
  item: ItemDetail;
  selectedQuantity: number | undefined;
  selectionEnabled: boolean;
  onSelectedQuantityChange: (quantity: number) => void;
}) {
  const showQuantityStepper =
    selectionEnabled && selectedQuantity !== undefined && Number(item.quantity) > 1;

  return (
    <>
      <ItemName item={item} />
      {showQuantityStepper && (
        <div className="mt-1">
          <NumericStepper
            value={selectedQuantity}
            onChange={onSelectedQuantityChange}
            min={1}
            max={Number(item.quantity)}
            ariaLabel={`Transfer ${item.name} quantity`}
          />
        </div>
      )}
    </>
  );
}

function TransferSelectionCell({
  item,
  selectedQuantity,
  selectionEnabled,
  onToggle,
}: {
  item: ItemDetail;
  selectedQuantity: number | undefined;
  selectionEnabled: boolean;
  onToggle: () => void;
}) {
  const checked = selectedQuantity !== undefined;

  return (
    <div className="flex h-5 items-center">
      <input
        type="checkbox"
        className="accent-primary size-4"
        checked={checked}
        disabled={!selectionEnabled}
        onChange={onToggle}
        aria-label={`Select ${item.name}`}
      />
    </div>
  );
}
