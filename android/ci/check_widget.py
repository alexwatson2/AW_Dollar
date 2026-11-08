#!/usr/bin/env python3
"""Layout contract checks for the single, compact two-currency widget (no SDK needed)."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

MAIN = Path(__file__).resolve().parents[1] / "app/src/main"
A = "{http://schemas.android.com/apk/res/android}"


class CompactWidgetTest(unittest.TestCase):
    def test_only_one_provider_is_offered(self):
        manifest = ET.parse(MAIN / "AndroidManifest.xml")
        widgets = [receiver for receiver in manifest.findall("application/receiver")
                   if any(m.get(A + "name") == "android.appwidget.provider"
                          for m in receiver.findall("meta-data"))]
        self.assertEqual([".DollarWidget"], [r.get(A + "name") for r in widgets])

    def test_default_size_is_two_by_one(self):
        provider = ET.parse(MAIN / "res/xml/dollar_widget.xml").getroot()
        self.assertEqual("2", provider.get(A + "targetCellWidth"))
        self.assertEqual("1", provider.get(A + "targetCellHeight"))
        self.assertEqual("horizontal", provider.get(A + "resizeMode"))

    def test_only_prices_trend_icons_and_date_are_visible_text(self):
        layout = ET.parse(MAIN / "res/layout/price_widget.xml")
        labels = list(layout.iter("TextView"))
        self.assertEqual({"@+id/usd_price", "@+id/eur_price", "@+id/usd_trend",
                          "@+id/eur_trend", "@+id/updated"},
                         {label.get(A + "id") for label in labels})
        self.assertTrue(all(label.get(A + "maxLines") == "1" for label in labels))
        # No dummy market prices or explanatory first-quote text in the preview.
        self.assertTrue(all(label.get(A + "text") == "—" for label in labels))

    def test_each_currency_has_its_own_row_and_flag(self):
        layout = ET.parse(MAIN / "res/layout/price_widget.xml")
        nodes = {node.get(A + "id"): node for node in layout.iter()}
        for row, flag in (("usd_row", "usd_flag"), ("eur_row", "eur_flag")):
            image = nodes["@+id/" + row].find("ImageView")
            self.assertEqual("@drawable/" + flag, image.get(A + "src"))
            self.assertTrue((MAIN / "res/drawable-nodpi" / (flag + ".png")).is_file())


if __name__ == "__main__":
    unittest.main()
