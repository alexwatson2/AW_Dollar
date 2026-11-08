package com.aw.dollar;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.widget.RemoteViews;
import java.text.SimpleDateFormat;
import java.util.List;
import java.util.Locale;

/** One compact widget, containing both currencies. No history or verbose status on its face. */
public abstract class PriceWidget extends AppWidgetProvider {
    @Override public void onUpdate(Context context, AppWidgetManager manager, int[] ids) {
        Refresh.schedule(context);
        update(context, manager, ids);
        // Receiver finishes immediately; JobScheduler owns all network work.
        android.app.job.JobScheduler scheduler = (android.app.job.JobScheduler) context.getSystemService(Context.JOB_SCHEDULER_SERVICE);
        scheduler.schedule(new android.app.job.JobInfo.Builder(302, new ComponentName(context, RefreshJobService.class))
                .setRequiredNetworkType(android.app.job.JobInfo.NETWORK_TYPE_ANY).setMinimumLatency(1000).build());
    }

    public static void updateAll(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        update(context, manager, manager.getAppWidgetIds(new ComponentName(context, DollarWidget.class)));
    }

    private static void update(Context context, AppWidgetManager manager, int[] ids) {
        if (ids.length == 0) return;
        List<Price> usd;
        List<Price> euro;
        try (PriceStore store = new PriceStore(context)) {
            usd = store.history("usd", 2, 0);
            euro = store.history("euro", 2, 0);
        }
        long updatedAt = WidgetSummary.updatedAt(latest(usd), latest(euro));
        for (int id : ids) {
            RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.price_widget);
            bindRow(context, views, id, "usd", usd, R.id.usd_row, R.id.usd_price, R.id.usd_trend);
            bindRow(context, views, id, "euro", euro, R.id.eur_row, R.id.eur_price, R.id.eur_trend);
            String stamp = updatedAt == 0 ? "—" :
                    new SimpleDateFormat("yyyy/MM/dd HH:mm", Locale.US).format(updatedAt);
            views.setTextViewText(R.id.updated, stamp);
            // Color alone is not the only stale indicator: TalkBack and the app expose details.
            boolean stale = Refresh.stale(context, latest(usd)) || Refresh.stale(context, latest(euro));
            views.setTextColor(R.id.updated, stale ? 0xffdbc08d : 0xffb7bdc4);
            views.setContentDescription(R.id.updated, updatedAt == 0 ? "هنوز نرخی دریافت نشده" :
                    "زمان قدیمی‌ترین نرخ نمایش‌داده‌شده: " + stamp + (stale ? "؛ نرخ ذخیره‌شده یا قدیمی" : ""));
            views.setOnClickPendingIntent(R.id.widget_root, openCurrency(context, id, "usd"));
            manager.updateAppWidget(id, views);
        }
    }

    private static Price latest(List<Price> prices) { return prices.isEmpty() ? null : prices.get(0); }

    private static void bindRow(Context context, RemoteViews views, int id, String symbol, List<Price> prices,
                                int rowId, int priceId, int trendId) {
        Price current = latest(prices);
        Price previous = prices.size() < 2 ? null : prices.get(1);
        String value = current == null ? "—" : Price.toman(current.rial);
        String trend = WidgetSummary.trend(current, previous);
        views.setTextViewText(priceId, value);
        views.setTextViewText(trendId, trend);
        views.setTextColor(trendId, trend.equals("—") ? 0xffb7bdc4 :
                current.rial > previous.rial ? 0xff72d5ad : 0xffff8e99);
        String name = symbol.equals("usd") ? "دلار" : "یورو";
        views.setContentDescription(rowId, name + ": " + (current == null ? "هنوز دریافت نشده" :
                value + " تومان؛ " + Price.change(current, previous)) + "؛ باز کردن تاریخچه");
        views.setOnClickPendingIntent(rowId, openCurrency(context, id, symbol));
    }

    private static PendingIntent openCurrency(Context context, int id, String symbol) {
        Intent open = new Intent(context, MainActivity.class).putExtra("symbol", symbol)
                .setAction("com.aw.dollar.OPEN." + symbol)
                .setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        // Distinct actions prevent USD/EUR PendingIntents overwriting one another.
        return PendingIntent.getActivity(context, id, open,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
}
