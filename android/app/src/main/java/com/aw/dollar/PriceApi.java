package com.aw.dollar;

import org.json.JSONObject;
import org.json.JSONArray;
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

    public static SourceResult fetch(PriceSource source, String symbol) {
        long started = System.nanoTime();
        try {
            String url = source.url + (source == PriceSource.PRICETODAY ? symbol : "");
            Price quote = parse(source, symbol, request(url, source), System.currentTimeMillis());
            return new SourceResult(source, symbol, quote, "", System.currentTimeMillis(),
                    (System.nanoTime()-started)/1000000);
        } catch (Exception e) {
            String error = e instanceof java.net.SocketTimeoutException ? "مهلت پاسخ تمام شد" :
                    e instanceof java.net.UnknownHostException ? "دسترسی به سرور ممکن نشد" :
                    e instanceof IllegalArgumentException || e instanceof org.json.JSONException || e instanceof ArithmeticException
                            ? "پاسخ نامعتبر یا قالب تغییرکرده" : "خطای ارتباط: " + e.getClass().getSimpleName();
            return new SourceResult(source, symbol, null, error, System.currentTimeMillis(),
                    (System.nanoTime()-started)/1000000);
        }
    }

    private static String request(String url, PriceSource source) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
        connection.setConnectTimeout(6000);
        connection.setReadTimeout(6000);
        connection.setRequestProperty("User-Agent", "dollar-Android/1.2 (+https://github.com/alexwatson2/AW_Dollar)");
        if (source == PriceSource.IRAN_MARKET)
            connection.setRequestProperty("Accept", "application/vnd.github.raw+json");
        long deadline = System.nanoTime() + 12000000000L;
        try {
            int status = connection.getResponseCode();
            if (status != 200) throw new java.io.IOException("HTTP " + status);
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            try (InputStream stream = connection.getInputStream()) {
                byte[] buffer = new byte[4096];
                int n;
                while ((n = stream.read(buffer)) != -1) {
                    if (Thread.currentThread().isInterrupted() || System.nanoTime() > deadline)
                        throw new java.net.SocketTimeoutException("Deadline reached");
                    if (bytes.size()+n > 1048576) throw new IllegalArgumentException("Response too large");
                    bytes.write(buffer, 0, n);
                }
            }
            return bytes.toString("UTF-8");
        } finally { connection.disconnect(); }
    }

    static Price parse(PriceSource source, String symbol, String body, long now) throws Exception {
        if (!symbol.equals("usd") && !symbol.equals("euro")) throw new IllegalArgumentException("Unknown currency");
        JSONObject json = new JSONObject(body);
        long rial, time;
        if (source == PriceSource.PRICETODAY) {
            if (!json.optBoolean("success")) throw new IllegalArgumentException("API failure");
            rial = new BigDecimal(json.get("price").toString()).longValueExact();
            time = parseTime(json.getString("time"));
        } else if (source == PriceSource.IRAN_MARKET) {
            JSONArray data = json.getJSONArray("data");
            JSONObject item = null;
            String key = symbol.equals("usd") ? "USD_IRR_FREE" : "EUR_IRR_FREE";
            for (int i=0; i<data.length(); i++) {
                JSONObject candidate = data.getJSONObject(i);
                if (key.equals(candidate.optString("symbol"))) { item = candidate; break; }
            }
            if (item == null || !"iran_free".equals(item.getString("market"))
                    || new BigDecimal(item.get("unit_size").toString()).compareTo(BigDecimal.ONE) != 0) throw new IllegalArgumentException("Wrong market or unit");
            String currency = item.getString("currency");
            if (!currency.equals("IRT") && !currency.equals("IRR")) throw new IllegalArgumentException("Unknown unit");
            BigDecimal amount = new BigDecimal(item.get("price").toString());
            rial = (currency.equals("IRT") ? amount.multiply(BigDecimal.TEN) : amount).longValueExact();
            time = parseTime(item.getString("timestamp"));
        } else {
            String key = symbol.equals("usd") ? "dollar" : "euro";
            rial = new BigDecimal(json.getJSONObject("rates").get(key).toString()).multiply(BigDecimal.TEN).longValueExact();
            time = SourceTime.tehranJalali(json.getString("date"), json.getString("time"));
        }
        if (time > now+5*60*1000L) throw new IllegalArgumentException("Future quote");
        return new Price(symbol, rial, time, now, source.id);
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
