# Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
"""Tests for plot_study.py (no matplotlib needed). Run: python -m unittest test_plot_study.py"""

import os
import tempfile
import unittest

import plot_study

HEADER = ("timestamp_utc,label,reaction_device,reaction_confidence,reaction_median_ms,reaction_valid_trials,"
          "reaction_raw_ms,hold_device,hold_confidence,hold_max_s,hold_raw_s,taps_device,taps_confidence,"
          "taps_count,taps_duration_s,taps_per_s,stability_device,stability_confidence,stability_spread,"
          "stability_mean_offset,buttons_device,buttons_confidence,buttons_requested,buttons_unpressed,"
          "proposed_timingMultiplier,proposed_qteMode,proposed_holdToToggle,proposed_mashingMode,"
          "proposed_stickDeadzone,proposed_aimAssist,proposed_remapSuggestions,proposed_suggestOneHandedLayout,"
          "profile_version")


def row(label, reaction="400", hold="3", taps="5", spread="0.1", unpressed="", stability_conf="Ok"):
    return (f"2026-11-19T10:00:00.000Z,{label},Gamepad,Ok,{reaction},5,,Gamepad,Ok,{hold},,Gamepad,Ok,25,5,{taps},"
            f"Gamepad,{stability_conf},{spread},0,Gamepad,Ok,A;B;X;Y,{unpressed},1,Normal,false,Normal,0.125,Off,,false,1")


class PlotStudyTests(unittest.TestCase):
    def write(self, *lines):
        f = tempfile.NamedTemporaryFile("w", suffix=".csv", delete=False, encoding="utf-8", newline="")
        f.write("\n".join((HEADER,) + lines) + "\n")
        f.close()
        self.addCleanup(os.remove, f.name)
        return f.name

    def test_groups_rows_from_several_files_by_label_in_first_seen_order(self):
        a = self.write(row("normal"), row("gloves"))
        b = self.write(row("normal"), row('"left hand, tired"'))
        groups = plot_study.group_by_label(plot_study.load_rows([a, b]))
        self.assertEqual(list(groups), ["normal", "gloves", "left hand, tired"])
        self.assertEqual(len(groups["normal"]), 2)

    def test_summary_uses_medians_and_ignores_skipped(self):
        path = self.write(row("x", reaction="400"), row("x", reaction="600"), row("x", reaction="1000"),
                          row("x", spread="", stability_conf="Skipped"))
        (label, n, medians), = plot_study.summary(plot_study.group_by_label(plot_study.load_rows([path])))
        self.assertEqual(n, 4)
        self.assertEqual(medians["reaction_median_ms"], 500.0)   # median of 400, 600, 1000 and 400 (4th row)
        self.assertAlmostEqual(medians["stability_spread"], 0.1)  # skipped row left out

    def test_counts_unpressed_buttons(self):
        path = self.write(row("x", unpressed="X;Y"))
        rows = plot_study.load_rows([path])
        self.assertEqual(plot_study.value(rows[0], "buttons_unpressed_count"), 2.0)

    def test_empty_label_is_grouped_as_no_label(self):
        path = self.write(row(""))
        self.assertEqual(list(plot_study.group_by_label(plot_study.load_rows([path]))), [plot_study.NO_LABEL])

    def test_table_mentions_every_label(self):
        path = self.write(row("normal"), row("gloves"))
        table = plot_study.format_table(plot_study.group_by_label(plot_study.load_rows([path])))
        self.assertIn("normal", table)
        self.assertIn("gloves", table)


if __name__ == "__main__":
    unittest.main()
