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

    @Test public void combinedDateUsesOlderQuoteNotNewerFetch() {
        Price usd = quote("usd", 2649000, 10);
        Price euro = quote("euro", 2957500, 20);
        assertEquals(10, WidgetSummary.updatedAt(usd, euro));
        assertEquals(10, WidgetSummary.updatedAt(euro, usd));
    }

    @Test public void dateHandlesEmptyAndPartiallyLoadedWidget() {
        Price usd = quote("usd", 2649000, 10);
        assertEquals(0, WidgetSummary.updatedAt(null, null));
        assertEquals(10, WidgetSummary.updatedAt(usd, null));
        assertEquals(10, WidgetSummary.updatedAt(null, usd));
    }
}
