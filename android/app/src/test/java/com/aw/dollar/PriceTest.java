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
