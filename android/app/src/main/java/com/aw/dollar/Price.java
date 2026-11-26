package com.aw.dollar;

import java.math.BigDecimal;
import java.text.NumberFormat;
import java.util.Locale;

/** Stored integers use rials for the legacy currencies and microdollars for btc_usd.
 * The legacy database column is named rial; symbols keep the units unambiguous. */
public final class Price {
    public final String symbol, source;
    public final long minorUnits, sourceTime, receivedTime;

    public Price(String symbol, long minorUnits, long sourceTime, long receivedTime) {
        this(symbol, minorUnits, sourceTime, receivedTime, "pricetoday");
    }

    public Price(String symbol, long minorUnits, long sourceTime, long receivedTime, String source) {
        PriceSource.byId(source);
        if (!symbol.equals("usd") && !symbol.equals("euro") && !symbol.equals("usdt") && !symbol.equals("btc") && !symbol.equals("btc_usd")) throw new IllegalArgumentException("Unknown currency");
        if (minorUnits <= 0 || sourceTime <= 0 || receivedTime <= 0) throw new IllegalArgumentException("Invalid quote");
        this.source = source;
        this.symbol = symbol;
        this.minorUnits = minorUnits;
        this.sourceTime = sourceTime;
        this.receivedTime = receivedTime;
    }

    public static String toman(long rial) {
        NumberFormat format = NumberFormat.getNumberInstance(Locale.US);
        format.setMaximumFractionDigits(1);
        return format.format(BigDecimal.valueOf(rial, 1));
    }

    public static boolean isDollarBitcoin(String symbol) { return "btc_usd".equals(symbol); }

    /** Number without a currency sign, for a chart or value with an explicit unit label. */
    public static String amount(String symbol, long minorUnits) {
        if (!isDollarBitcoin(symbol)) return toman(minorUnits);
        NumberFormat format = NumberFormat.getNumberInstance(Locale.US);
        format.setMaximumFractionDigits(6);
        return format.format(BigDecimal.valueOf(minorUnits, 6));
    }

    public static String unit(String symbol) { return isDollarBitcoin(symbol) ? "دلار" : "تومان"; }

    /** Exact ungrouped decimal for CSV, including negative changes. */
    public static String csvAmount(String symbol, long minorUnits) {
        BigDecimal value = BigDecimal.valueOf(minorUnits, isDollarBitcoin(symbol) ? 6 : 1);
        return (isDollarBitcoin(symbol) ? value.stripTrailingZeros() : value).toPlainString();
    }

    /** Columns are price_toman, change_toman, price_usd, change_usd. */
    public static String[] csvColumns(String symbol, long minorUnits, Long previous) {
        String amount = csvAmount(symbol, minorUnits);
        String delta = previous == null ? "" : csvAmount(symbol, minorUnits - previous);
        return isDollarBitcoin(symbol) ? new String[]{"", "", amount, delta}
                : new String[]{amount, delta, "", ""};
    }

    public static String change(Price current, Price previous) {
        if (current == null || previous == null) return "اولین نرخ ثبت‌شده";
        if (!current.symbol.equals(previous.symbol) || !current.source.equals(previous.source))
            return "تغییر منبع؛ اختلاف الزاماً نوسان بازار نیست";
        long delta = current.minorUnits - previous.minorUnits;
        if (delta == 0) return "بدون تغییر";
        return (delta > 0 ? "↑ +" : "↓ −") + amount(current.symbol, Math.abs(delta));
    }
}
