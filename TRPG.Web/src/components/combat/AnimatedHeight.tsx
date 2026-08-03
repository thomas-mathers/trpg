import { useLayoutEffect, useRef, type ReactNode } from 'react';

const prefersReducedMotion =
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

interface AnimatedHeightProps {
  children: ReactNode;
}

// Animates the wrapped content's container growing/shrinking to fit whenever
// its children change (e.g. the action bar swapping for the ability list) —
// clamp to the outgoing height in a layout effect (before paint, so there's
// no flash of the unclamped new content), then let it grow/shrink to its own
// natural height on the next frame.
export function AnimatedHeight({ children }: AnimatedHeightProps) {
  const outerRef = useRef<HTMLDivElement>(null);
  const innerRef = useRef<HTMLDivElement>(null);
  const previousHeight = useRef<number | null>(null);

  useLayoutEffect(() => {
    const outer = outerRef.current;
    const inner = innerRef.current;
    if (!outer || !inner) {
      return;
    }

    const newHeight = inner.getBoundingClientRect().height;

    if (
      prefersReducedMotion ||
      previousHeight.current === null ||
      previousHeight.current === newHeight
    ) {
      previousHeight.current = newHeight;
      return;
    }

    outer.style.height = `${previousHeight.current}px`;
    outer.style.overflow = 'hidden';
    void outer.offsetHeight; // force the browser to commit the clamped height as a real frame

    const raf = requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        outer.style.height = `${newHeight}px`;
      });
    });

    const handleTransitionEnd = () => {
      outer.style.height = 'auto';
      outer.style.overflow = '';
    };
    outer.addEventListener('transitionend', handleTransitionEnd, { once: true });

    previousHeight.current = newHeight;

    return () => {
      cancelAnimationFrame(raf);
      outer.removeEventListener('transitionend', handleTransitionEnd);
    };
  });

  return (
    <div
      ref={outerRef}
      className="transition-[height] duration-300 ease-[cubic-bezier(0.22,1,0.36,1)]"
    >
      <div ref={innerRef}>{children}</div>
    </div>
  );
}
