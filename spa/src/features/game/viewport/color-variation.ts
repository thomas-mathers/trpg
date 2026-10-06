import { Color } from 'three';

export function stringHash(text: string) {
  let hash = 0;
  for (const character of text) hash = (hash * 31 + character.charCodeAt(0)) | 0;
  return Math.abs(hash);
}

function unit(hash: number, salt: number) {
  const mixed = Math.imul(hash ^ Math.imul(salt + 1, 0x9e3779b1), 0x85ebca6b);
  return (((mixed ^ (mixed >>> 15)) >>> 0) % 1000) / 500 - 1;
}

export function varyColor(color: string, id: string, spread: number) {
  const hash = stringHash(id);
  const hsl = { h: 0, s: 0, l: 0 };
  new Color(color).getHSL(hsl);
  return `#${new Color()
    .setHSL(
      (hsl.h + unit(hash, 0) * 0.03 * spread + 1) % 1,
      Math.min(1, Math.max(0, hsl.s + unit(hash, 1) * 0.12 * spread)),
      Math.min(1, Math.max(0, hsl.l + unit(hash, 2) * 0.08 * spread)),
    )
    .getHexString()}`;
}

export function shadeColor(color: string, lightness: number) {
  const shaded = new Color(color);
  shaded.offsetHSL(0, 0, lightness);
  return `#${shaded.getHexString()}`;
}
