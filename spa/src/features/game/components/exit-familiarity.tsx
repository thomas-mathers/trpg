// Without this an exit list is a row of names with nothing to say which of them you have already
// walked, which is how a dungeon turns into unintentional backtracking.
export function ExitFamiliarity({
  isVisited,
  isWayBack,
}: {
  isVisited: boolean;
  isWayBack: boolean;
}) {
  if (isWayBack) {
    return <span className="text-muted-foreground shrink-0 text-xs">back</span>;
  }

  if (isVisited) {
    return <span className="text-muted-foreground shrink-0 text-xs">explored</span>;
  }

  return null;
}
