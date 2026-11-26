package com.aw.dollar;

import org.junit.Test;
import static org.junit.Assert.*;

public class PriceTest {
    @Test public void convertsRialWithoutLosingFractionalToman() {
        assertEquals("268,200", Price.toman(2682000));
        assertEquals("268,200.5", Price.toman(2682005));
    }
    @Test public void comparesWithPreviousSnapshot() {
        Price previous = new Price("usd", 2682000, 1, 2);
        assertEquals("↑ +400", Price.change(new Price("usd", 2686000, 3, 4), previous));
        assertEquals("↓ −400", Price.change(new Price("usd", 2678000, 3, 4), previous));
        assertEquals("بدون تغییر", Price.change(previous, previous));
        assertEquals("اولین نرخ ثبت‌شده", Price.change(previous, null));
    }
    @Test public void bitcoinUsdUsesSeparateUnitAndExactCsv() {
        Price btc = new Price("btc_usd", 60613640000L, 2, 3, "iranmarket");
        Price previous = new Price("btc_usd", 60613000000L, 1, 2, "iranmarket");
        assertEquals("60,613.64", Price.amount(btc.symbol, btc.minorUnits));
        assertEquals("دلار", Price.unit(btc.symbol));
        assertEquals("↑ +0.64", Price.change(btc, previous));
        assertEquals("60613.64", Price.csvAmount(btc.symbol, btc.minorUnits));
        assertEquals("-0.64", Price.csvAmount(btc.symbol, -640000));
        assertArrayEquals(new String[]{"", "", "60613.64", "0.64"}, Price.csvColumns("btc_usd", 60613640000L, 60613000000L));
        assertArrayEquals(new String[]{"", "", "60613.64", ""}, Price.csvColumns("btc_usd", 60613640000L, null));
        assertArrayEquals(new String[]{"18038516806.0", "", "", ""}, Price.csvColumns("btc", 180385168060L, null));
        assertArrayEquals(new String[]{"268200.5", "0.5", "", ""}, Price.csvColumns("usdt", 2682005L, 2682000L));
        assertEquals("تومان", Price.unit("usdt"));
        assertEquals("268200.5", Price.csvAmount("usdt", 2682005));
        assertEquals("تغییر منبع؛ اختلاف الزاماً نوسان بازار نیست", Price.change(btc, new Price("btc", 180000000000L, 1, 2, "iranmarket")));
    }
    @Test(expected = IllegalArgumentException.class) public void rejectsZero() {
        new Price("usd", 0, 1, 2);
    }
    @Test(expected = IllegalArgumentException.class) public void rejectsUnknownSymbol() {
        new Price("xxx", 10, 1, 2);
    }
    @Test public void handlesLargePricesExactly() {
        assertEquals("900,719,925,474,099.3", Price.toman(9007199254740993L));
    }
}
