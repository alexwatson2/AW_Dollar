#!/usr/bin/env python3
"""Layout contract checks for the single, compact four-currency widget (no SDK needed)."""
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
        self.assertEqual("56dp", provider.get(A + "minHeight"))

    def test_only_prices_trend_icons_and_date_are_visible_text(self):
        layout = ET.parse(MAIN / "res/layout/price_widget.xml")
        labels = list(layout.iter("TextView"))
        self.assertEqual({"@+id/usd_price", "@+id/eur_price", "@+id/usd_trend",
                          "@+id/eur_trend", "@+id/usdt_price", "@+id/usdt_trend", "@+id/btc_price", "@+id/btc_trend", "@+id/updated"},
                         {label.get(A + "id") for label in labels})
        self.assertTrue(all(label.get(A + "maxLines") == "1" for label in labels))
        # No dummy market prices or explanatory first-quote text in the preview.
        self.assertTrue(all(label.get(A + "text") == "—" for label in labels))

    def test_grid_has_two_cells_per_row_in_order(self):
        root = ET.parse(MAIN / "res/layout/price_widget.xml").getroot()
        rows = [node for node in root if node.tag == "LinearLayout"]
        self.assertEqual(["@+id/fiat_row", "@+id/crypto_row"], [row.get(A + "id") for row in rows])
        self.assertEqual([["@+id/usd_row", "@+id/eur_row"], ["@+id/usdt_row", "@+id/btc_row"]],
                         [[cell.get(A + "id") for cell in row] for row in rows])
        for row in rows:
            self.assertEqual("horizontal", row.get(A + "orientation"))
            self.assertTrue(all(cell.get(A + "layout_weight") == "1" for cell in row))

    def test_wide_layout_preserves_grid_and_uses_readable_larger_elements(self):
        compact = ET.parse(MAIN / "res/layout/price_widget.xml").getroot()
        wide = ET.parse(MAIN / "res/layout/price_widget_wide.xml").getroot()
        compact_nodes = {n.get(A + "id"): n for n in compact.iter() if n.get(A + "id")}
        wide_nodes = {n.get(A + "id"): n for n in wide.iter() if n.get(A + "id")}
        self.assertEqual(set(compact_nodes), set(wide_nodes))
        rows = [node for node in wide if node.tag == "LinearLayout"]
        self.assertEqual([["@+id/usd_row", "@+id/eur_row"], ["@+id/usdt_row", "@+id/btc_row"]],
                         [[cell.get(A + "id") for cell in row] for row in rows])
        for prefix in ("usd", "eur", "usdt", "btc"):
            cell = wide_nodes["@+id/" + prefix + "_row"]
            self.assertEqual("18dp", cell.find("ImageView").get(A + "layout_width"))
            price = wide_nodes["@+id/" + prefix + "_price"]
            self.assertEqual("16sp", price.get(A + "textSize"))
            self.assertEqual("wrap_content", price.get(A + "layout_width"))
            self.assertEqual("76dp", price.get(A + "maxWidth"))
            # Trend directly follows the price rather than being pushed to the cell edge.
            self.assertEqual("@+id/" + prefix + "_trend", list(cell)[2].get(A + "id"))
        self.assertEqual("10sp", wide_nodes["@+id/updated"].get(A + "textSize"))

    def test_size_change_rebinds_each_widget_using_its_own_options(self):
        source = (MAIN / "java/com/aw/dollar/PriceWidget.java").read_text()
        self.assertIn("onAppWidgetOptionsChanged", source)
        self.assertIn("new int[]{appWidgetId}, newOptions", source)
        self.assertIn("manager.getAppWidgetOptions(id)", source)
        self.assertIn("OPTION_APPWIDGET_MIN_WIDTH", source)
        self.assertIn("WidgetSummary.useWideLayout(width)", source)

    def test_each_currency_has_its_own_row_and_flag(self):
        layout = ET.parse(MAIN / "res/layout/price_widget.xml")
        nodes = {node.get(A + "id"): node for node in layout.iter()}
        for row, flag in (("usd_row", "usd_flag"), ("eur_row", "eur_flag"), ("usdt_row", "usdt_icon"), ("btc_row", "btc_icon")):
            image = nodes["@+id/" + row].find("ImageView")
            self.assertEqual("@drawable/" + flag, image.get(A + "src"))
            folder, extension = ("drawable", ".xml") if flag.endswith("_icon") else ("drawable-nodpi", ".png")
            self.assertTrue((MAIN / "res" / folder / (flag + extension)).is_file())


if __name__ == "__main__":
    unittest.main()
