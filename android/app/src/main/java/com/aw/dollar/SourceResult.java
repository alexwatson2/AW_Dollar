package com.aw.dollar;

public final class SourceResult {
    public final PriceSource source;
    public final String symbol, error;
    public final Price price;
    public final long checkedAt, latencyMs;
    public SourceResult(PriceSource source, String symbol, Price price, String error, long checkedAt, long latencyMs) {
        this.source = source; this.symbol = symbol; this.price = price;
        this.error = error; this.checkedAt = checkedAt; this.latencyMs = latencyMs;
    }
    public boolean success() { return price != null && error.isEmpty(); }
}
