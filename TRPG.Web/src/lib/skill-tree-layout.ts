import type { AbilitySummary } from '@/api/client';

export interface SkillTreeNode {
  ability: AbilitySummary;
  column: number;
  row: number;
}

export interface SkillTreeEdge {
  from: string;
  to: string;
}

export interface SkillTreeLayout {
  nodes: SkillTreeNode[];
  edges: SkillTreeEdge[];
  columnCount: number;
}

export function layoutSkillTree(abilities: AbilitySummary[]): SkillTreeLayout {
  const byName = new Map(abilities.map((ability) => [ability.name, ability]));

  const childrenByName = new Map<string, string[]>();
  for (const ability of abilities) {
    for (const prerequisite of ability.prerequisites) {
      if (!byName.has(prerequisite)) continue;
      const children = childrenByName.get(prerequisite) ?? [];
      children.push(ability.name);
      childrenByName.set(prerequisite, children);
    }
  }

  const distanceToLeafByName = new Map<string, number>();
  function distanceToLeaf(name: string): number {
    const cached = distanceToLeafByName.get(name);
    if (cached !== undefined) return cached;

    const children = childrenByName.get(name) ?? [];
    const distance = children.length === 0 ? 0 : 1 + Math.max(...children.map(distanceToLeaf));
    distanceToLeafByName.set(name, distance);
    return distance;
  }

  const distances = abilities.map((ability) => distanceToLeaf(ability.name));
  const maxDistance = distances.length === 0 ? 0 : Math.max(...distances);

  const sortedAbilities = [...abilities].sort((a, b) => a.name.localeCompare(b.name));
  const rowByColumn = new Map<number, number>();
  const nodes: SkillTreeNode[] = sortedAbilities.map((ability) => {
    const column = maxDistance - distanceToLeaf(ability.name);
    const row = rowByColumn.get(column) ?? 0;
    rowByColumn.set(column, row + 1);
    return { ability, column, row };
  });

  const edges: SkillTreeEdge[] = abilities.flatMap((ability) =>
    ability.prerequisites
      .filter((prerequisite) => byName.has(prerequisite))
      .map((prerequisite) => ({ from: prerequisite, to: ability.name })),
  );

  return { nodes, edges, columnCount: maxDistance + 1 };
}
