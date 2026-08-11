import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import {
  getCreatureBasicAttackDamageOptions,
  getCreatureInventoryOptions,
  getCreatureAttributesOptions,
  equipCreatureItemMutation,
  unequipCreatureItemMutation,
} from '@/api/client';
import type { EquipmentSlot, ItemDetail, ItemType } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  CharacterStatsPanel,
  type EquipItemPreview,
} from '@/features/inventory/components/character-stats-panel';
import { ItemName } from '@/features/inventory/components/item-name';
import { ItemTable } from '@/features/inventory/components/item-table';
import { useItemTable } from '@/features/inventory/hooks/use-item-table';

import { EQUIPMENT_SLOT_LABEL } from '../display-names';

const WEAPON_TYPES = new Set<ItemType>([
  'Dagger',
  'Sword',
  'Axe',
  'Mace',
  'Hammer',
  'Staff',
  'Wand',
  'Bow',
  'Crossbow',
  'Javelin',
  'GreatSword',
  'GreatAxe',
  'GreatHammer',
]);

const ARMOR_SLOTS = new Set<ItemType>(['Helm', 'Chest', 'Boots', 'Gloves']);

function equipSlotFor(item: ItemDetail, equippedSlots: Set<EquipmentSlot>): EquipmentSlot | null {
  if (WEAPON_TYPES.has(item.type)) {
    return 'RightHand';
  }
  if (item.type === 'Shield' || item.type === 'Arrow' || item.type === 'Bolt') {
    return 'LeftHand';
  }
  if (ARMOR_SLOTS.has(item.type)) {
    return item.type as EquipmentSlot;
  }
  if (item.type === 'Necklace' || item.type === 'Belt') {
    return item.type;
  }
  if (item.type === 'Ring') {
    return equippedSlots.has('LeftRing') ? 'RightRing' : 'LeftRing';
  }
  return null;
}

interface InventoryDialogProps {
  playerId: string;
  open: boolean;
  onClose: () => void;
}

export function InventoryDialog({ playerId, open, onClose }: InventoryDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-5xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <InventoryDialogBody playerId={playerId} onClose={onClose} />
      </DialogContent>
    </Dialog>
  );
}

function InventoryDialogBody({ playerId, onClose }: { playerId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const inventoryOptions = getCreatureInventoryOptions({
    path: { creatureId: playerId },
  });
  const inventory = useQuery(inventoryOptions);
  const items = inventory.data?.items ?? [];

  const itemTable = useItemTable(items);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const invalidateCreatureData = () => {
    queryClient.invalidateQueries({ queryKey: inventoryOptions.queryKey });
    queryClient.invalidateQueries({
      queryKey: getCreatureAttributesOptions({ path: { creatureId: playerId } }).queryKey,
    });
    queryClient.invalidateQueries({
      queryKey: getCreatureBasicAttackDamageOptions({
        path: { creatureId: playerId },
      }).queryKey,
    });
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'previewCreatureEquipment' || id === 'previewCreatureBasicAttackDamage';
      },
    });
  };

  const equip = useMutation({
    ...equipCreatureItemMutation(),
    onSuccess: () => {
      invalidateCreatureData();
      setSelectedItemId(null);
    },
  });
  const unequip = useMutation({
    ...unequipCreatureItemMutation(),
    onSuccess: invalidateCreatureData,
  });

  const equippedSlots = new Set(
    items.map((item) => item.equippedSlot).filter((slot): slot is EquipmentSlot => slot != null),
  );
  const busy = equip.isPending || unequip.isPending;

  const selectedItem = selectedItemId ? items.find((i) => i.itemId === selectedItemId) : null;
  const selectedSlot =
    selectedItem && selectedItem.equippedSlot == null
      ? equipSlotFor(selectedItem, equippedSlots)
      : null;
  const previewItem: EquipItemPreview | null =
    selectedItem && selectedSlot ? { itemId: selectedItem.itemId, slot: selectedSlot } : null;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Inventory</DialogTitle>
      </DialogHeader>

      <div className="flex min-h-0 flex-1 gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <ItemTable
            table={itemTable}
            renderItemName={(item) => (
              <ItemName
                item={item}
                equippedLabel={
                  item.equippedSlot === null ? undefined : EQUIPMENT_SLOT_LABEL[item.equippedSlot]
                }
              />
            )}
            loading={!inventory.data}
            emptyMessage="Your inventory is empty."
            onRowClick={(item) =>
              setSelectedItemId((current) => (current === item.itemId ? null : item.itemId))
            }
            isSelected={(item) => item.itemId === selectedItemId}
            renderAction={(item) => (
              <InventoryItemAction
                item={item}
                equippedSlots={equippedSlots}
                busy={busy}
                onEquip={(itemId, slot) =>
                  equip.mutate({ path: { creatureId: playerId }, body: { itemId, slot } })
                }
                onUnequip={(slot) => unequip.mutate({ path: { creatureId: playerId, slot } })}
              />
            )}
          />
        </div>

        <CharacterStatsPanel creatureId={playerId} previewItem={previewItem} />
      </div>

      <DialogFooter>
        <Button aria-label="Close inventory" variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function InventoryItemAction({
  item,
  equippedSlots,
  busy,
  onEquip,
  onUnequip,
}: {
  item: ItemDetail;
  equippedSlots: Set<EquipmentSlot>;
  busy: boolean;
  onEquip: (itemId: string, slot: EquipmentSlot) => void;
  onUnequip: (slot: EquipmentSlot) => void;
}) {
  const targetSlot = equipSlotFor(item, equippedSlots);
  const equippedSlot = item.equippedSlot;

  if (equippedSlot != null) {
    return (
      <Button
        size="sm"
        variant="outline"
        disabled={busy}
        onClick={(event) => {
          event.stopPropagation();
          onUnequip(equippedSlot);
        }}
      >
        Unequip
      </Button>
    );
  }

  if (targetSlot != null) {
    return (
      <Button
        size="sm"
        disabled={busy}
        onClick={(event) => {
          event.stopPropagation();
          onEquip(item.itemId, targetSlot);
        }}
      >
        Equip
      </Button>
    );
  }

  return null;
}
