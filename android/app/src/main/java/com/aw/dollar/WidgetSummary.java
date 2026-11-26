package com.aw.dollar;

/** Compact widget-only presentation; detailed change text stays inside the app. */
public final class WidgetSummary {
    private WidgetSummary() { }

    public static String trend(Price current, Price previous) {
        if (current == null || previous == null || !current.source.equals(previous.source) || current.rial == previous.rial) return "—";
        return current.rial > previous.rial ? "▲" : "▼";
    }

    /** Conservative minimum width across orientations, in dp; missing options use the compact layout. */
    public static boolean useWideLayout(int minWidthDp) { return minWidthDp >= 240; }

    /** Shorten large toman prices to fit the compact two-column widget. */
    public static String compactPrice(long rial) {
        java.math.BigDecimal toman = java.math.BigDecimal.valueOf(rial, 1);
        java.math.BigDecimal billion = java.math.BigDecimal.valueOf(1000000000L);
        java.math.BigDecimal million = java.math.BigDecimal.valueOf(1000000L);
        if (toman.compareTo(billion) >= 0)
            return toman.divide(billion, 2, java.math.RoundingMode.HALF_UP).stripTrailingZeros().toPlainString() + "B";
        if (toman.compareTo(million) >= 0)
            return toman.divide(million, 2, java.math.RoundingMode.HALF_UP).stripTrailingZeros().toPlainString() + "M";
        return Price.toman(rial);
    }

    /** Use the older displayed quote, not a fetch time that could hide stale/partial data. */
    public static long updatedAt(Price... prices) {
        long oldest = 0;
        for (Price price : prices) {
            if (price != null && (oldest == 0 || price.sourceTime < oldest)) oldest = price.sourceTime;
        }
        return oldest;
    }
}
