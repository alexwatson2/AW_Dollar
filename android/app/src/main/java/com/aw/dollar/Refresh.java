package com.aw.dollar;

import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.Context;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class Refresh {
    public static final ExecutorService IO = Executors.newSingleThreadExecutor();
    // Keep UI/database reads independent of potentially slow providers.
    public static final ExecutorService NETWORK = Executors.newSingleThreadExecutor();
    private static final java.util.concurrent.CopyOnWriteArrayList<Runnable> LISTENERS = new java.util.concurrent.CopyOnWriteArrayList<>();
    public static void addListener(Runnable listener) { LISTENERS.addIfAbsent(listener); }
    public static void removeListener(Runnable listener) { LISTENERS.remove(listener); }
    private static final int JOB_ID = 301;
    private Refresh() { }

    public static void schedule(Context context) {
        JobScheduler scheduler = (JobScheduler)context.getSystemService(Context.JOB_SCHEDULER_SERVICE);
        for (JobInfo job : scheduler.getAllPendingJobs()) if (job.getId() == JOB_ID) return;
        scheduler.schedule(new JobInfo.Builder(JOB_ID,new ComponentName(context,RefreshJobService.class))
                .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY).setPeriodic(30*60*1000L).setPersisted(true).build());
    }
    public static boolean fetchAll(Context context) {
        Set<String> completed = new HashSet<>();
        Set<String> won = new HashSet<>();
        long started = System.currentTimeMillis();
        context.getSharedPreferences("refresh",0).edit().putLong("sources_checked",started).apply();
        try (PriceStore store = new PriceStore(context)) {
            for (PriceSource source : PriceSource.values()) for (String symbol : new String[]{"usd","euro"})
                store.status(source,symbol,"pending","",started,0);
            changed();
            List<Callable<SourceResult>> tasks = new ArrayList<>();
            for (PriceSource source : PriceSource.values()) for (String symbol : new String[]{"usd","euro"})
                tasks.add(() -> PriceApi.fetch(source,symbol));
            try {
                won.addAll(SourceRace.run(tasks,18000,new SourceRace.Listener() {
                    @Override public boolean eligible(Price price) {
                        List<Price> cached = store.history(price.symbol,1,0);
                        return System.currentTimeMillis()-price.sourceTime <= 7*24*60*60*1000L
                                && (cached.isEmpty() || price.sourceTime >= cached.get(0).sourceTime);
                    }
                    @Override public void completed(SourceResult result, boolean winner) {
                        completed.add(result.source.id + ":" + result.symbol);
                        store.sourceResult(result);
                        if (winner) {
                            won.add(result.symbol);
                            store.save(result.price);
                            context.getSharedPreferences("refresh",0).edit()
                                    .putBoolean(result.symbol+"_failed",false)
                                    .putLong(result.symbol+"_checked",result.checkedAt).apply();
                            PriceWidget.updateAll(context); // Publish immediately; do NOT wait for the slow sources.
                        }
                        changed();
                    }
                }));
            } catch (InterruptedException e) { Thread.currentThread().interrupt(); }
            finally {
                for (PriceSource source : PriceSource.values()) for (String symbol : new String[]{"usd","euro"}) {
                    if (!completed.contains(source.id+":"+symbol))
                        store.status(source,symbol,"error",Thread.currentThread().isInterrupted() ? "بررسی لغو شد" : "مهلت بررسی تمام شد",System.currentTimeMillis(),System.currentTimeMillis()-started);
                }
                for (String symbol : new String[]{"usd","euro"}) {
                    if (!won.contains(symbol)) context.getSharedPreferences("refresh",0).edit().putBoolean(symbol+"_failed",true).apply();
                }
                PriceWidget.updateAll(context); changed();
            }
        }
        return won.size() == 2;
    }
    private static void changed() {
        new android.os.Handler(android.os.Looper.getMainLooper()).post(() -> {
            for (Runnable listener : LISTENERS) listener.run();
        });
    }
    public static boolean stale(Context context, Price price) {
        return price == null || context.getSharedPreferences("refresh",0).getBoolean(price.symbol+"_failed",false)
                || System.currentTimeMillis()-price.sourceTime > 60*60*1000L;
    }
}
