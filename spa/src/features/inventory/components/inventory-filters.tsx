import { SearchInput } from '@/components/search-input';
import { Toggle } from '@/components/ui/toggle';
import { CATEGORY_ORDER, type ItemCategory } from '@/features/inventory/item-visuals';

import { CATEGORY_LABEL } from '../display-names';

interface InventoryFiltersProps {
  search: string;
  onSearchChange: (search: string) => void;
  categories: ReadonlySet<ItemCategory>;
  onCategoriesChange: (categories: ReadonlySet<ItemCategory>) => void;
  equippedOnly: boolean;
  onEquippedOnlyChange: (equippedOnly: boolean) => void;
  ariaLabel?: string;
}

export function InventoryFilters({
  search,
  onSearchChange,
  categories,
  onCategoriesChange,
  equippedOnly,
  onEquippedOnlyChange,
  ariaLabel,
}: InventoryFiltersProps) {
  const toggleCategory = (category: ItemCategory) => {
    const next = new Set(categories);
    if (next.has(category)) {
      next.delete(category);
    } else {
      next.add(category);
    }
    onCategoriesChange(next);
  };

  return (
    <div className="flex flex-col gap-2">
      <SearchInput
        value={search}
        onChange={onSearchChange}
        ariaLabel={ariaLabel ? `Search ${ariaLabel}` : undefined}
      />

      <div className="flex w-full min-w-0 gap-1.5 overflow-x-auto">
        <Toggle
          size="sm"
          variant="outline"
          className="shrink-0 rounded-full"
          pressed={equippedOnly}
          onPressedChange={onEquippedOnlyChange}
          aria-label={ariaLabel ? `${ariaLabel} Equipped` : undefined}
        >
          Equipped
        </Toggle>
        {CATEGORY_ORDER.map((category) => (
          <Toggle
            key={category}
            size="sm"
            variant="outline"
            className="shrink-0 rounded-full"
            pressed={categories.has(category)}
            onPressedChange={() => toggleCategory(category)}
            aria-label={ariaLabel ? `${ariaLabel} ${CATEGORY_LABEL[category]}` : undefined}
          >
            {CATEGORY_LABEL[category]}
          </Toggle>
        ))}
      </div>
    </div>
  );
}
