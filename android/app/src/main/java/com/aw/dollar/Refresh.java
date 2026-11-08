package com.aw.dollar;

import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.Context;
import android.content.SharedPreferences;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class Refresh {
    public static final ExecutorService IO = Executors.newSingleThreadExecutor();
    private static final int JOB_ID = 301;
    private Refresh() { }

    public static void schedule(Context context) {
        JobScheduler scheduler = (JobScheduler) context.getSystemService(Context.JOB_SCHEDULER_SERVICE);
        for (JobInfo job : scheduler.getAllPendingJobs()) {
            if (job.getId() == JOB_ID) return;
        }
        scheduler.schedule(new JobInfo.Builder(JOB_ID, new ComponentName(context, RefreshJobService.class))
                .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY)
                .setPeriodic(30 * 60 * 1000L).setPersisted(true).build());
    }

    /** Each currency succeeds independently; never replace cached data with zero on an error. */
    public static boolean fetchAll(Context context) {
        boolean success = true;
        try (PriceStore store = new PriceStore(context)) {
            for (String symbol : new String[]{"usd", "euro"}) {
                if (Thread.currentThread().isInterrupted()) return false;
                SharedPreferences.Editor prefs = context.getSharedPreferences("refresh", 0).edit();
                try {
                    store.save(PriceApi.fetch(symbol));
                    prefs.putBoolean(symbol + "_failed", false).putLong(symbol + "_checked", System.currentTimeMillis());
                } catch (Exception e) {
                    success = false;
                    prefs.putBoolean(symbol + "_failed", true);
                }
                prefs.apply();
            }
        }
        PriceWidget.updateAll(context);
        return success;
    }

    public static boolean stale(Context context, Price price) {
        return price == null || context.getSharedPreferences("refresh", 0).getBoolean(price.symbol + "_failed", false)
                || System.currentTimeMillis() - price.sourceTime > 60 * 60 * 1000L;
    }
}
