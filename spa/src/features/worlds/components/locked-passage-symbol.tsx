export function LockedPassageSymbol({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 36 36" role="img" aria-label="Locked passage">
      <path
        d="M 15 3 V 33 M 21 3 V 33 M 12 8 H 24 M 12 18 H 24 M 12 28 H 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.5"
      />
    </svg>
  );
}
