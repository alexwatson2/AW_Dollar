package com.aw.dollar;

import java.math.BigDecimal;
import java.text.NumberFormat;
import java.util.Locale;

/** Money is stored as integer rials; never round through a floating-point value. */
public final class Price {
    public final String symbol;
    public final long rial, sourceTime, receivedTime;

    public Price(String symbol, long rial, long sourceTime, long receivedTime) {
        if (!symbol.equals("usd") && !symbol.equals("euro")) throw new IllegalArgumentException("Unknown currency");
        if (rial <= 0 || sourceTime <= 0 || receivedTime <= 0) throw new IllegalArgumentException("Invalid quote");
        this.symbol = symbol;
        this.rial = rial;
        this.sourceTime = sourceTime;
        this.receivedTime = receivedTime;
    }

    public static String toman(long rial) {
        NumberFormat format = NumberFormat.getNumberInstance(Locale.US);
        format.setMaximumFractionDigits(1);
        return format.format(BigDecimal.valueOf(rial, 1));
    }

    public static String change(Price current, Price previous) {
        if (current == null || previous == null) return "اولین نرخ ثبت‌شده";
        long delta = current.rial - previous.rial;
        if (delta == 0) return "بدون تغییر";
        return (delta > 0 ? "↑ +" : "↓ −") + toman(Math.abs(delta));
    }
}
