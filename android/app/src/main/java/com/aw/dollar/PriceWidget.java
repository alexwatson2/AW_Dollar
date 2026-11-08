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

public abstract class PriceWidget extends AppWidgetProvider {
    protected abstract String symbol();

    @Override public void onUpdate(Context context, AppWidgetManager manager, int[] ids) {
        Refresh.schedule(context);
        update(context, manager, ids, symbol());
        // Receiver finishes immediately; JobScheduler owns all network work.
        android.app.job.JobScheduler scheduler = (android.app.job.JobScheduler) context.getSystemService(Context.JOB_SCHEDULER_SERVICE);
        scheduler.schedule(new android.app.job.JobInfo.Builder(302, new ComponentName(context, RefreshJobService.class))
                .setRequiredNetworkType(android.app.job.JobInfo.NETWORK_TYPE_ANY).setMinimumLatency(1000).build());
    }

    public static void updateAll(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        update(context, manager, manager.getAppWidgetIds(new ComponentName(context, DollarWidget.class)), "usd");
        update(context, manager, manager.getAppWidgetIds(new ComponentName(context, EuroWidget.class)), "euro");
    }

    private static void update(Context context, AppWidgetManager manager, int[] ids, String symbol) {
        if (ids.length == 0) return;
        List<Price> prices;
        try (PriceStore store = new PriceStore(context)) { prices = store.history(symbol, 2, 0); }
        Price current = prices.isEmpty() ? null : prices.get(0);
        Price previous = prices.size() < 2 ? null : prices.get(1);
        for (int id : ids) {
            RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.price_widget);
            views.setTextViewText(R.id.flag, symbol.equals("usd") ? "🇺🇸" : "🇪🇺");
            views.setTextViewText(R.id.currency_name, symbol.equals("usd") ? "US Dollar" : "Euro");
            views.setTextViewText(R.id.price, current == null ? "—" : Price.toman(current.rial));
            views.setTextViewText(R.id.change, Price.change(current, previous));
            views.setTextColor(R.id.change, current == null || previous == null || current.rial == previous.rial ? 0xff687b79
                    : current.rial > previous.rial ? 0xff258270 : 0xffb25764);
            String stamp = current == null ? "برای شروع لمس کنید" :
                    (Refresh.stale(context, current) ? "ذخیره‌شده · " : "") + new SimpleDateFormat("MM/dd HH:mm", Locale.US).format(current.sourceTime);
            views.setTextViewText(R.id.updated, stamp);
            Intent open = new Intent(context, MainActivity.class).putExtra("symbol", symbol)
                    .setAction("com.aw.dollar.OPEN." + symbol).setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
            views.setOnClickPendingIntent(R.id.widget_root, PendingIntent.getActivity(context, id, open,
                    PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
            manager.updateAppWidget(id, views);
        }
    }
}
