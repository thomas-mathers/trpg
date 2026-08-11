import { useForm } from '@tanstack/react-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import * as z from 'zod';

import {
  allocateCreatureAttributePointsMutation,
  getCreatureAttributePointsOptions,
  getCreatureBaseAttributesOptions,
  getCreatureAttributesQueryKey,
  getCreatureBaseAttributesQueryKey,
  getCreatureAttributePointsQueryKey,
  getCreatureBasicAttackDamageQueryKey,
  type BaseAttributesResponse,
} from '@/api/client';
import {
  ALLOCATABLE_ATTRIBUTES,
  toAttributeValues,
  type AllocatableAttributeKey,
} from '@/components/attribute-allocation';
import { AttributeAllocator } from '@/components/attribute-allocator';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Field, FieldGroup } from '@/components/ui/field';
import { Spinner } from '@/components/ui/spinner';
import { usePlayerId } from '@/features/game/contexts/scene-context';

type AttributeDeltas = Record<AllocatableAttributeKey, number>;

const attributeDeltasSchema = z.object(
  Object.fromEntries(
    ALLOCATABLE_ATTRIBUTES.map(({ allocationKey }) => [allocationKey, z.number().int().min(0)]),
  ) as Record<AllocatableAttributeKey, z.ZodNumber>,
);

const defaultAttributeDeltas: AttributeDeltas = {
  strength: 0,
  dexterity: 0,
  endurance: 0,
  stamina: 0,
  mana: 0,
  intelligence: 0,
};

const FORM_ID = 'character-attribute-allocation-form';

interface CharacterDialogProps {
  open: boolean;
  onClose: () => void;
}

export function CharacterDialog({ open, onClose }: CharacterDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-2xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <CharacterDialogBody onClose={onClose} />
      </DialogContent>
    </Dialog>
  );
}

function CharacterDialogBody({ onClose }: { onClose: () => void }) {
  const playerId = usePlayerId();
  const queryClient = useQueryClient();
  const baseAttributes = useQuery({
    ...getCreatureBaseAttributesOptions({ path: { creatureId: playerId ?? '' } }),
    enabled: playerId !== undefined,
  });
  const attributePoints = useQuery({
    ...getCreatureAttributePointsOptions({ path: { creatureId: playerId ?? '' } }),
    enabled: playerId !== undefined,
  });

  const allocatePoints = useMutation(allocateCreatureAttributePointsMutation());

  if (!playerId || !baseAttributes.data || !attributePoints.data) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  return (
    <AttributeAllocationForm
      baseAttributes={baseAttributes.data}
      availablePoints={attributePoints.data.unallocatedPoints}
      isSubmitting={allocatePoints.isPending}
      errorMessage={allocatePoints.error ? 'Failed to allocate attribute points.' : null}
      onCancel={onClose}
      onSubmit={(deltas) =>
        allocatePoints.mutate(
          { path: { creatureId: playerId }, body: { deltas } },
          {
            onSuccess: () => {
              void queryClient.invalidateQueries({
                queryKey: getCreatureAttributePointsQueryKey({ path: { creatureId: playerId } }),
              });
              void queryClient.invalidateQueries({
                queryKey: getCreatureBaseAttributesQueryKey({ path: { creatureId: playerId } }),
              });
              void queryClient.invalidateQueries({
                queryKey: getCreatureAttributesQueryKey({ path: { creatureId: playerId } }),
              });
              void queryClient.invalidateQueries({
                queryKey: getCreatureBasicAttackDamageQueryKey({
                  path: { creatureId: playerId },
                }),
              });
              onClose();
            },
          },
        )
      }
    />
  );
}

function AttributeAllocationForm({
  baseAttributes,
  availablePoints,
  isSubmitting,
  errorMessage,
  onCancel,
  onSubmit,
}: {
  baseAttributes: BaseAttributesResponse;
  availablePoints: number;
  isSubmitting: boolean;
  errorMessage: string | null;
  onCancel: () => void;
  onSubmit: (deltas: AttributeDeltas) => void;
}) {
  const baseValues = toAttributeValues(baseAttributes);
  const form = useForm({
    defaultValues: defaultAttributeDeltas,
    validators: { onSubmit: attributeDeltasSchema },
    onSubmit: ({ value }) => onSubmit(value),
  });

  useEffect(() => {
    form.reset(defaultAttributeDeltas);
  }, [form, baseAttributes, availablePoints]);

  return (
    <>
      <DialogHeader>
        <DialogTitle>Character</DialogTitle>
      </DialogHeader>

      <form
        id={FORM_ID}
        className="min-h-0 flex-1 overflow-y-auto"
        onSubmit={(event) => {
          event.preventDefault();
          form.handleSubmit();
        }}
      >
        <FieldGroup>
          <form.Subscribe selector={(state) => state.values}>
            {(values) => {
              const spent = Object.values(values).reduce((sum, value) => sum + value, 0);
              const remaining = availablePoints - spent;

              return (
                <Field>
                  <AttributeAllocator
                    baseValues={baseValues}
                    deltas={values}
                    minimumValues={baseValues}
                    availablePoints={availablePoints}
                    remainingPoints={remaining}
                    onDeltaChange={(attribute, delta) => form.setFieldValue(attribute, delta)}
                  />
                  <form.Subscribe selector={(state) => state.errorMap.onSubmit}>
                    {(error) =>
                      error && <p className="text-destructive text-sm">{String(error)}</p>
                    }
                  </form.Subscribe>
                </Field>
              );
            }}
          </form.Subscribe>
        </FieldGroup>
      </form>

      {errorMessage && <p className="text-destructive text-sm">{errorMessage}</p>}
      <DialogFooter className="flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-end">
        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
          <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting] as const}>
            {([canSubmit, submitting]) => (
              <Button
                type="submit"
                form={FORM_ID}
                disabled={!canSubmit || submitting || isSubmitting}
              >
                Confirm
              </Button>
            )}
          </form.Subscribe>
        </div>
      </DialogFooter>
    </>
  );
}
