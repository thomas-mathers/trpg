import { useForm } from '@tanstack/react-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import * as z from 'zod';

import {
  getCreatureGenerationOptionsOptions,
  getJobOptions,
  listWorldsOptions,
  createSessionMutation,
  createWorldMutation,
} from '../../../api/client';
import type { Gender, PlayerClass, Race } from '../../../api/client';
import {
  MINIMUM_ATTRIBUTE_VALUES,
  toAttributeValues,
} from '../../../components/attribute-allocation';
import { AttributeAllocator } from '../../../components/attribute-allocator';
import { NumericStepper } from '../../../components/numeric-stepper';
import { Button } from '../../../components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '../../../components/ui/dialog';
import { Field, FieldError, FieldGroup, FieldLabel } from '../../../components/ui/field';
import { Input } from '../../../components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select';
import { Slider } from '../../../components/ui/slider';
import { Spinner } from '../../../components/ui/spinner';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/tabs';
import { WorldGenerationDefaults } from '../world-generation-defaults';

const GENDERS: Gender[] = ['Male', 'Female'];
const MIN_AGE = 18;
const MAX_AGE = 75;
const RACES: Race[] = ['Human', 'Elf', 'Dwarf', 'Orc', 'Halfling', 'Gnome'];
const PLAYER_CLASSES: PlayerClass[] = ['Knight', 'Rogue', 'Ranger', 'Mage', 'Cleric'];
type Status = 'idle' | 'generating' | 'error';

interface CreateWorldResponse {
  worldId: string;
  playerId: string;
  worldName: string;
}

const newWorldFormSchema = z.object({
  name: z.string().trim().min(1, 'Name is required'),
  gender: z.custom<Gender>(),
  age: z.number(),
  race: z.custom<Race>(),
  playerClass: z.custom<PlayerClass>(),
  attributes: z.record(z.string(), z.number()),
  description: z.string(),
  cityStates: z.tuple([z.number(), z.number()]),
  ruralStates: z.tuple([z.number(), z.number()]),
  dungeonsPerState: z.tuple([z.number(), z.number()]),
  factionMembers: z.tuple([z.number(), z.number()]),
  householdSize: z.tuple([z.number(), z.number()]),
  factionCount: z.number(),
  housesPerCity: z.number(),
});

const defaultValues = {
  name: '',
  gender: 'Male' as Gender,
  age: 30,
  race: 'Human' as Race,
  playerClass: 'Knight' as PlayerClass,
  attributes: {} as Record<string, number>,
  description: WorldGenerationDefaults.description,
  cityStates: [WorldGenerationDefaults.minCityStates, WorldGenerationDefaults.maxCityStates] as [
    number,
    number,
  ],
  ruralStates: [WorldGenerationDefaults.minRuralStates, WorldGenerationDefaults.maxRuralStates] as [
    number,
    number,
  ],
  dungeonsPerState: [
    WorldGenerationDefaults.minBuildingsPerState,
    WorldGenerationDefaults.maxBuildingsPerState,
  ] as [number, number],
  factionMembers: [
    WorldGenerationDefaults.minFactionMembers,
    WorldGenerationDefaults.maxFactionMembers,
  ] as [number, number],
  householdSize: [
    WorldGenerationDefaults.minHouseholdSize,
    WorldGenerationDefaults.maxHouseholdSize,
  ] as [number, number],
  factionCount: WorldGenerationDefaults.factionCount,
  housesPerCity: WorldGenerationDefaults.housesPerCity,
};

const FORM_ID = 'new-world-form';

export function NewWorldDialog() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState<Status>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | null>(null);

  const generationOptions = useQuery(getCreatureGenerationOptionsOptions());
  const pointsPerLevel = Number(generationOptions.data?.pointsPerLevel ?? 0);

  const createWorld = useMutation(createWorldMutation());
  const startSession = useMutation(createSessionMutation());

  const form = useForm({
    defaultValues,
    validators: { onSubmit: newWorldFormSchema },
    onSubmit: ({ value }) => {
      setStatus('generating');
      setErrorMessage(null);

      createWorld.mutate(
        {
          body: {
            playerName: value.name,
            gender: value.gender,
            age: value.age,
            race: value.race,
            playerClass: value.playerClass,
            startingAttributeAllocation: value.attributes,
            description: value.description,
            minCityStates: value.cityStates[0],
            maxCityStates: value.cityStates[1],
            minRuralStates: value.ruralStates[0],
            maxRuralStates: value.ruralStates[1],
            minBuildingsPerState: value.dungeonsPerState[0],
            maxBuildingsPerState: value.dungeonsPerState[1],
            minFactionMembers: value.factionMembers[0],
            maxFactionMembers: value.factionMembers[1],
            factionCount: value.factionCount,
            housesPerCity: value.housesPerCity,
            minHouseholdSize: value.householdSize[0],
            maxHouseholdSize: value.householdSize[1],
          },
        },
        {
          onSuccess: (data) => setJobId(data.jobId),
          onError: () => {
            setStatus('error');
            setErrorMessage('Failed to start world generation.');
          },
        },
      );
    },
  });

  const jobStatus = useQuery({
    ...getJobOptions({ path: { id: jobId ?? '' } }),
    enabled: jobId !== null,
    refetchInterval: (query) => {
      const currentStatus = query.state.data?.status;
      return currentStatus === 'Done' || currentStatus === 'Failed' || currentStatus === 'Cancelled'
        ? false
        : 1000;
    },
  });

  useEffect(() => {
    const data = jobStatus.data;
    if (
      !data ||
      data.status === 'Idle' ||
      data.status === 'Queued' ||
      data.status === 'InProgress'
    ) {
      return;
    }

    if (data.status === 'Failed' || data.status === 'Cancelled') {
      setStatus('error');
      setErrorMessage(data.errorMessage ?? 'World generation failed.');
      return;
    }

    const result = JSON.parse(data.resultJson!) as CreateWorldResponse;
    queryClient.invalidateQueries({ queryKey: listWorldsOptions().queryKey });

    startSession.mutate(
      { body: { worldId: result.worldId } },
      {
        onSuccess: (session) => {
          setOpen(false);
          navigate({ to: '/session/$sessionId', params: { sessionId: session.sessionId } });
        },
        onError: () => {
          setStatus('error');
          setErrorMessage('World was created but starting the session failed.');
        },
      },
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [jobStatus.data]);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setStatus('idle');
          setErrorMessage(null);
          setJobId(null);
          form.reset();
        }
      }}
    >
      <DialogTrigger asChild>
        <Button>New World</Button>
      </DialogTrigger>
      <DialogContent
        className="max-h-[85vh] overflow-y-auto sm:max-w-xl"
        closeButtonDisabled={status === 'generating'}
        onPointerDownOutside={(e) => e.preventDefault()}
        onEscapeKeyDown={(e) => {
          if (status === 'generating') {
            e.preventDefault();
          }
        }}
      >
        <DialogHeader>
          <DialogTitle>New World</DialogTitle>
        </DialogHeader>

        {status === 'generating' && (
          <div className="flex flex-col items-center gap-4 py-8">
            <Spinner className="size-8" />
            <p className="text-muted-foreground text-sm">Generating world...</p>
          </div>
        )}

        {status === 'error' && (
          <div className="flex flex-col gap-4 py-4">
            <p className="text-destructive text-sm">{errorMessage}</p>
            <Button variant="outline" onClick={() => setStatus('idle')}>
              Back
            </Button>
          </div>
        )}

        {status === 'idle' && (
          <>
            <form
              id={FORM_ID}
              onSubmit={(e) => {
                e.preventDefault();
                form.handleSubmit();
              }}
            >
              <Tabs defaultValue="character">
                <TabsList>
                  <TabsTrigger value="character">Character</TabsTrigger>
                  <TabsTrigger value="world">World</TabsTrigger>
                </TabsList>

                <TabsContent value="character">
                  <FieldGroup>
                    <form.Field name="name">
                      {(field) => {
                        const isInvalid = field.state.meta.isTouched && !field.state.meta.isValid;
                        return (
                          <Field data-invalid={isInvalid}>
                            <FieldLabel htmlFor={field.name}>Name</FieldLabel>
                            <Input
                              id={field.name}
                              value={field.state.value}
                              onBlur={field.handleBlur}
                              onChange={(e) => field.handleChange(e.target.value)}
                              aria-invalid={isInvalid}
                            />
                            {isInvalid && <FieldError errors={field.state.meta.errors} />}
                          </Field>
                        );
                      }}
                    </form.Field>

                    <form.Field name="age">
                      {(field) => (
                        <Field>
                          <div className="flex items-center justify-between">
                            <FieldLabel htmlFor={field.name}>Age</FieldLabel>
                            <span className="text-muted-foreground text-sm">
                              {field.state.value}
                            </span>
                          </div>
                          <Slider
                            id={field.name}
                            value={[field.state.value]}
                            onValueChange={([next]) => field.handleChange(next)}
                            min={MIN_AGE}
                            max={MAX_AGE}
                            step={1}
                          />
                        </Field>
                      )}
                    </form.Field>

                    <div className="grid grid-cols-2 gap-4">
                      <form.Field name="gender">
                        {(field) => (
                          <Field>
                            <FieldLabel htmlFor={field.name}>Gender</FieldLabel>
                            <Select
                              value={field.state.value}
                              onValueChange={(v) => field.handleChange(v as Gender)}
                            >
                              <SelectTrigger id={field.name}>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {GENDERS.map((g) => (
                                  <SelectItem key={g} value={g}>
                                    {g}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </Field>
                        )}
                      </form.Field>

                      <form.Field name="race">
                        {(field) => (
                          <Field>
                            <FieldLabel htmlFor={field.name}>Race</FieldLabel>
                            <Select
                              value={field.state.value}
                              onValueChange={(v) => field.handleChange(v as Race)}
                            >
                              <SelectTrigger id={field.name}>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {RACES.map((r) => (
                                  <SelectItem key={r} value={r}>
                                    {r}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </Field>
                        )}
                      </form.Field>

                      <form.Field name="playerClass">
                        {(field) => (
                          <Field>
                            <FieldLabel htmlFor={field.name}>Class</FieldLabel>
                            <Select
                              value={field.state.value}
                              onValueChange={(v) => field.handleChange(v as PlayerClass)}
                            >
                              <SelectTrigger id={field.name}>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {PLAYER_CLASSES.map((c) => (
                                  <SelectItem key={c} value={c}>
                                    {c}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </Field>
                        )}
                      </form.Field>
                    </div>

                    <form.Field name="attributes">
                      {(field) => {
                        const spent = Object.values(field.state.value).reduce(
                          (sum, points) => sum + points,
                          0,
                        );
                        const remaining = pointsPerLevel - spent;

                        return (
                          <Field>
                            <AttributeAllocator
                              baseValues={toAttributeValues(generationOptions.data?.baseAttributes)}
                              deltas={field.state.value}
                              minimumValues={MINIMUM_ATTRIBUTE_VALUES}
                              availablePoints={pointsPerLevel}
                              remainingPoints={remaining}
                              onDeltaChange={(attribute, delta) =>
                                field.handleChange({
                                  ...field.state.value,
                                  [attribute]: delta,
                                })
                              }
                            />
                          </Field>
                        );
                      }}
                    </form.Field>
                  </FieldGroup>
                </TabsContent>

                <TabsContent value="world">
                  <FieldGroup>
                    <form.Field name="description">
                      {(field) => (
                        <Field>
                          <FieldLabel htmlFor={field.name}>Description</FieldLabel>
                          <Input
                            id={field.name}
                            value={field.state.value}
                            onChange={(e) => field.handleChange(e.target.value)}
                          />
                        </Field>
                      )}
                    </form.Field>

                    <form.Field name="cityStates">
                      {(field) => (
                        <RangeField
                          label="City States"
                          value={field.state.value}
                          onChange={field.handleChange}
                          min={RACES.length}
                          max={80}
                        />
                      )}
                    </form.Field>
                    <form.Field name="ruralStates">
                      {(field) => (
                        <RangeField
                          label="Rural States"
                          value={field.state.value}
                          onChange={field.handleChange}
                          max={40}
                        />
                      )}
                    </form.Field>
                    <form.Field name="dungeonsPerState">
                      {(field) => (
                        <RangeField
                          label="Dungeons per State"
                          value={field.state.value}
                          onChange={field.handleChange}
                          max={10}
                        />
                      )}
                    </form.Field>
                    <form.Field name="factionMembers">
                      {(field) => (
                        <RangeField
                          label="Faction Members"
                          value={field.state.value}
                          onChange={field.handleChange}
                          max={20}
                        />
                      )}
                    </form.Field>
                    <form.Field name="householdSize">
                      {(field) => (
                        <RangeField
                          label="Household Size"
                          value={field.state.value}
                          onChange={field.handleChange}
                          max={10}
                        />
                      )}
                    </form.Field>

                    <form.Field name="factionCount">
                      {(field) => (
                        <Field orientation="horizontal">
                          <FieldLabel htmlFor={field.name}>Factions</FieldLabel>
                          <NumericStepper
                            value={field.state.value}
                            onChange={field.handleChange}
                            max={30}
                          />
                        </Field>
                      )}
                    </form.Field>
                    <form.Field name="housesPerCity">
                      {(field) => (
                        <Field orientation="horizontal">
                          <FieldLabel htmlFor={field.name}>Houses per City</FieldLabel>
                          <NumericStepper
                            value={field.state.value}
                            onChange={field.handleChange}
                            max={40}
                          />
                        </Field>
                      )}
                    </form.Field>
                  </FieldGroup>
                </TabsContent>
              </Tabs>
            </form>

            <DialogFooter className="flex-col items-stretch gap-2 sm:flex-col">
              <form.Subscribe
                selector={(state) =>
                  [state.values.name, state.values.race, state.values.playerClass] as const
                }
              >
                {([name, race, playerClass]) => (
                  <p className="text-muted-foreground text-xs">
                    {name.trim()
                      ? `${name} the ${race} ${playerClass}`
                      : 'Enter a name to continue'}
                  </p>
                )}
              </form.Subscribe>
              <form.Subscribe
                selector={(state) =>
                  [state.canSubmit, state.isSubmitting, state.isPristine] as const
                }
              >
                {([canSubmit, isSubmitting, isPristine]) => (
                  <Button
                    type="submit"
                    form={FORM_ID}
                    disabled={!canSubmit || isSubmitting || isPristine}
                  >
                    Create World
                  </Button>
                )}
              </form.Subscribe>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

interface RangeFieldProps {
  label: string;
  value: [number, number];
  onChange: (value: [number, number]) => void;
  min?: number;
  max: number;
}

function RangeField({ label, value, onChange, min = 0, max }: RangeFieldProps) {
  return (
    <Field>
      <div className="flex items-center justify-between">
        <FieldLabel>{label}</FieldLabel>
        <span className="text-muted-foreground text-sm">
          {value[0]}–{value[1]}
        </span>
      </div>
      <Slider
        value={value}
        onValueChange={(next) => onChange(next as [number, number])}
        min={min}
        max={max}
        step={1}
      />
    </Field>
  );
}
