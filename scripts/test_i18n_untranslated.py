import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

import i18n_untranslated as tool


class I18nUntranslatedTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        en = {'copies': {'A_tsx_copy': {'title': 'Safety events', 'go': 'NO-GO', 'unit': 'min',
                                        'count': '{count} / {total}', 'loh': 'LoH {value} h'}},
              'pairs': {'m_1': 'Reload', 'm_2': 'POSEIDON', 'm_3': 'Status'}}
        de = {'copies': {'A_tsx_copy': {'title': 'Sicherheitsereignisse', 'go': 'NO-GO', 'unit': 'min',
                                        'count': '{count} / {total}', 'loh': 'LoH {value} h'}},
              'pairs': {'m_1': 'Reload', 'm_2': 'POSEIDON', 'm_3': 'Status', 'm_x': 'extra'}}
        for name, data in {'en': en, 'en-GB': en, 'de': de}.items():
            (self.dir / f'{name}.json').write_text(json.dumps(data), encoding='utf-8')

    def test_language_neutral_values_are_not_reported(self):
        for value in ('NO-GO', 'SOF', 'LOA (m)', 'min', '{count} / {total}', '00–08', 'POSEIDON', 'GO'):
            self.assertTrue(tool.is_language_neutral(value), value)
        for value in ('Reload', 'Safety events', 'LoH {value} hours', 'd days'):
            self.assertFalse(tool.is_language_neutral(value), value)

    def test_report_counts_copied_values_and_excludes_english_variants(self):
        data = tool.report(self.dir, include_keys=True)
        self.assertEqual(['de'], list(data['locales']))
        de = data['locales']['de']
        self.assertEqual(['pairs.m_1', 'pairs.m_3'], de['keys'])
        self.assertEqual(2, de['untranslated'])
        self.assertEqual(0, de['missingKeys'])
        self.assertEqual(1, de['extraKeys'])

    def test_prefix_filter_and_max_exit_code(self):
        self.assertEqual(0, tool.report(self.dir, prefix='copies.')['locales']['de']['untranslated'])
        with redirect_stdout(io.StringIO()) as out:
            self.assertEqual(1, tool.main(['--messages', str(self.dir), '--max', '1']))
        self.assertEqual(2, json.loads(out.getvalue())['locales']['de']['untranslated'])
        with redirect_stdout(io.StringIO()):
            self.assertEqual(0, tool.main(['--messages', str(self.dir), '--max', '2']))

    def test_repository_catalogs_share_one_key_set(self):
        data = tool.report()
        for locale, entry in data['locales'].items():
            self.assertEqual(0, entry['missingKeys'], locale)
            self.assertEqual(0, entry['extraKeys'], locale)


if __name__ == '__main__':
    unittest.main()
