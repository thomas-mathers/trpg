import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import type { AbilityCategory, AbilitySummary, Skill } from '@/api/client';
import {
  getCreatureAbilitiesOptions,
  getPlayerFightAbilitiesOptions,
  getPlayerFightAbilitiesQueryKey,
} from '@/api/client';
import { SearchInput } from '@/components/search-input';
import { EmptyNote } from '@/features/combat/components/empty-note';
import { PickerHeader } from '@/features/combat/components/picker-header';
import { gameEventBus } from '@/lib/game-event-bus';

const SEARCH_THRESHOLD = 6;

const SKILL_ORDER: Skill[] = [
  'Melee',
  'Unarmed',
  'Sneak',
  'Destruction',
  'Illusion',
  'Archery',
  'Restoration',
  'Alteration',
  'General',
  'Blocking',
];

export interface AbilityPickerProps {
  playerId: string;
  category: AbilityCategory;
  onBack: () => void;
  onChoose: (ability: AbilitySummary) => void;
  showHeader?: boolean;
}

export function AbilityPicker({
  playerId,
  category,
  onBack,
  onChoose,
  showHeader = true,
}: AbilityPickerProps) {
  const [query, setQuery] = useState('');
  const queryClient = useQueryClient();

  const abilitiesQuery = useQuery(getCreatureAbilitiesOptions({ path: { creatureId: playerId } }));
  const availabilityQuery = useQuery(getPlayerFightAbilitiesOptions({ path: { playerId } }));

  useEffect(() => {
    const key = getPlayerFightAbilitiesQueryKey({ path: { playerId } });
    const invalidate = () => queryClient.invalidateQueries({ queryKey: key });
    const offUpdated = gameEventBus.on('CombatUpdated', invalidate);
    const offStarted = gameEventBus.on('CombatStarted', invalidate);
    return () => {
      offUpdated();
      offStarted();
    };
  }, [playerId, queryClient]);

  const availabilityByName = new Map((availabilityQuery.data ?? []).map((a) => [a.name, a]));
  const candidates = (abilitiesQuery.data ?? []).filter((a) => a.category === category);

  const normalizedQuery = query.trim().toLowerCase();
  const filtered = normalizedQuery
    ? candidates.filter((a) => a.name.toLowerCase().includes(normalizedQuery))
    : candidates;

  return (
    <div className="p-2.5">
      {showHeader && (
        <PickerHeader
          className="pb-2"
          onBack={onBack}
          title={`Choose ${category === 'Offensive' ? 'an attack' : 'a support ability'}`}
        />
      )}

      {candidates.length > SEARCH_THRESHOLD && (
        <SearchInput value={query} onChange={setQuery} placeholder="Search abilities…" />
      )}

      {candidates.length === 0 ? (
        <EmptyNote>No abilities of that type.</EmptyNote>
      ) : filtered.length === 0 ? (
        <EmptyNote>No matches for &ldquo;{query.trim()}&rdquo;.</EmptyNote>
      ) : (
        groupBySkill(filtered).map(([skill, entries]) => (
          <div key={skill ?? 'all'}>
            {skill && (
              <p className="text-muted-foreground mt-2.5 mb-1 text-[10px] font-semibold tracking-wider uppercase">
                {skill}
              </p>
            )}
            <div className="flex flex-col gap-1.5">
              {entries.map((ability) => {
                const availability = availabilityByName.get(ability.name);
                const usable = availability?.isUsable ?? true;
                return (
                  <button
                    key={ability.name}
                    type="button"
                    disabled={!usable}
                    onClick={() => onChoose(ability)}
                    className="border-border bg-card hover:bg-accent flex items-end justify-between gap-2.5 rounded-md border px-2.5 py-2 text-left shadow-sm disabled:pointer-events-none disabled:opacity-50"
                  >
                    <span className="min-w-0">
                      <span className="block text-[13px] font-medium">{ability.name}</span>
                      <span className="text-muted-foreground block text-[11px] leading-snug">
                        {usable ? ability.description : availability?.reason}
                      </span>
                    </span>
                    <span className="flex shrink-0 gap-2 text-[11px] tabular-nums">
                      {Number(ability.apCost) > 0 && (
                        <span className="text-stamina">AP {ability.apCost}</span>
                      )}
                      {Number(ability.mpCost) > 0 && (
                        <span className="text-mp">MP {ability.mpCost}</span>
                      )}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        ))
      )}
    </div>
  );
}

function groupBySkill(list: AbilitySummary[]): Array<[Skill | null, AbilitySummary[]]> {
  const bySkill = new Map<Skill, AbilitySummary[]>();
  for (const entry of list) {
    const bucket = bySkill.get(entry.skill) ?? [];
    bucket.push(entry);
    bySkill.set(entry.skill, bucket);
  }

  if (bySkill.size <= 1) {
    return [[null, list]];
  }

  return SKILL_ORDER.filter((skill) => bySkill.has(skill)).map((skill) => [
    skill,
    bySkill.get(skill)!,
  ]);
}
