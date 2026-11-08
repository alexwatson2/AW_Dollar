package com.aw.dollar;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.Callable;
import java.util.concurrent.CompletionService;
import java.util.concurrent.ExecutorCompletionService;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

/** First VALID response per currency wins; other sources still complete for the comparison UI. */
public final class SourceRace {
    public interface Listener {
        boolean eligible(Price price);
        void completed(SourceResult result, boolean winner);
    }
    private SourceRace() { }

    public static Set<String> run(List<Callable<SourceResult>> tasks, long budgetMs, Listener listener)
            throws InterruptedException {
        ExecutorService pool = Executors.newFixedThreadPool(Math.max(1, tasks.size()));
        CompletionService<SourceResult> completions = new ExecutorCompletionService<>(pool);
        List<Future<SourceResult>> futures = new ArrayList<>();
        Set<String> winners = new HashSet<>();
        long deadline = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(budgetMs);
        try {
            for (Callable<SourceResult> task : tasks) futures.add(completions.submit(task));
            for (int i = 0; i < tasks.size(); i++) {
                long remaining = deadline - System.nanoTime();
                if (remaining <= 0) break;
                Future<SourceResult> future = completions.poll(remaining, TimeUnit.NANOSECONDS);
                if (future == null) break;
                SourceResult result;
                try { result = future.get(); }
                catch (java.util.concurrent.ExecutionException e) { continue; }
                boolean winner = result.success() && !winners.contains(result.symbol)
                        && listener.eligible(result.price);
                if (winner) winners.add(result.symbol);
                listener.completed(result, winner);
            }
            return winners;
        } finally {
            for (Future<?> future : futures) future.cancel(true);
            pool.shutdownNow();
        }
    }
}
