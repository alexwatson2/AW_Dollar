package com.aw.dollar;

import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.math.BigDecimal;
import java.net.HttpURLConnection;
import java.net.URL;
import java.text.ParsePosition;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

public final class PriceApi {
    private PriceApi() { }

    public static Price fetch(String symbol) throws Exception {
        if (!symbol.equals("usd") && !symbol.equals("euro")) throw new IllegalArgumentException("Unknown currency");
        HttpURLConnection connection = (HttpURLConnection) new URL("https://api.priceto.day/v1/latest/irr/" + symbol).openConnection();
        connection.setConnectTimeout(15000);
        connection.setReadTimeout(15000);
        connection.setRequestProperty("User-Agent", "AW-Dollar-Android/1.0");
        try {
            if (connection.getResponseCode() != 200) throw new IllegalStateException("HTTP " + connection.getResponseCode());
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            try (InputStream stream = connection.getInputStream()) {
                byte[] buffer = new byte[4096];
                int n;
                while ((n = stream.read(buffer)) != -1) {
                    if (bytes.size() + n > 65536) throw new IllegalStateException("Response too large");
                    bytes.write(buffer, 0, n);
                }
            }
            JSONObject json = new JSONObject(bytes.toString("UTF-8"));
            if (!json.optBoolean("success") || json.isNull("price")) throw new IllegalStateException("Invalid response");
            long rial = new BigDecimal(json.getString("price")).longValueExact();
            long now = System.currentTimeMillis();
            // Never invent an upstream timestamp: an invalid date is an invalid quote.
            long time = parseTime(json.getString("time"));
            if (time > now + 5 * 60 * 1000L) throw new IllegalArgumentException("Future quote");
            return new Price(symbol, rial, time, now);
        } finally {
            connection.disconnect();
        }
    }

    static long parseTime(String value) {
        String normalized = value.replaceFirst("Z$", "+0000").replaceFirst("([+-]\\d{2}):(\\d{2})$", "$1$2");
        // SimpleDateFormat interprets fractional digits as milliseconds, so normalize to three.
        normalized = normalized.replaceFirst("\\.(\\d{3})\\d+", ".$1");
        normalized = normalized.replaceFirst("\\.(\\d)(?=[+-])", ".$100");
        normalized = normalized.replaceFirst("\\.(\\d{2})(?=[+-])", ".$10");
        for (String pattern : new String[]{"yyyy-MM-dd'T'HH:mm:ss.SSSZ", "yyyy-MM-dd'T'HH:mm:ssZ"}) {
            SimpleDateFormat f = new SimpleDateFormat(pattern, Locale.US);
            f.setTimeZone(TimeZone.getTimeZone("UTC"));
            f.setLenient(false);
            ParsePosition position = new ParsePosition(0);
            Date date = f.parse(normalized, position);
            if (date != null && position.getIndex() == normalized.length()) return date.getTime();
        }
        throw new IllegalArgumentException("Missing or invalid source time");
    }
}
