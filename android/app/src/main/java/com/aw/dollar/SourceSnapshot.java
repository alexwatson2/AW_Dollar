package com.aw.dollar;

/** Latest attempt plus independently retained last successful quote, including failed/offline sources. */
public final class SourceSnapshot {
    public final PriceSource source;
    public final Price price;
    public final String status, error;
    public final long checkedAt, latencyMs;
    public SourceSnapshot(PriceSource source, Price price, String status, String error, long checkedAt, long latencyMs) {
        this.source = source; this.price = price; this.status = status;
        this.error = error; this.checkedAt = checkedAt; this.latencyMs = latencyMs;
    }
}
