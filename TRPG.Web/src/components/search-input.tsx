import { Search } from 'lucide-react';

interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
}

export function SearchInput({ value, onChange, placeholder }: SearchInputProps) {
  return (
    <div className="border-input bg-card mb-2 flex h-[34px] items-center gap-2 rounded-md border px-2.5 shadow-sm">
      <Search className="text-muted-foreground h-3.5 w-3.5 shrink-0" />
      <input
        type="text"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="placeholder:text-muted-foreground flex-1 bg-transparent text-sm outline-none"
      />
    </div>
  );
}
