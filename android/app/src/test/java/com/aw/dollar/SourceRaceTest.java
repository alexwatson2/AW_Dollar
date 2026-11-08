package com.aw.dollar;

import org.junit.Test;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Set;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import static org.junit.Assert.*;

public class SourceRaceTest {
    private SourceResult result(PriceSource source,String symbol,boolean success) {
        return new SourceResult(source,symbol,success ? new Price(symbol,1000,100,200,source.id) : null,
                success ? "" : "offline",200,10);
    }
    @Test public void failureDoesNotWinAndSlowSuccessCannotOverwriteFirstValid() throws Exception {
        List<String> winners = new ArrayList<>(), completed = new ArrayList<>();
        CountDownLatch winnerPublished = new CountDownLatch(1);
        List<Callable<SourceResult>> tasks = Arrays.asList(
            () -> result(PriceSource.PRICETODAY,"usd",false),
            () -> result(PriceSource.IRAN_MARKET,"usd",true),
            () -> { assertTrue(winnerPublished.await(2,TimeUnit.SECONDS)); return result(PriceSource.NERKHSANJ,"usd",true); },
            () -> result(PriceSource.NERKHSANJ,"euro",true));
        Set<String> won = SourceRace.run(tasks,3000,new SourceRace.Listener() {
            public boolean eligible(Price p) { return true; }
            public void completed(SourceResult r,boolean winner) {
                completed.add(r.source.id+":"+r.symbol);
                if(winner) { winners.add(r.source.id+":"+r.symbol); if(r.symbol.equals("usd")) winnerPublished.countDown(); }
            }
        });
        assertEquals(2,won.size()); assertEquals(4,completed.size());
        assertTrue(winners.contains("iranmarket:usd"));
        assertFalse(winners.contains("nerkhsanj:usd"));
    }
    @Test public void rejectedOldDataFallsThroughToNextSource() throws Exception {
        List<String> winners = new ArrayList<>();
        SourceRace.run(Arrays.asList(() -> result(PriceSource.PRICETODAY,"usd",true),
                () -> result(PriceSource.IRAN_MARKET,"usd",true)),1000,new SourceRace.Listener() {
            public boolean eligible(Price p) { return !p.source.equals("pricetoday"); }
            public void completed(SourceResult r,boolean winner) { if(winner) winners.add(r.source.id); }
        });
        assertEquals(Arrays.asList("iranmarket"),winners);
    }
    @Test public void allFailuresLeaveNoWinner() throws Exception {
        Set<String> won=SourceRace.run(Arrays.asList(() -> result(PriceSource.PRICETODAY,"usd",false)),1000,new SourceRace.Listener() {
            public boolean eligible(Price p) { throw new AssertionError(); }
            public void completed(SourceResult r,boolean winner) { assertFalse(winner); }
        });
        assertTrue(won.isEmpty());
    }
    @Test public void deadlineCancelsHangingProvider() throws Exception {
        CountDownLatch cancelled = new CountDownLatch(1);
        SourceRace.run(Arrays.asList(() -> {
            try { Thread.sleep(10000); return result(PriceSource.PRICETODAY,"usd",true); }
            catch(InterruptedException e) { cancelled.countDown(); throw e; }
        }),100,new SourceRace.Listener() {
            public boolean eligible(Price p) { return true; }
            public void completed(SourceResult r,boolean winner) { fail("Deadline ignored"); }
        });
        assertTrue(cancelled.await(2,TimeUnit.SECONDS));
    }
}
