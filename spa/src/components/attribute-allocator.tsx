import type { AttributeAllocation } from '@/api/client';
import { NumericStepper } from '@/components/numeric-stepper';
import { FieldLabel } from '@/components/ui/field';

import {
  ALLOCATABLE_ATTRIBUTES,
  type AllocatableAttributeKey,
  type AttributeValues,
} from './attribute-allocation';

interface AttributeAllocatorProps {
  baseValues: AttributeValues;
  deltas: AttributeAllocation;
  minimumValues: AttributeValues;
  availablePoints: number;
  remainingPoints: number;
  onDeltaChange: (attribute: AllocatableAttributeKey, delta: number) => void;
}

export function AttributeAllocator({
  baseValues,
  deltas,
  minimumValues,
  availablePoints,
  remainingPoints,
  onDeltaChange,
}: AttributeAllocatorProps) {
  return (
    <>
      <div className="flex items-center justify-between">
        <FieldLabel>Attributes</FieldLabel>
        <span className="text-muted-foreground text-sm">
          {remainingPoints} of {availablePoints} points remaining
        </span>
      </div>
      {ALLOCATABLE_ATTRIBUTES.map(({ name, allocationKey, description }) => {
        const baseValue = baseValues[name];
        const value = baseValue + (deltas[allocationKey] ?? 0);

        return (
          <div key={allocationKey} className="flex items-center justify-between">
            <div>
              <p className="text-sm">{name}</p>
              <p className="text-muted-foreground text-xs">{description}</p>
            </div>
            <NumericStepper
              value={value}
              min={minimumValues[name]}
              max={value + remainingPoints}
              ariaLabel={name}
              onChange={(next) => onDeltaChange(allocationKey, next - baseValue)}
            />
          </div>
        );
      })}
    </>
  );
}
