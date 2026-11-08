package com.aw.dollar;

import android.app.Activity;
import android.appwidget.AppWidgetManager;
import android.content.ComponentName;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.List;
import java.util.Locale;

public final class MainActivity extends Activity {
    // All components run in this process; no exported broadcast receiver is necessary.
    private final Runnable updates = () -> load();
    @Override protected void onStart() { super.onStart(); Refresh.addListener(updates); }
    @Override protected void onStop() { Refresh.removeListener(updates); super.onStop(); }

    private String symbol = "usd";
    private int page;
    private int generation;
    private boolean refreshing;
    private LinearLayout body;
    private ScrollView screen;
    private boolean resetScroll = true;
    private static final int PAGE_SIZE = 50;
    private static final int EXPORT = 4;
    private final android.os.Handler handler = new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable ticker = new Runnable() {
        @Override public void run() { load(); handler.postDelayed(this, 30000); }
    };

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (state != null) {
            symbol = state.getString("symbol", "usd");
            page = state.getInt("page", 0);
        } else readIntent(getIntent());
        Refresh.schedule(this);
    }
    @Override protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        readIntent(intent);
        page = 0;
        resetScroll = true;
        load();
    }
    private void readIntent(Intent intent) { symbol = "euro".equals(intent.getStringExtra("symbol")) ? "euro" : "usd"; }
    @Override protected void onSaveInstanceState(Bundle state) {
        state.putString("symbol", symbol); state.putInt("page", page); super.onSaveInstanceState(state);
    }
    @Override protected void onResume() {
        super.onResume();
        load();
        long checked = getSharedPreferences("refresh", 0).getLong(symbol + "_checked", 0);
        if (getSharedPreferences("refresh",0).getLong("sources_checked",0) == 0 || System.currentTimeMillis()-checked >= 30*60*1000L) refresh();
        handler.postDelayed(ticker, 30000);
    }
    @Override protected void onPause() { handler.removeCallbacks(ticker); super.onPause(); }
    @Override protected void onDestroy() { generation++; super.onDestroy(); }

    private void refresh() {
        if (refreshing) return;
        refreshing = true;
        load();
        Refresh.NETWORK.execute(() -> {
            boolean success = Refresh.fetchAll(getApplicationContext());
            runOnUiThread(() -> {
                if (isDestroyed()) return;
                refreshing = false;
                load();
                Toast.makeText(this, success ? "قیمت‌ها بررسی شدند" : "برای بعضی ارزها پاسخ تازهٔ معتبری نرسید؛ دادهٔ قبلی حفظ شد", Toast.LENGTH_LONG).show();
            });
        });
    }

    private void load() {
        final int request = ++generation;
        final String selected = symbol;
        final int selectedPage = page;
        Refresh.IO.execute(() -> {
            try (PriceStore store = new PriceStore(getApplicationContext())) {
                List<Price> latest = store.history(selected, 60, 0);
                List<Price> rows = store.history(selected, PAGE_SIZE+1, selectedPage*PAGE_SIZE);
                int count = store.count(selected);
                List<SourceSnapshot> usdSources = store.sources("usd");
                List<SourceSnapshot> euroSources = store.sources("euro");
                runOnUiThread(() -> {
                    if (!isDestroyed() && generation == request) render(latest, rows, count, usdSources, euroSources);
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    if (!isDestroyed() && generation == request)
                        Toast.makeText(this, "خواندن تاریخچه ناموفق بود؛ دوباره تلاش کنید", Toast.LENGTH_LONG).show();
                });
            }
        });
    }

    private void render(List<Price> latest, List<Price> rows, int count, List<SourceSnapshot> usdSources, List<SourceSnapshot> euroSources) {
        int previousY = screen == null || resetScroll ? 0 : screen.getScrollY();
        resetScroll = false;
        ScrollView scroll = new ScrollView(this);
        screen = scroll;
        scroll.setFillViewport(true);
        scroll.setBackgroundColor(0xfff3f6f4);
        body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        body.setPadding(dp(22), dp(20), dp(22), dp(28));
        scroll.addView(body);
        // Android 15 enforces edge-to-edge; keep all content clear of system bars/cutouts.
        scroll.setOnApplyWindowInsetsListener((v, insets) -> {
            v.setPadding(insets.getSystemWindowInsetLeft(), insets.getSystemWindowInsetTop(),
                    insets.getSystemWindowInsetRight(), insets.getSystemWindowInsetBottom());
            return insets;
        });
        setContentView(scroll);
        scroll.requestApplyInsets();
        scroll.post(() -> scroll.scrollTo(0,previousY));
        text(body, "dollar", 13, 0xff2c786d).setTypeface(null, Typeface.BOLD);
        text(body, "نبض ارز، روی صفحهٔ تو", 26, 0xff183d38);
        text(body, "بازار آزاد ایران · قیمت‌ها به تومان", 13, 0xff758780);
        LinearLayout tabs = horizontal(body);
        button(tabs, symbol.equals("usd") ? "● دلار 🇺🇸" : "دلار 🇺🇸", () -> select("usd"));
        button(tabs, symbol.equals("euro") ? "● یورو 🇪🇺" : "یورو 🇪🇺", () -> select("euro"));

        Price current = latest.isEmpty() ? null : latest.get(0);
        Price previous = latest.size() > 1 ? latest.get(1) : null;
        LinearLayout hero = card(body);
        text(hero, symbol.equals("usd") ? "🇺🇸   US Dollar" : "🇪🇺   Euro", 18, 0xff56716e).setTextDirection(View.TEXT_DIRECTION_LTR);
        TextView value = text(hero, current == null ? "—" : Price.toman(current.rial), 44, 0xff183d38);
        value.setTextDirection(View.TEXT_DIRECTION_LTR);
        value.setGravity(Gravity.CENTER);
        text(hero, "تومان", 13, 0xff758780).setGravity(Gravity.CENTER);
        text(hero, Price.change(current, previous) + (previous == null || current == null || !current.source.equals(previous.source) ? "" : " تومان نسبت به ثبت قبلی"), 15,
                current == null || previous == null || !current.source.equals(previous.source) ? 0xff758780 : current.rial < previous.rial ? 0xffb25764 : 0xff258270);
        text(hero, current == null ? "هنوز نرخی دریافت نشده؛ اینترنت را بررسی و به‌روزرسانی کنید."
                : (Refresh.stale(this, current) ? "نرخ ذخیره‌شده / قدیمی\n" : "") + "زمان منبع: " + stamp(current.sourceTime)
                + "\nدریافت در گوشی: " + stamp(current.receivedTime), 12, 0xff758780);
        if (current != null) text(hero, "منبع نرخ منتخب: " + PriceSource.byId(current.source).title, 13, 0xff2c786d);
        Button update = button(body, refreshing ? "در حال دریافت…" : "به‌روزرسانی قیمت‌ها ↻", this::refresh);
        update.setEnabled(!refreshing);
        text(body, "مقایسهٔ همهٔ APIها", 22, 0xff183d38);
        text(body, "ویجت اولین پاسخ معتبر را نشان می‌دهد؛ بقیهٔ منابع برای مقایسه تا پایان مهلت بررسی می‌شوند. قیمت‌ها ممکن است به‌علت زمان و منبع داده متفاوت باشند.", 12, 0xff758780);
        for (int i = 0; i < usdSources.size(); i++) {
            SourceSnapshot usd = usdSources.get(i), euro = euroSources.get(i);
            LinearLayout sourceCard = card(body);
            text(sourceCard, usd.source.title + (current != null && current.source.equals(usd.source.id) ? " · منبع منتخب این ارز" : ""), 18, 0xff183d38);
            sourceQuote(sourceCard, "دلار", usd);
            sourceQuote(sourceCard, "یورو", euro);
            text(sourceCard, usd.source.url, 10, 0xff758780).setTextDirection(View.TEXT_DIRECTION_LTR);
        }
        text(body, "روند آخرین ۶۰ ثبت", 18, 0xff183d38);
        text(body, "نمودار نرخ‌های منتخب است؛ بین دو منبع متفاوت خط روند رسم نمی‌شود.", 12, 0xff758780);
        LinearLayout chartCard = card(body);
        chartCard.addView(new TrendChart(this, latest), new LinearLayout.LayoutParams(-1, dp(170)));

        LinearLayout tools = horizontal(body);
        button(tools, "افزودن ویجت ＋", this::pinWidget);
        button(tools, "خروجی CSV", this::export);
        text(body, "تاریخچهٔ نرخ منتخب ویجت", 22, 0xff183d38);
        text(body, count + " نرخ ذخیره‌شده · تاریخ میلادی، ساعت محلی گوشی", 12, 0xff758780);
        if (rows.isEmpty()) text(body, "تاریخچه از اولین دریافت روی همین گوشی شروع می‌شود؛ سوابق بازار قبل از نصب در دسترس نیست.", 14, 0xff758780);
        int visible = Math.min(PAGE_SIZE, rows.size());
        for (int i=0; i<visible; i++) {
            Price row = rows.get(i);
            Price before = i+1 < rows.size() ? rows.get(i+1) : null;
            LinearLayout item = card(body);
            TextView priceText = text(item, Price.toman(row.rial) + " تومان", 23, 0xff183d38);
            priceText.setTextDirection(View.TEXT_DIRECTION_LTR);
            text(item, Price.change(row, before), 14, before == null || !row.source.equals(before.source) || row.rial == before.rial ? 0xff758780
                    : row.rial > before.rial ? 0xff258270 : 0xffb25764);
            text(item, "API: " + PriceSource.byId(row.source).title + "\nزمان منبع: " + stamp(row.sourceTime) + "\nثبت در گوشی: " + stamp(row.receivedTime), 12, 0xff758780);
        }
        LinearLayout pages = horizontal(body);
        if (page > 0) button(pages, "جدیدتر", () -> { page--; resetScroll = true; load(); });
        if ((page+1)*PAGE_SIZE < count) button(pages, "قدیمی‌تر", () -> { page++; resetScroll = true; load(); });
        text(body, "بررسی خودکار حدود هر ۳۰ دقیقه؛ محدودیت باتری و اینترنت ممکن است آن را عقب بیندازد. تغییر هر نرخ نسبت به ثبت قبلی همین گوشی است، نه تغییر روزانهٔ بازار. نرخ یکسان با زمان منبع یکسان تکراری ذخیره نمی‌شود.", 12, 0xff758780);
        text(body, "منابع: PriceToDay، Iran Market Data و نرخ‌سنج؛ بعضی منابع دادهٔ پایهٔ مشترک TGJU دارند\nتاریخچه فقط روی این گوشی نگهداری می‌شود. با حذف برنامه پاک خواهد شد؛ برای نگهداری، خروجی CSV بگیرید.", 12, 0xff758780);
    }

    private void sourceQuote(LinearLayout parent, String name, SourceSnapshot snapshot) {
        String amount = snapshot.price == null ? "—" : Price.toman(snapshot.price.rial) + " تومان";
        text(parent, name + ": " + amount, 20, 0xff183d38);
        String state;
        if (snapshot.status.equals("pending")) state = "در حال بررسی…";
        else if (snapshot.status.equals("error")) state = "ناموفق: " + snapshot.error
                + (snapshot.price == null ? "" : "؛ قیمت بالا ذخیره‌شده است");
        else if (snapshot.status.equals("ok")) state = "پاسخ معتبر · " + snapshot.latencyMs + " ms";
        else state = "هنوز بررسی نشده";
        if (snapshot.checkedAt > 0) state += "\nآخرین تلاش: " + stamp(snapshot.checkedAt);
        if (snapshot.price != null) state += "\nزمان نرخ: " + stamp(snapshot.price.sourceTime)
                + (System.currentTimeMillis()-snapshot.price.sourceTime > 60*60*1000L ? " · قدیمی / ذخیره‌شده" : "");
        text(parent, state, 12, snapshot.status.equals("error") ? 0xffb25764 : 0xff758780);
    }

    private void select(String value) { symbol = value; page = 0; resetScroll = true; load(); }
    private void pinWidget() {
        AppWidgetManager manager = AppWidgetManager.getInstance(this);
        if (Build.VERSION.SDK_INT >= 26 && manager.isRequestPinAppWidgetSupported()) {
            manager.requestPinAppWidget(new ComponentName(this, DollarWidget.class), null, null);
        } else {
            Toast.makeText(this, "روی فضای خالی صفحهٔ اصلی نگه دارید، ویجت‌ها را باز کنید و dollar را انتخاب کنید.", Toast.LENGTH_LONG).show();
        }
    }
    private void export() {
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE)
                .setType("text/csv").putExtra(Intent.EXTRA_TITLE, "dollar-history.csv");
        try { startActivityForResult(intent, EXPORT); }
        catch (android.content.ActivityNotFoundException e) {
            Toast.makeText(this, "برنامهٔ مدیریت فایل برای ذخیرهٔ خروجی پیدا نشد", Toast.LENGTH_LONG).show();
        }
    }
    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != EXPORT || result != RESULT_OK || data == null || data.getData() == null) return;
        android.net.Uri uri = data.getData();
        Refresh.IO.execute(() -> {
            boolean success = false;
            try (PriceStore store = new PriceStore(getApplicationContext());
                 OutputStream output = getContentResolver().openOutputStream(uri, "wt")) {
                if (output == null) throw new java.io.IOException("No output stream");
                try (OutputStreamWriter writer = new OutputStreamWriter(output, StandardCharsets.UTF_8)) {
                    writer.write('\ufeff');
                    writer.write("symbol,source,price_toman,change_toman,source_time_utc,received_time_utc\n");
                    SimpleDateFormat utc = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US);
                    utc.setTimeZone(java.util.TimeZone.getTimeZone("UTC"));
                    java.util.Map<String, Long> previousBySource = new java.util.HashMap<>();
                    try (android.database.Cursor c = store.getReadableDatabase().rawQuery(
                            "SELECT symbol,rial,source_time,received_time,source FROM source_history ORDER BY symbol,source_time,id", null)) {
                        while (c.moveToNext()) {
                            String s = c.getString(0);
                            long rial = c.getLong(1);
                            String source = c.getString(4), key = s + ":" + source;
                            Long before = previousBySource.put(key, rial);
                            // Ungrouped decimal numbers so separators never split CSV fields.
                            writer.write(s + "," + source + "," + java.math.BigDecimal.valueOf(rial, 1).toPlainString() + ","
                                    + (before != null ? java.math.BigDecimal.valueOf(rial-before, 1).toPlainString() : "")
                                    + "," + utc.format(c.getLong(2)) + "," + utc.format(c.getLong(3)) + "\n");
                        }
                    }
                }
                success = true;
            } catch (Exception ignored) { }
            final boolean done = success;
            runOnUiThread(() -> {
                if (!isDestroyed()) Toast.makeText(this, done ? "تاریخچهٔ هر دو ارز از همهٔ منابع ذخیره شد" : "ذخیرهٔ خروجی ناموفق بود؛ دوباره تلاش کنید", Toast.LENGTH_LONG).show();
            });
        });
    }

    private String stamp(long time) { return new SimpleDateFormat("yyyy/MM/dd · HH:mm", Locale.US).format(time); }
    private int dp(int n) { return Math.round(n*getResources().getDisplayMetrics().density); }
    private TextView text(LinearLayout parent, String value, int size, int color) {
        TextView v = new TextView(this);
        v.setText(value); v.setTextSize(size); v.setTextColor(color);
        v.setPadding(0, dp(5), 0, dp(5));
        parent.addView(v, new LinearLayout.LayoutParams(-1, -2));
        return v;
    }
    private LinearLayout horizontal(LinearLayout parent) {
        LinearLayout row = new LinearLayout(this); row.setGravity(Gravity.CENTER);
        parent.addView(row, new LinearLayout.LayoutParams(-1, -2)); return row;
    }
    private LinearLayout card(LinearLayout parent) {
        LinearLayout v = new LinearLayout(this); v.setOrientation(LinearLayout.VERTICAL);
        v.setPadding(dp(18), dp(14), dp(18), dp(14));
        GradientDrawable bg = new GradientDrawable(GradientDrawable.Orientation.TL_BR, new int[]{Color.WHITE, 0xffe8f4f3});
        bg.setCornerRadius(dp(24)); v.setBackground(bg);
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, -2);
        p.setMargins(0, dp(10), 0, dp(8)); parent.addView(v,p); return v;
    }
    private Button button(LinearLayout parent, String label, Runnable action) {
        Button v = new Button(this); v.setText(label); v.setTextSize(13); v.setAllCaps(false);
        v.setTextColor(0xff246c62); v.setOnClickListener(view -> action.run());
        LinearLayout.LayoutParams p = parent == body ? new LinearLayout.LayoutParams(-1, -2)
                : new LinearLayout.LayoutParams(0, -2, 1);
        parent.addView(v,p); return v;
    }
}
