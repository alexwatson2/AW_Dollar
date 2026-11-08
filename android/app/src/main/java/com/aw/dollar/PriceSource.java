package com.aw.dollar;

/** Public free-market sources only; never mix official FX or USDT with cash USD/EUR. */
public enum PriceSource {
    PRICETODAY("pricetoday", "PriceToDay", "https://api.priceto.day/v1/latest/irr/"),
    IRAN_MARKET("iranmarket", "Iran Market Data", "https://api.github.com/repos/iran-market/iran-market.github.io/contents/data/popular.json"),
    NERKHSANJ("nerkhsanj", "نرخ‌سنج", "https://nerkh.jahankhahan.shop/data/live.json");

    public final String id, title, url;
    PriceSource(String id, String title, String url) { this.id = id; this.title = title; this.url = url; }
    public static PriceSource byId(String id) {
        for (PriceSource source : values()) if (source.id.equals(id)) return source;
        throw new IllegalArgumentException("Unknown source");
    }
}
