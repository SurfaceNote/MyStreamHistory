// Generates the 20 viewer level badges into public/images/levels/{1..20}.svg.
// Run: node scripts/generate-level-icons.mjs
// Levels come in five tiers of four; the tier sets the frame shape and palette,
// the level sets the symbol. Titles live in src/app/i18n/translations.ts (level.N).
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const outDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'images', 'levels');

const TIERS = [
  { name: 'bronze', rim: '#c27a3e', inner: '#3b2414', sym: '#f3cfa6', shape: 'circle' },
  { name: 'silver', rim: '#b9c3cf', inner: '#26303c', sym: '#eef3f8', shape: 'shield' },
  { name: 'gold', rim: '#e6b52f', inner: '#3a2905', sym: '#ffe28c', shape: 'hexagon' },
  { name: 'sapphire', rim: '#4d8fea', inner: '#0e1f3d', sym: '#c4dcff', shape: 'rosette' },
  { name: 'amethyst', rim: '#a874ec', inner: '#22113b', sym: '#f6e3ff', shape: 'crest', accent: '#f2c94c' }
];

const CX = 32;
const CY = 31;

function points(list) {
  return list.map(([x, y]) => `${x.toFixed(2)},${y.toFixed(2)}`).join(' ');
}

function star(cx, cy, outer, inner, n, rotation = -90) {
  const result = [];
  for (let i = 0; i < n * 2; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const a = ((rotation + (i * 180) / n) * Math.PI) / 180;
    result.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
  }
  return points(result);
}

function shapeElement(shape, fill, extra = '') {
  switch (shape) {
    case 'circle':
      return `<circle cx="${CX}" cy="${CY}" r="25" fill="${fill}"${extra}/>`;
    case 'shield':
      return `<path d="M32 4 L55 11 V30 C55 45 45 54 32 59 C19 54 9 45 9 30 V11 Z" fill="${fill}"${extra}/>`;
    case 'hexagon':
      return `<polygon points="32,4 56,17.5 56,44.5 32,58 8,44.5 8,17.5" fill="${fill}"${extra}/>`;
    case 'rosette':
      return `<polygon points="${star(CX, CY, 28, 23.5, 8, -90)}" fill="${fill}"${extra}/>`;
    case 'crest':
      return `<path d="M9 15 L16 5 L24 12 L32 2 L40 12 L48 5 L55 15 V31 C55 45 45 54 32 59 C19 54 9 45 9 31 Z" fill="${fill}"${extra}/>`;
    default:
      throw new Error(`Unknown shape ${shape}`);
  }
}

// One branch of the wreath (left side); the right side is its mirror image.
function laurelBranch(sym) {
  const cx = 32;
  const cy = 29;
  const r = 12.5;
  const parts = [];
  for (let i = 0; i < 6; i++) {
    const deg = 112 + i * 22; // from bottom-left up to top-left
    const a = (deg * Math.PI) / 180;
    const x = cx + r * Math.cos(a);
    const y = cy + r * Math.sin(a);
    // Leaves point along the branch towards the top, tilted outwards.
    const rotation = deg + 90 + 35;
    const lx = x + 2.6 * Math.cos(((rotation) * Math.PI) / 180);
    const ly = y + 2.6 * Math.sin(((rotation) * Math.PI) / 180);
    parts.push(`<ellipse cx="${lx.toFixed(2)}" cy="${ly.toFixed(2)}" rx="3.4" ry="1.6" transform="rotate(${rotation.toFixed(1)} ${lx.toFixed(2)} ${ly.toFixed(2)})" fill="${sym}"/>`);
    const inner = rotation - 70;
    const ix = x + 2.4 * Math.cos((inner * Math.PI) / 180);
    const iy = y + 2.4 * Math.sin((inner * Math.PI) / 180);
    if (i > 0) {
      parts.push(`<ellipse cx="${ix.toFixed(2)}" cy="${iy.toFixed(2)}" rx="2.8" ry="1.3" transform="rotate(${inner.toFixed(1)} ${ix.toFixed(2)} ${iy.toFixed(2)})" fill="${sym}"/>`);
    }
  }
  const start = [cx + r * Math.cos((100 * Math.PI) / 180), cy + r * Math.sin((100 * Math.PI) / 180)];
  const end = [cx + r * Math.cos((236 * Math.PI) / 180), cy + r * Math.sin((236 * Math.PI) / 180)];
  parts.unshift(`<path d="M${start[0].toFixed(2)} ${start[1].toFixed(2)} A${r} ${r} 0 0 1 ${end[0].toFixed(2)} ${end[1].toFixed(2)}" fill="none" stroke="${sym}" stroke-width="1.8" stroke-linecap="round"/>`);
  return parts.join('');
}

function laurel(sym) {
  const branch = laurelBranch(sym);
  return `<g>${branch}</g><g transform="translate(64 0) scale(-1 1)">${branch}</g>`;
}

// Symbols are drawn for a 64×64 box with the visual centre near (32, 29).
const SYMBOLS = {
  // Spy: fedora and domino mask
  1: ({ sym, inner }) => `<path d="M25 20 L27 13 H37 L39 20 Z" fill="${sym}"/><path d="M19 20.5 H45" stroke="${sym}" stroke-width="2.6" stroke-linecap="round"/><path d="M18 27 C25 23 39 23 46 27 C46 33 42 36 37 35 C34.5 34.5 33.5 32 32 32 C30.5 32 29.5 34.5 27 35 C22 36 18 33 18 27 Z" fill="${sym}"/><ellipse cx="25.5" cy="29" rx="3.2" ry="2" fill="${inner}"/><ellipse cx="38.5" cy="29" rx="3.2" ry="2" fill="${inner}"/>`,
  // Neophyte: candle
  2: ({ sym, inner }) => `<path d="M32 13 C35.5 17.5 36.5 20 36.5 22 C36.5 24.6 34.5 26.5 32 26.5 C29.5 26.5 27.5 24.6 27.5 22 C27.5 20 28.5 17.5 32 13 Z" fill="${sym}"/><path d="M32 21.5 C33.2 22.8 33.5 23.6 33.3 24.5" stroke="${inner}" stroke-width="1.4" fill="none" stroke-linecap="round"/><rect x="27" y="28" width="10" height="14" rx="1.5" fill="${sym}"/><path d="M21 44 H43" stroke="${sym}" stroke-width="2.6" stroke-linecap="round"/>`,
  // Adept: open book
  3: ({ sym, inner }) => `<path d="M31 20 C26.5 17 21 16.8 16.5 18.8 V40 C21 38 26.5 38.2 31 41 Z" fill="${sym}"/><path d="M33 20 C37.5 17 43 16.8 47.5 18.8 V40 C43 38 37.5 38.2 33 41 Z" fill="${sym}"/><path d="M20.5 24.5 C23.5 23.5 26 23.6 28 24.6 M20.5 29.5 C23.5 28.5 26 28.6 28 29.6 M20.5 34.5 C23.5 33.5 26 33.6 28 34.6 M36 24.6 C38 23.6 40.5 23.5 43.5 24.5 M36 29.6 C38 28.6 40.5 28.5 43.5 29.5" stroke="${inner}" stroke-width="1.5" fill="none" stroke-linecap="round"/>`,
  // Soldier: sword
  4: ({ sym, inner }) => `<path d="M32 11 L35.5 15.5 V34 H28.5 V15.5 Z" fill="${sym}"/><path d="M32 15 V32" stroke="${inner}" stroke-width="1.2" stroke-linecap="round"/><rect x="22" y="33.5" width="20" height="3.6" rx="1.8" fill="${sym}"/><rect x="30.3" y="37" width="3.4" height="6.5" fill="${sym}"/><circle cx="32" cy="45" r="2.5" fill="${sym}"/>`,
  // Inquisitor: torch
  5: ({ sym, inner }) => `<path d="M32 10 C37 15 40.5 19.5 39.5 24.5 C39 28 36 30 32 30 C28 30 25 28 24.5 24.5 C24 21 26.5 18.5 28 15.5 C29 18.5 30.2 19.8 31.4 20 C30.8 16.5 30.8 13.5 32 10 Z" fill="${sym}"/><path d="M32 22 C34.5 24 35 26 33.8 27.6 C33 28.5 31 28.5 30.2 27.6 C29.2 26.3 30 24 32 22 Z" fill="${inner}"/><path d="M27 31 H37 L34.5 46 H29.5 Z" fill="${sym}"/><path d="M28 34.5 H36" stroke="${inner}" stroke-width="1.4"/>`,
  // Archmage: staff and star
  6: ({ sym }) => `<path d="M23 47 L35 24" stroke="${sym}" stroke-width="3" stroke-linecap="round"/><polygon points="${star(38, 18, 8, 3.4, 5)}" fill="${sym}"/><polygon points="${star(21, 22, 3.6, 1.2, 4, 0)}" fill="${sym}"/><polygon points="${star(44, 35, 3, 1, 4, 0)}" fill="${sym}"/>`,
  // Knight: helmet with plume
  7: ({ sym, inner }) => `<path d="M32 15 C33.5 10.5 38 9 42.5 10.5 C39 11.5 37 13.5 36 16" fill="${sym}"/><path d="M20 43 V29 C20 21 25 15.5 32 15.5 C39 15.5 44 21 44 29 V43 Z" fill="${sym}"/><rect x="23" y="27" width="18" height="3.2" rx="1.6" fill="${inner}"/><path d="M32 32.5 V40.5 M27.5 34.5 V39.5 M36.5 34.5 V39.5" stroke="${inner}" stroke-width="1.6" stroke-linecap="round"/>`,
  // Magister: scroll
  8: ({ sym, inner }) => `<rect x="20" y="17" width="24" height="25" fill="${sym}"/><rect x="17" y="13.5" width="30" height="5.5" rx="2.75" fill="${sym}" stroke="${inner}" stroke-width="1.3"/><rect x="17" y="40" width="30" height="5.5" rx="2.75" fill="${sym}" stroke="${inner}" stroke-width="1.3"/><path d="M24 24 H40 M24 29 H40 M24 34 H34" stroke="${inner}" stroke-width="1.6" stroke-linecap="round"/>`,
  // Admiral: anchor
  9: ({ sym }) => `<g fill="none" stroke="${sym}" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="32" cy="14.5" r="3.2"/><path d="M32 18 V44"/><path d="M25 23 H39"/><path d="M19.5 33 C20.5 40 26 44 32 44 C38 44 43.5 40 44.5 33"/><path d="M16.5 36 L19.5 32.5 L23 35.5 M41 35.5 L44.5 32.5 L47.5 36"/></g>`,
  // Lord: castle tower
  10: ({ sym, inner }) => `<path d="M22 44 V24 H20 V15 H25 V18.5 H29.5 V15 H34.5 V18.5 H39 V15 H44 V24 H42 V44 Z" fill="${sym}"/><path d="M28.5 44 V37.5 C28.5 34.5 35.5 34.5 35.5 37.5 V44 Z" fill="${inner}"/><rect x="30.4" y="25.5" width="3.2" height="5.5" rx="1.6" fill="${inner}"/>`,
  // Ambassador: sealed letter
  11: ({ sym, inner, rim }) => `<rect x="16.5" y="19" width="31" height="22" rx="2" fill="${sym}"/><path d="M17.5 20.5 L32 31.5 L46.5 20.5" stroke="${inner}" stroke-width="1.8" fill="none" stroke-linejoin="round"/><circle cx="32" cy="32" r="5" fill="${rim}" stroke="${inner}" stroke-width="1.5"/><path d="M30 41 L28.5 47 L32 45.5 L35.5 47 L34 41" fill="${rim}"/>`,
  // Councillor: scales
  12: ({ sym }) => `<g stroke="${sym}" stroke-linecap="round" fill="none"><path d="M32 15 V42" stroke-width="2.6"/><path d="M25 43.5 H39" stroke-width="2.8"/><path d="M21 19.5 H43" stroke-width="2.4"/><path d="M21 19.5 L17 30 M21 19.5 L25 30 M43 19.5 L39 30 M43 19.5 L47 30" stroke-width="1.3"/></g><circle cx="32" cy="14" r="2.3" fill="${sym}"/><path d="M16 30 H26 C26 34.5 16 34.5 16 30 Z M38 30 H48 C48 34.5 38 34.5 38 30 Z" fill="${sym}"/>`,
  // Senator: laurel wreath
  13: ({ sym }) => laurel(sym) + `<circle cx="32" cy="43.5" r="2" fill="${sym}"/>`,
  // Jarl: bearded axe
  14: ({ sym, inner }) => `<path d="M23 47 L38.5 14" stroke="${sym}" stroke-width="3.2" stroke-linecap="round"/><path d="M33.5 21 L36 15.5 C42 12.5 49 16 49 24 C49 31 43 35.5 37.5 34 C40 30.5 40 26.5 37 23.5 Z" fill="${sym}"/><path d="M42.5 19 C45.5 21 46.5 25 45 29" stroke="${inner}" stroke-width="1.3" fill="none" stroke-linecap="round"/><path d="M33.5 13.5 L39 16" stroke="${sym}" stroke-width="3" stroke-linecap="round"/>`,
  // Count: coronet
  15: ({ sym, inner }) => `<path d="M18 34 L19.5 24.5 L26 30 L32 21.5 L38 30 L44.5 24.5 L46 34 Z" fill="${sym}"/><circle cx="19.5" cy="22.5" r="2.5" fill="${sym}"/><circle cx="32" cy="19" r="2.7" fill="${sym}"/><circle cx="44.5" cy="22.5" r="2.5" fill="${sym}"/><rect x="17" y="34" width="30" height="7" rx="1.5" fill="${sym}"/><circle cx="25" cy="37.5" r="1.6" fill="${inner}"/><circle cx="32" cy="37.5" r="1.8" fill="${inner}"/><circle cx="39" cy="37.5" r="1.6" fill="${inner}"/>`,
  // Prince: crown
  16: ({ sym, inner }) => `<path d="M18 37 L16.5 19 L24 27 L28 16 L32 25 L36 16 L40 27 L47.5 19 L46 37 Z" fill="${sym}"/><rect x="17.5" y="37" width="29" height="6" rx="1.5" fill="${sym}"/><circle cx="32" cy="32" r="2.4" fill="${inner}"/><circle cx="24.5" cy="33" r="1.6" fill="${inner}"/><circle cx="39.5" cy="33" r="1.6" fill="${inner}"/>`,
  // Emperor: imperial crown with orb cross
  17: ({ sym, inner }) => `<path d="M32 8.5 V16 M28.8 11.3 H35.2" stroke="${sym}" stroke-width="2.4" stroke-linecap="round"/><path d="M19 39 V30 C19 21.5 25 17 32 17 C39 17 45 21.5 45 30 V39 Z" fill="${sym}"/><path d="M32 18 V38 M25.5 20 C23.5 25.5 23.5 32 24.5 38 M38.5 20 C40.5 25.5 40.5 32 39.5 38" stroke="${inner}" stroke-width="1.5" fill="none"/><rect x="16.5" y="38" width="31" height="7" rx="1.5" fill="${sym}"/><circle cx="23" cy="41.5" r="1.6" fill="${inner}"/><circle cx="32" cy="41.5" r="1.9" fill="${inner}"/><circle cx="41" cy="41.5" r="1.6" fill="${inner}"/>`,
  // Patriarch: crozier
  18: ({ sym, inner }) => `<path d="M29.5 47 V22 C29.5 14 42.5 13 42.5 21.5 C42.5 27 35.5 28 34.5 22.5" stroke="${sym}" stroke-width="3.2" fill="none" stroke-linecap="round"/><circle cx="29.5" cy="31" r="3.4" fill="${sym}"/><circle cx="29.5" cy="31" r="1.3" fill="${inner}"/><path d="M25.5 37 H33.5" stroke="${sym}" stroke-width="2.2" stroke-linecap="round"/>`,
  // Saint: figure with halo
  19: ({ sym }) => `<ellipse cx="32" cy="14.5" rx="9.5" ry="3.4" fill="none" stroke="${sym}" stroke-width="2.4"/><circle cx="32" cy="24.5" r="5.6" fill="${sym}"/><path d="M19.5 45 C19.5 36 25 31.5 32 31.5 C39 31.5 44.5 36 44.5 45 Z" fill="${sym}"/>`,
  // Illuminate: eye in a radiant triangle
  20: ({ sym, inner, accent }) => `<g stroke="${accent}" stroke-width="1.8" stroke-linecap="round"><path d="M32 7 V10.5 M21.5 11.5 L23.5 14 M42.5 11.5 L40.5 14 M15.5 20 L18.5 21.5 M48.5 20 L45.5 21.5"/></g><path d="M32 13.5 L47.5 42 H16.5 Z" fill="${sym}"/><path d="M23.5 34 C27.5 28.8 36.5 28.8 40.5 34 C36.5 39.2 27.5 39.2 23.5 34 Z" fill="${inner}"/><circle cx="32" cy="34" r="2.8" fill="${accent}"/>`
};

function badge(level) {
  const tier = TIERS[Math.floor((level - 1) / 4)];
  const colors = { ...tier, accent: tier.accent ?? tier.sym };
  const innerShape = shapeElement(tier.shape, tier.inner, ` transform="translate(${CX} ${CY}) scale(0.84) translate(-${CX} -${CY})"`);
  const accentRing = tier.accent
    ? shapeElement(tier.shape, 'none', ` stroke="${tier.accent}" stroke-width="1.4" transform="translate(${CX} ${CY}) scale(0.92) translate(-${CX} -${CY})"`)
    : '';
  const fontSize = level >= 10 ? 9 : 10;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64" role="img" aria-labelledby="t">
<title id="t">Level ${level}</title>
${shapeElement(tier.shape, tier.rim)}
${accentRing}
${innerShape}
${SYMBOLS[level](colors)}
<rect x="21" y="50.5" width="22" height="12" rx="6" fill="${tier.rim}" stroke="${tier.inner}" stroke-width="1.5"/>
<text x="32" y="59.6" text-anchor="middle" font-family="Inter, Segoe UI, Arial, sans-serif" font-size="${fontSize}" font-weight="700" fill="${tier.inner}">${level}</text>
</svg>
`;
}

mkdirSync(outDir, { recursive: true });
for (let level = 1; level <= 20; level++) {
  writeFileSync(join(outDir, `${level}.svg`), badge(level));
}
console.log(`Wrote 20 level badges to ${outDir}`);
