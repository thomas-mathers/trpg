import type { ReactNode } from 'react';

import type { ItemDetail } from '@/api/client';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTextTrigger,
} from '@/components/ui/hover-popover';
import { ItemTooltip } from '@/features/inventory/components/item-tooltip';
import { RARITY_COLOR, TYPE_ICON } from '@/features/inventory/item-visuals';

interface ItemNameProps {
  item: ItemDetail;
  equippedLabel?: ReactNode;
}

export function ItemName({ item, equippedLabel = 'Equipped' }: ItemNameProps) {
  const Icon = TYPE_ICON[item.type];
  const rarityColor = item.rarity ? RARITY_COLOR[item.rarity] : undefined;

  return (
    <div className="flex h-5 min-w-0 items-center gap-1.5 text-sm">
      <Icon className="text-muted-foreground size-3.5 shrink-0" />
      {rarityColor && (
        <span
          className="size-1.5 shrink-0 rounded-full"
          style={{ backgroundColor: rarityColor }}
          title={item.rarity ?? undefined}
        />
      )}
      <HoverPopover>
        <HoverPopoverTextTrigger
          className="min-w-0 truncate text-left font-medium"
          onClick={(event) => event.stopPropagation()}
        >
          {item.name}
        </HoverPopoverTextTrigger>
        <HoverPopoverContent side="bottom" className="w-auto max-w-64 p-2 text-sm">
          <ItemTooltip item={item} />
        </HoverPopoverContent>
      </HoverPopover>
      {item.equippedSlot != null && (
        <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase">
          {equippedLabel}
        </span>
      )}
    </div>
  );
}
