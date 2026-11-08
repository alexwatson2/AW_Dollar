package com.aw.dollar;

import android.app.job.JobParameters;
import android.app.job.JobService;
import android.os.Handler;
import android.os.Looper;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.Future;

public final class RefreshJobService extends JobService {
    // A periodic job and a widget's one-off job may coexist in the same service.
    // Lifecycle callbacks and this map are confined to the main thread.
    private final Map<JobParameters, Future<?>> tasks = new HashMap<>();
    @Override public boolean onStartJob(JobParameters params) {
        Future<?> task = Refresh.IO.submit(() -> {
            boolean success = Refresh.fetchAll(getApplicationContext());
            new Handler(Looper.getMainLooper()).post(() -> {
                if (tasks.remove(params) != null) jobFinished(params, !success);
            });
        });
        tasks.put(params, task);
        return true;
    }
    @Override public boolean onStopJob(JobParameters params) {
        Future<?> task = tasks.remove(params);
        if (task != null) task.cancel(true);
        return true;
    }
}
