#!/usr/bin/env python3
# Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
"""
Compare AccessCalibrate study-mode CSV files by session label.

    python plot_study.py study.csv [more.csv ...] [-o study.png]

Every row is one calibration (written by the package's study mode). Rows from all files are
grouped by their "label" column (e.g. "normal", "non-dominant hand", "gloves"). The script
prints a summary table and draws one small chart per measurement: one dot per session, a
short line at the median of each label, and the rule threshold as a dashed line.

Needs Python 3.8+ and matplotlib (pip install matplotlib) for the chart; the table works without it.
Thresholds drawn are the package's GAMEPAD defaults - hypotheses, not clinical values.
"""

import argparse
import csv
import statistics
import sys
from collections import OrderedDict

# (column, title, unit, threshold, rule text) - thresholds = RulesConfig gamepad defaults.
METRICS = [
    ("reaction_median_ms", "Reaction (median)", "ms", 500.0, "rule: above 500 ms (450 + 50 latency margin)"),
    ("hold_max_s", "Longest comfortable hold", "s", 2.0, "rule: below 2 s"),
    ("taps_per_s", "Repeated presses", "presses / s", 4.0, "rule: below 4 / s"),
    ("stability_spread", "Steadiness spread", "× circle radius", 0.15, "rule: above 0.15"),
    ("buttons_unpressed_count", "Buttons not pressed", "buttons", 1.0, "rule: 1 or more"),
]

CONFIDENCE_COLUMN = {
    "reaction_median_ms": "reaction_confidence",
    "hold_max_s": "hold_confidence",
    "taps_per_s": "taps_confidence",
    "stability_spread": "stability_confidence",
    "buttons_unpressed_count": "buttons_confidence",
}

NO_LABEL = "(no label)"


def load_rows(paths):
    """Reads every file; returns a list of dict rows (strings), each with a 'source' key."""
    rows = []
    for path in paths:
        with open(path, newline="", encoding="utf-8-sig") as f:
            for row in csv.DictReader(f):
                row["source"] = path
                unpressed = (row.get("buttons_unpressed") or "").strip()
                usable = row.get("buttons_confidence") not in ("", "Skipped", None)
                row["buttons_unpressed_count"] = (
                    str(len([b for b in unpressed.split(";") if b])) if usable else "")
                rows.append(row)
    return rows


def value(row, column):
    """The numeric value of a cell, or None if empty / skipped / not a number."""
    if row.get(CONFIDENCE_COLUMN.get(column, ""), "") == "Skipped":
        return None
    try:
        return float(row.get(column, ""))
    except (TypeError, ValueError):
        return None


def group_by_label(rows):
    """OrderedDict label -> rows, in the order labels first appear."""
    groups = OrderedDict()
    for row in rows:
        label = (row.get("label") or "").strip() or NO_LABEL
        groups.setdefault(label, []).append(row)
    return groups


def summary(groups):
    """List of (label, sessions, {column: median or None})."""
    out = []
    for label, rows in groups.items():
        medians = {}
        for column, *_ in METRICS:
            values = [v for v in (value(r, column) for r in rows) if v is not None]
            medians[column] = statistics.median(values) if values else None
        out.append((label, len(rows), medians))
    return out


def format_table(groups):
    headers = ["label", "sessions"] + [f"{title} ({unit})" for _, title, unit, _, _ in METRICS]
    lines = [headers]
    for label, n, medians in summary(groups):
        cells = [label, str(n)]
        for column, *_ in METRICS:
            m = medians[column]
            cells.append("-" if m is None else f"{m:.3g}")
        lines.append(cells)
    widths = [max(len(line[i]) for line in lines) for i in range(len(headers))]
    text = ["  ".join(c.ljust(w) for c, w in zip(line, widths)) for line in lines]
    text.insert(1, "  ".join("-" * w for w in widths))
    return "\n".join(text) + "\n(medians per label; '-' = no usable sessions)"


# Reference palette (dataviz skill): one series hue, text tokens, recessive grid.
SERIES = "#2a78d6"
TEXT_PRIMARY = "#0b0b0b"
TEXT_SECONDARY = "#52514e"
TEXT_MUTED = "#8a8985"
GRID = "#e6e5e1"
SURFACE = "#fcfcfb"


def plot(groups, out_path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.lines import Line2D
    from matplotlib.ticker import MaxNLocator

    labels = list(groups.keys())
    fig, axes = plt.subplots(2, 3, figsize=(15, 8.5), facecolor=SURFACE)
    fig.suptitle("AccessCalibrate study: sessions by label", fontsize=16, color=TEXT_PRIMARY,
                 x=0.03, ha="left", y=0.98)
    fig.text(0.03, 0.935, "One dot per calibration. Hollow dot = measurement marked uncertain. "
             "Skipped measurements are left out.", fontsize=10.5, color=TEXT_SECONDARY)

    for ax, (column, title, unit, threshold, rule) in zip(axes.flat, METRICS):
        ax.set_facecolor(SURFACE)
        for spine in ("top", "right"):
            ax.spines[spine].set_visible(False)
        for spine in ("left", "bottom"):
            ax.spines[spine].set_color(GRID)
        ax.tick_params(colors=TEXT_SECONDARY, labelsize=10, length=0)
        ax.yaxis.grid(True, color=GRID, linewidth=0.8)
        ax.set_axisbelow(True)
        ax.set_title(title, loc="left", fontsize=12.5, color=TEXT_PRIMARY, pad=22)
        # Rule under the title (not on the line, where it would collide with dots).
        ax.text(0, 1.02, "dashed line = " + rule, transform=ax.transAxes, fontsize=9.5,
                color=TEXT_SECONDARY, va="bottom")
        ax.set_ylabel(unit, color=TEXT_SECONDARY, fontsize=10)

        for x, label in enumerate(labels):
            rows = groups[label]
            values = []
            for i, row in enumerate(rows):
                v = value(row, column)
                if v is None:
                    continue
                values.append(v)
                jitter = ((i % 5) - 2) * 0.06          # deterministic spread so dots don't overlap
                uncertain = row.get(CONFIDENCE_COLUMN[column]) == "Uncertain"
                ax.plot(x + jitter, v, "o", markersize=8, markeredgewidth=2,
                        markerfacecolor=SURFACE if uncertain else SERIES,
                        markeredgecolor=SERIES, zorder=3)
            if values:
                m = statistics.median(values)
                ax.plot([x - 0.28, x + 0.28], [m, m], color=TEXT_PRIMARY, linewidth=2,
                        solid_capstyle="round", zorder=4)

        ax.axhline(threshold, color=TEXT_MUTED, linewidth=1.2, linestyle=(0, (4, 3)), zorder=2)
        ax.set_xticks(range(len(labels)))
        ax.set_xticklabels(labels, rotation=20 if len(labels) > 3 else 0,
                           ha="right" if len(labels) > 3 else "center", color=TEXT_PRIMARY)
        ax.set_xlim(-0.6, len(labels) - 0.4)
        top = ax.get_ylim()[1]
        ax.set_ylim(bottom=-0.04 * top)      # room so dots at 0 aren't cut by the axis
        if column == "buttons_unpressed_count":
            ax.yaxis.set_major_locator(MaxNLocator(integer=True))

    # Last cell: legend + note (identity is never colour alone: shape/fill + labels).
    legend_ax = axes.flat[-1]
    legend_ax.axis("off")
    handles = [
        Line2D([], [], marker="o", linestyle="", markersize=8, markerfacecolor=SERIES,
               markeredgecolor=SERIES, markeredgewidth=2, label="session"),
        Line2D([], [], marker="o", linestyle="", markersize=8, markerfacecolor=SURFACE,
               markeredgecolor=SERIES, markeredgewidth=2, label="session, uncertain"),
        Line2D([], [], color=TEXT_PRIMARY, linewidth=2, label="median per label"),
        Line2D([], [], color=TEXT_MUTED, linewidth=1.2, linestyle=(0, (4, 3)),
               label="rule threshold (gamepad default)"),
    ]
    legend_ax.legend(handles=handles, loc="upper left", frameon=False, fontsize=11,
                     labelcolor=TEXT_PRIMARY)
    legend_ax.text(0.0, 0.32, "Thresholds are hypotheses, not clinical values,\n"
                   "and differ per device (see RulesConfig).\nNot a medical tool.",
                   fontsize=10, color=TEXT_SECONDARY, va="top", transform=legend_ax.transAxes)

    fig.tight_layout(rect=(0, 0, 1, 0.92))
    fig.savefig(out_path, dpi=150, facecolor=SURFACE)
    plt.close(fig)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("csv", nargs="+", help="study-mode CSV file(s)")
    parser.add_argument("-o", "--out", default="study.png", help="output image (default: study.png)")
    parser.add_argument("--table-only", action="store_true", help="print the table, skip the chart")
    args = parser.parse_args(argv)

    rows = load_rows(args.csv)
    if not rows:
        print("No rows found.", file=sys.stderr)
        return 1
    groups = group_by_label(rows)
    print(format_table(groups))
    if not args.table_only:
        plot(groups, args.out)
        print(f"\nChart written to {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
