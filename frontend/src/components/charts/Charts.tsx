import { useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/Card";
import { Segmented } from "@/components/ui/PageHeader";
import { cn } from "@/lib/utils";

export interface ChartSeries {
  key: string;
  label: string;
  color: string;
}

function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(0);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(Math.floor(entry.contentRect.width)));
    observer.observe(el);
    setWidth(Math.floor(el.getBoundingClientRect().width));
    return () => observer.disconnect();
  }, []);

  return [ref, width] as const;
}

function niceScale(max: number, count = 4) {
  if (max <= 0) return { max: count, ticks: Array.from({ length: count + 1 }, (_, i) => i) };
  const rough = max / count;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= rough) ?? rough;
  const top = Math.ceil(max / step) * step;
  return { max: top, ticks: Array.from({ length: Math.round(top / step) + 1 }, (_, i) => i * step) };
}

function roundedTopRect(x: number, y: number, w: number, h: number, r: number) {
  const radius = Math.min(r, h, w / 2);
  return `M${x},${y + h} V${y + radius} Q${x},${y} ${x + radius},${y} H${x + w - radius} Q${x + w},${y} ${x + w},${y + radius} V${y + h} Z`;
}

function moveWithKeys(e: KeyboardEvent, active: number | null, count: number, set: (i: number | null) => void) {
  if (e.key === "ArrowRight") set(Math.min(count - 1, (active ?? -1) + 1));
  else if (e.key === "ArrowLeft") set(Math.max(0, (active ?? count) - 1));
  else if (e.key === "Escape") set(null);
  else return;
  e.preventDefault();
}

function Tooltip({ x, width, title, rows }: { x: number; width: number; title: string; rows: { color?: string; label: string; value: string }[] }) {
  const left = Math.min(Math.max(x, 90), width - 90);
  return (
    <div
      role="status"
      className="pointer-events-none absolute top-0 z-10 min-w-36 -translate-x-1/2 rounded-xl border border-line bg-surface px-3 py-2 shadow-lg shadow-black/10"
      style={{ left }}
    >
      <p className="mb-1 text-xs text-muted">{title}</p>
      {rows.map((r) => (
        <p key={r.label} className="flex items-center gap-2 text-xs whitespace-nowrap">
          {r.color && <span className="h-0.5 w-3 shrink-0 rounded-full" style={{ backgroundColor: r.color }} />}
          <span className="text-sm font-semibold text-fg tabular-nums">{r.value}</span>
          <span className="text-muted">{r.label}</span>
        </p>
      ))}
    </div>
  );
}

export function ChartCard({
  title,
  subtitle,
  table,
  children,
  className,
}: {
  title: string;
  subtitle: string;
  table: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  const { t } = useTranslation();
  const [view, setView] = useState<"chart" | "table">("chart");

  return (
    <Card className={cn("p-6", className)}>
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{title}</h2>
          <p className="text-sm text-muted">{subtitle}</p>
        </div>
        <Segmented
          label={title}
          value={view}
          onChange={setView}
          options={[
            { value: "chart", label: t("admin.insights.chart") },
            { value: "table", label: t("admin.insights.table") },
          ]}
        />
      </div>
      {view === "chart" ? children : table}
    </Card>
  );
}

export function ChartTable({ head, rows }: { head: string[]; rows: (string | number)[][] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-xs text-muted">
            {head.map((h, i) => (
              <th key={h} className={cn("py-2 font-medium", i > 0 && "text-right")}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((row) => (
            <tr key={String(row[0])}>
              {row.map((cell, i) => (
                <td key={i} className={cn("py-2", i > 0 && "text-right tabular-nums")}>
                  {cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const AXIS = { top: 28, right: 12, bottom: 28, left: 56 };

export function AreaTrend({
  points,
  series,
  format,
  height = 240,
}: {
  points: { key: string; label: string; title: string; value: number }[];
  series: ChartSeries;
  format: (value: number) => string;
  height?: number;
}) {
  const [ref, width] = useWidth<HTMLDivElement>();
  const [active, setActive] = useState<number | null>(null);
  const plotW = Math.max(0, width - AXIS.left - AXIS.right);
  const plotH = height - AXIS.top - AXIS.bottom;
  const scale = niceScale(Math.max(...points.map((p) => p.value), 0));
  const x = (i: number) => AXIS.left + (points.length > 1 ? (i * plotW) / (points.length - 1) : plotW / 2);
  const y = (v: number) => AXIS.top + plotH * (1 - v / scale.max);

  const line = points.map((p, i) => `${i ? "L" : "M"}${x(i)},${y(p.value)}`).join(" ");
  const area = `${line} L${x(points.length - 1)},${y(0)} L${x(0)},${y(0)} Z`;
  const last = points.length - 1;

  const pick = (clientX: number, rect: DOMRect) => {
    const rel = clientX - rect.left - AXIS.left;
    setActive(Math.max(0, Math.min(last, Math.round((rel / Math.max(plotW, 1)) * last))));
  };

  return (
    <div ref={ref} className="relative">
      {width > 0 && (
        <svg
          width={width}
          height={height}
          role="img"
          aria-label={`${series.label}: ${points.map((p) => `${p.title} ${format(p.value)}`).join(", ")}`}
          tabIndex={0}
          className="rounded-lg outline-none focus-visible:ring-4 focus-visible:ring-ring"
          onPointerMove={(e) => pick(e.clientX, e.currentTarget.getBoundingClientRect())}
          onPointerLeave={() => setActive(null)}
          onFocus={() => setActive(last)}
          onBlur={() => setActive(null)}
          onKeyDown={(e) => moveWithKeys(e, active, points.length, setActive)}
        >
          {scale.ticks.map((tick) => (
            <g key={tick}>
              <line x1={AXIS.left} x2={width - AXIS.right} y1={y(tick)} y2={y(tick)} stroke="var(--border)" strokeWidth={1} />
              <text x={AXIS.left - 8} y={y(tick)} dy="0.32em" textAnchor="end" className="fill-muted text-[11px] tabular-nums">
                {format(tick)}
              </text>
            </g>
          ))}
          {points.map((p, i) => (
            <text key={p.key} x={x(i)} y={height - 8} textAnchor="middle" className="fill-muted text-[11px]">
              {p.label}
            </text>
          ))}
          <path d={area} fill={series.color} fillOpacity={0.1} />
          <path d={line} fill="none" stroke={series.color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
          {active !== null && <line x1={x(active)} x2={x(active)} y1={AXIS.top} y2={y(0)} stroke="var(--muted)" strokeWidth={1} />}
          <circle cx={x(active ?? last)} cy={y(points[active ?? last]?.value ?? 0)} r={4} fill={series.color} stroke="var(--surface)" strokeWidth={2} />
          {active === null && points.length > 0 && (
            <text x={x(last)} y={y(points[last].value) - 12} textAnchor="end" className="fill-fg text-xs font-semibold tabular-nums">
              {format(points[last].value)}
            </text>
          )}
        </svg>
      )}
      {active !== null && points[active] && (
        <Tooltip
          x={x(active)}
          width={width}
          title={points[active].title}
          rows={[{ color: series.color, label: series.label, value: format(points[active].value) }]}
        />
      )}
    </div>
  );
}

export function StackedColumns({
  columns,
  series,
  totalLabel,
  height = 260,
}: {
  columns: { key: string; label: string; title: string; values: Record<string, number> }[];
  series: ChartSeries[];
  totalLabel: string;
  height?: number;
}) {
  const [ref, width] = useWidth<HTMLDivElement>();
  const [active, setActive] = useState<number | null>(null);
  const plotW = Math.max(0, width - AXIS.left - AXIS.right);
  const plotH = height - AXIS.top - AXIS.bottom;
  const totals = columns.map((c) => series.reduce((sum, s) => sum + (c.values[s.key] ?? 0), 0));
  const scale = niceScale(Math.max(...totals, 0));
  const band = columns.length ? plotW / columns.length : 0;
  const barW = Math.min(24, band * 0.6);
  const y = (v: number) => AXIS.top + plotH * (1 - v / scale.max);
  const bandX = (i: number) => AXIS.left + i * band;
  const labelEvery = Math.max(1, Math.ceil(52 / Math.max(band, 1)));

  return (
    <div>
      <ul className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
        {series.map((s) => (
          <li key={s.key} className="flex items-center gap-1.5">
            <span className="size-2.5 rounded-sm" style={{ backgroundColor: s.color }} />
            {s.label}
          </li>
        ))}
      </ul>
      <div ref={ref} className="relative">
        {width > 0 && (
          <svg
            width={width}
            height={height}
            role="img"
            aria-label={columns.map((c, i) => `${c.title}: ${totals[i]}`).join(", ")}
            tabIndex={0}
            className="rounded-lg outline-none focus-visible:ring-4 focus-visible:ring-ring"
            onPointerLeave={() => setActive(null)}
            onFocus={() => setActive(columns.length - 1)}
            onBlur={() => setActive(null)}
            onKeyDown={(e) => moveWithKeys(e, active, columns.length, setActive)}
          >
            {scale.ticks.map((tick) => (
              <g key={tick}>
                <line x1={AXIS.left} x2={width - AXIS.right} y1={y(tick)} y2={y(tick)} stroke="var(--border)" strokeWidth={1} />
                <text x={AXIS.left - 8} y={y(tick)} dy="0.32em" textAnchor="end" className="fill-muted text-[11px] tabular-nums">
                  {tick}
                </text>
              </g>
            ))}
            {columns.map((c, i) => {
              const cx = bandX(i) + band / 2;
              let base = 0;
              const visible = series.filter((s) => (c.values[s.key] ?? 0) > 0);
              return (
                <g key={c.key} opacity={active === null || active === i ? 1 : 0.55} onPointerEnter={() => setActive(i)}>
                  <rect x={bandX(i)} y={AXIS.top} width={band} height={plotH} fill="transparent" />
                  {visible.map((s, si) => {
                    const value = c.values[s.key];
                    const top = y(base + value);
                    const bottom = y(base) - (si > 0 ? 2 : 0);
                    base += value;
                    const h = Math.max(0, bottom - top);
                    const isTop = si === visible.length - 1;
                    return isTop ? (
                      <path key={s.key} d={roundedTopRect(cx - barW / 2, top, barW, h, 4)} fill={s.color} />
                    ) : (
                      <rect key={s.key} x={cx - barW / 2} y={top} width={barW} height={h} fill={s.color} />
                    );
                  })}
                  {(columns.length - 1 - i) % labelEvery === 0 && (
                    <text x={cx} y={height - 8} textAnchor="middle" className="fill-muted text-[11px]">
                      {c.label}
                    </text>
                  )}
                </g>
              );
            })}
          </svg>
        )}
        {active !== null && columns[active] && (
          <Tooltip
            x={bandX(active) + band / 2}
            width={width}
            title={columns[active].title}
            rows={[
              ...series.map((s) => ({ color: s.color, label: s.label, value: String(columns[active].values[s.key] ?? 0) })),
              { label: totalLabel, value: String(totals[active]) },
            ]}
          />
        )}
      </div>
    </div>
  );
}

export function BarList({
  rows,
  color,
}: {
  rows: { key: string; label: string; value: number; display: string; detail: string }[];
  color: string;
}) {
  return (
    <ul className="space-y-3">
      {rows.map((r) => (
        <li key={r.key} tabIndex={0} className="group relative grid grid-cols-[minmax(5rem,9rem)_1fr] items-center gap-3 rounded-lg outline-none focus-visible:ring-4 focus-visible:ring-ring">
          <span className="truncate text-sm">{r.label}</span>
          <div className="flex items-center gap-2">
            <div
              className="h-5 rounded-r transition-opacity group-hover:opacity-80"
              style={{ width: `calc((100% - 3.5rem) * ${Math.max(r.value, 0.01)})`, backgroundColor: color }}
            />
            <span className="text-sm font-semibold tabular-nums">{r.display}</span>
          </div>
          <span className="pointer-events-none absolute -top-8 left-36 z-10 hidden rounded-lg border border-line bg-surface px-2.5 py-1 text-xs whitespace-nowrap text-muted shadow-lg shadow-black/10 group-hover:block group-focus-visible:block">
            {r.detail}
          </span>
        </li>
      ))}
    </ul>
  );
}
