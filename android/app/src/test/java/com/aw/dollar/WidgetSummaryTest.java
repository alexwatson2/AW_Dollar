package com.aw.dollar;

import org.junit.Test;
import static org.junit.Assert.*;

public class WidgetSummaryTest {
    private Price quote(String symbol, long rial, long time) {
        return new Price(symbol, rial, time, time + 100);
    }

    @Test public void firstQuoteAndMissingDataUseOnlyADash() {
        Price usd = quote("usd", 2649000, 10);
        assertEquals("—", WidgetSummary.trend(null, null));
        assertEquals("—", WidgetSummary.trend(usd, null));
        assertEquals("—", WidgetSummary.trend(null, usd));
    }

    @Test public void trendIsOnlyAnIconNotVerboseChangeText() {
        Price previous = quote("usd", 2649000, 10);
        assertEquals("▲", WidgetSummary.trend(quote("usd", 2650000, 20), previous));
        assertEquals("▼", WidgetSummary.trend(quote("usd", 2648000, 20), previous));
        assertEquals("—", WidgetSummary.trend(quote("usd", 2649000, 20), previous));
    }

    @Test public void sourceSwitchDoesNotPretendToBeMarketMovement() {
        Price original = new Price("usd",2649000,10,11,"pricetoday");
        Price other = new Price("usd",2649500,20,21,"iranmarket");
        assertEquals("—",WidgetSummary.trend(other,original));
        assertTrue(Price.change(other,original).contains("تغییر منبع"));
        assertEquals("—", WidgetSummary.trend(new Price("btc_usd",100,20,21,"iranmarket"), new Price("btc",50,10,11,"iranmarket")));
    }

    @Test public void combinedDateUsesOlderQuoteNotNewerFetch() {
        Price usd = quote("usd", 2649000, 10);
        Price euro = quote("euro", 2957500, 20);
        assertEquals(10, WidgetSummary.updatedAt(usd, euro));
        assertEquals(10, WidgetSummary.updatedAt(euro, usd));
    }

    @Test public void tetherParticipatesInOldestDisplayedDate() {
        Price usd = quote("usd", 10, 30);
        Price euro = quote("euro", 10, 20);
        Price usdt = quote("usdt", 10, 10);
        assertEquals(10, WidgetSummary.updatedAt(usd, euro, usdt));
        assertEquals(10, WidgetSummary.updatedAt(null, null, usdt));
        assertEquals(20, WidgetSummary.updatedAt(usd, euro, null));
    }

    @Test public void widgetChoosesReadableLayoutOnlyWhenWidthIsKnownAndFits() {
        assertFalse(WidgetSummary.useWideLayout(0));
        assertFalse(WidgetSummary.useWideLayout(140));
        assertFalse(WidgetSummary.useWideLayout(239));
        assertTrue(WidgetSummary.useWideLayout(240));
        assertTrue(WidgetSummary.useWideLayout(330));
    }

    @Test public void compactPriceKeepsSmallPricesAndAbbreviatesLargePrices() {
        assertEquals("292,890", WidgetSummary.compactPrice(2928900));
        assertEquals("1.23M", WidgetSummary.compactPrice(12345670));
        assertEquals("18.04B", WidgetSummary.compactPrice(180385168060L));
    }

    @Test public void bitcoinWidgetUsesExplicitRoundedDollarValue() {
        assertEquals("$60,614", WidgetSummary.bitcoinPrice(60613640000L));
        assertEquals("$60,613", WidgetSummary.bitcoinPrice(60613400000L));
    }

    @Test public void bitcoinParticipatesInOldestDisplayedDate() {
        Price btc = quote("btc_usd", 60613640000L, 5);
        assertEquals(5, WidgetSummary.updatedAt(quote("usd",10,30), quote("euro",10,20), quote("usdt",10,10), btc));
        assertEquals(5, WidgetSummary.updatedAt(null, null, null, btc));
    }

    @Test public void dateHandlesEmptyAndPartiallyLoadedWidget() {
        Price usd = quote("usd", 2649000, 10);
        assertEquals(0, WidgetSummary.updatedAt(null, null));
        assertEquals(10, WidgetSummary.updatedAt(usd, null));
        assertEquals(10, WidgetSummary.updatedAt(null, usd));
    }
}
