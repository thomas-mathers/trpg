import { Search } from 'lucide-react';

import { cn } from '@/lib/utils';

interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  ariaLabel?: string;
}

export function SearchInput({
  value,
  onChange,
  placeholder = 'Search...',
  className,
  ariaLabel,
}: SearchInputProps) {
  return (
    <div
      className={cn(
        'border-input bg-background flex h-[34px] items-center gap-2 rounded-md border px-2.5 shadow-sm',
        className,
      )}
    >
      <Search className="text-muted-foreground h-3.5 w-3.5 shrink-0" />
      <input
        type="text"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        aria-label={ariaLabel}
        className="placeholder:text-muted-foreground flex-1 bg-transparent text-sm outline-none"
      />
    </div>
  );
}
