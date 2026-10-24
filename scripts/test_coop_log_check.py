import os
import tempfile
import unittest

import coop_log_check


def log(*lines):
    return "\n".join(lines) + "\n"


def info(text):
    return "[Info   :Dizzy Bundle Up] " + text


def warn(text):
    return "[Warning:Dizzy Bundle Up] " + text


class CoopLogCheckTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)

    def write(self, name, text):
        path = os.path.join(self.dir.name, name)
        with open(path, "w", encoding="utf-8") as handle:
            handle.write(text)
        return path

    def check(self, host, guest, stalls=()):
        lines = []
        code = coop_log_check.run(self.write("host.log", host), self.write("guest.log", guest),
                                  [self.write("s%d.log" % i, s) for i, s in enumerate(stalls)], out=lines.append)
        return code, "\n".join(lines)

    def test_agreeing_session_passes(self):
        host = log(info("[Coop] Sent candle 100: 3 pieces."),
                   info("[Coop] Received candle 200."), info("[Coop] Applied candle 200: 5 pieces."))
        guest = log(info("[Coop] Received candle 100."), info("[Coop] Applied candle 100: 3 pieces."),
                    info("[Coop] Sent candle 200: 5 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)
        self.assertIn("OK", text)

    def test_a_lost_message_is_found(self):
        host = log(info("[Coop] Sent candle 100: 3 pieces."), info("[Coop] Sent candle 100: 2 pieces."))
        guest = log(info("[Coop] Received candle 100."), info("[Coop] Applied candle 100: 3 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(1, code)
        self.assertIn("LOST", text)
        self.assertIn("2 time(s)", text)

    def test_counts_that_end_apart_are_found(self):
        host = log(info("[Coop] Sent candle 100: 3 pieces."))
        guest = log(info("[Coop] Received candle 100."), info("[Coop] Applied candle 100: 1 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(1, code)
        self.assertIn("DIFFERS", text)
        self.assertIn("3 pieces on host", text)
        self.assertIn("1 on guest", text)

    def test_the_last_count_wins(self):
        host = log(info("[Coop] Sent candle 100: 4 pieces."), info("[Coop] Sent candle 100: 3 pieces."))
        guest = log(info("[Coop] Received candle 100."), info("[Coop] Applied candle 100: 4 pieces."),
                    info("[Coop] Received candle 100."), info("[Coop] Applied candle 100: 3 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)

    def test_a_merge_counts_as_the_final_count_and_is_reported(self):
        host = log(info("[Coop] Sent candle 100: 3 pieces."), info("[Coop] Received candle 100."),
                   info("[Coop] candle 100 was changed here too: 2 pieces."), info("[Coop] Applied candle 100: 2 pieces."))
        guest = log(info("[Coop] Sent candle 100: 3 pieces."), info("[Coop] Received candle 100."),
                    info("[Coop] candle 100 was changed here too: 2 pieces."), info("[Coop] Applied candle 100: 2 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)
        self.assertIn("MERGED", text)

    def test_an_unknown_bundle_is_found(self):
        host = log(info("[Coop] Received bundle 77, which is not here."))
        guest = log(info("[Coop] Sent candle 100: 3 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(1, code)
        self.assertIn("UNKNOWN", text)

    def test_warnings_and_errors_are_failures(self):
        host = log(info("[Coop] Sent candle 100: 1 pieces."), warn("Could not send a bundle to the other players: x"))
        guest = log(info("[Coop] Received candle 100."), "[Error  :Dizzy Bundle Up] boom")
        code, text = self.check(host, guest)
        self.assertEqual(1, code)
        self.assertIn("WARNING", text)
        self.assertIn("ERROR", text)

    def test_other_mods_warnings_are_ignored(self):
        host = log(info("[Coop] Sent candle 100: 1 pieces."), "[Warning:Some Other Mod] hmm")
        guest = log(info("[Coop] Received candle 100."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)

    def test_ignored_pickups_are_noted_not_failed(self):
        host = log(info("[Coop] Sent candle 100: 5 pieces."),
                   info("[Coop] Another player picked up 101, a bundle we just combined. Ignored, so no other bundle takes its ID."))
        guest = log(info("[Coop] Received candle 100."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)
        self.assertIn("IGNORED", text)

    def test_stall_reports_are_found(self):
        ok = log(info("[Coop] Sent candle 100: 1 pieces."))
        other = log(info("[Coop] Received candle 100."))
        stall = "21:04:11 Game stopped for 4 s. Last place: Update. Last bundle work: rebuilt goat cheese (3).\r\n"
        code, text = self.check(ok, other, stalls=(stall, ""))
        self.assertEqual(1, code)
        self.assertIn("STALL", text)
        self.assertIn("goat cheese", text)

    def test_a_log_with_no_coop_lines_is_reported(self):
        code, text = self.check(log("[Info   :BepInEx] Loading"), log("[Info   :BepInEx] Loading"))
        self.assertEqual(1, code)
        self.assertIn("EMPTY", text)

    def test_names_with_spaces_and_held_marks_parse(self):
        host = log(info("[Coop] Sent goat cheese 100: 3 pieces."))
        guest = log(info("[Coop] Received goat cheese 100 (held)."), info("[Coop] Applied goat cheese 100: 3 pieces."))
        code, text = self.check(host, guest)
        self.assertEqual(0, code, text)

    def test_a_missing_file_gives_exit_code_two(self):
        self.assertEqual(2, coop_log_check.main([os.path.join(self.dir.name, "none.log"), os.path.join(self.dir.name, "none2.log")]))


if __name__ == "__main__":
    unittest.main()
