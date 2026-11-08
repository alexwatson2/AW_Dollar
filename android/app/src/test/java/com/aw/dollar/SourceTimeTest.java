package com.aw.dollar;

import org.junit.Test;
import static org.junit.Assert.*;

public class SourceTimeTest {
    @Test public void parsesUtcAndOffsets() {
        long utc = PriceApi.parseTime("2026-11-08T10:00:00Z");
        assertEquals(utc, PriceApi.parseTime("2026-11-08T13:30:00+03:30"));
        assertEquals(utc, PriceApi.parseTime("2026-11-08T10:00:00.000Z"));
        assertEquals(utc+123, PriceApi.parseTime("2026-11-08T10:00:00.123456Z"));
        assertEquals(utc+100, PriceApi.parseTime("2026-11-08T10:00:00.1Z"));
        assertEquals(utc+120, PriceApi.parseTime("2026-11-08T10:00:00.12Z"));
    }
    @Test(expected = IllegalArgumentException.class) public void rejectsMissingDate() {
        PriceApi.parseTime("");
    }
    @Test(expected = IllegalArgumentException.class) public void rejectsImpossibleDate() {
        PriceApi.parseTime("2026-02-30T10:00:00Z");
    }
    @Test(expected = IllegalArgumentException.class) public void rejectsTrailingGarbage() {
        PriceApi.parseTime("2026-11-08T10:00:00Zgarbage");
    }
}
