package com.aw.dollar;

/** Compact widget-only presentation; detailed change text stays inside the app. */
public final class WidgetSummary {
    private WidgetSummary() { }

    public static String trend(Price current, Price previous) {
        if (current == null || previous == null || !current.source.equals(previous.source) || current.rial == previous.rial) return "—";
        return current.rial > previous.rial ? "▲" : "▼";
    }

    /** Use the older displayed quote, not a fetch time that could hide stale/partial data. */
    public static long updatedAt(Price usd, Price euro) {
        if (usd == null) return euro == null ? 0 : euro.sourceTime;
        if (euro == null) return usd.sourceTime;
        return Math.min(usd.sourceTime, euro.sourceTime);
    }
}
