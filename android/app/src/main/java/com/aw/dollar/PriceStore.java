package com.aw.dollar;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import java.util.ArrayList;
import java.util.List;

/** Winner history and an independent cache/status for EVERY source. No destructive upgrade. */
public final class PriceStore extends SQLiteOpenHelper {
    public PriceStore(Context context) { super(context, "price-history.db", null, 2); }

    private static final String QUOTES = "(id INTEGER PRIMARY KEY, symbol TEXT NOT NULL, rial INTEGER NOT NULL, source_time INTEGER NOT NULL, received_time INTEGER NOT NULL, source TEXT NOT NULL, UNIQUE(symbol, source, source_time, rial))";
    @Override public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE quotes " + QUOTES);
        db.execSQL("CREATE INDEX quotes_time ON quotes(symbol, source_time DESC, id DESC)");
        createSources(db);
    }
    private void createSources(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE source_history " + QUOTES);
        db.execSQL("CREATE INDEX source_history_time ON source_history(symbol,source,source_time DESC)");
        db.execSQL("CREATE TABLE source_latest (source TEXT NOT NULL, symbol TEXT NOT NULL, rial INTEGER NOT NULL, source_time INTEGER NOT NULL, received_time INTEGER NOT NULL, PRIMARY KEY(source,symbol))");
        db.execSQL("CREATE TABLE source_status (source TEXT NOT NULL, symbol TEXT NOT NULL, status TEXT NOT NULL, error TEXT NOT NULL, checked_at INTEGER NOT NULL, latency INTEGER NOT NULL, PRIMARY KEY(source,symbol))");
    }
    @Override public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        if (oldVersion < 2) {
            db.execSQL("CREATE TABLE quotes_new " + QUOTES);
            db.execSQL("INSERT INTO quotes_new (id,symbol,rial,source_time,received_time,source) SELECT id,symbol,rial,source_time,received_time,'pricetoday' FROM quotes");
            db.execSQL("DROP TABLE quotes");
            db.execSQL("ALTER TABLE quotes_new RENAME TO quotes");
            db.execSQL("CREATE INDEX quotes_time ON quotes(symbol, source_time DESC, id DESC)");
            createSources(db);
            db.execSQL("INSERT INTO source_history SELECT * FROM quotes");
            db.execSQL("INSERT INTO source_latest SELECT source,symbol,rial,source_time,received_time FROM quotes q WHERE id=(SELECT id FROM quotes WHERE symbol=q.symbol ORDER BY source_time DESC,id DESC LIMIT 1)");
        }
    }
    private ContentValues priceValues(Price price) {
        ContentValues row = new ContentValues();
        row.put("symbol",price.symbol); row.put("rial",price.rial);
        row.put("source_time",price.sourceTime); row.put("received_time",price.receivedTime);
        row.put("source",price.source); return row;
    }
    public void save(Price price) {
        getWritableDatabase().insertWithOnConflict("quotes",null,priceValues(price),SQLiteDatabase.CONFLICT_IGNORE);
        getWritableDatabase().insertWithOnConflict("source_history",null,priceValues(price),SQLiteDatabase.CONFLICT_IGNORE);
    }
    public void sourceResult(SourceResult result) {
        SQLiteDatabase db = getWritableDatabase();
        db.beginTransaction();
        try {
            if (result.success()) {
                db.insertWithOnConflict("source_latest",null,priceValues(result.price),SQLiteDatabase.CONFLICT_REPLACE);
                db.insertWithOnConflict("source_history",null,priceValues(result.price),SQLiteDatabase.CONFLICT_IGNORE);
            }
            status(result.source,result.symbol,result.success() ? "ok" : "error",result.error,result.checkedAt,result.latencyMs);
            db.setTransactionSuccessful();
        } finally { db.endTransaction(); }
    }
    public void status(PriceSource source, String symbol, String status, String error, long checkedAt, long latency) {
        ContentValues row = new ContentValues();
        row.put("source",source.id); row.put("symbol",symbol); row.put("status",status);
        row.put("error",error); row.put("checked_at",checkedAt); row.put("latency",latency);
        getWritableDatabase().insertWithOnConflict("source_status",null,row,SQLiteDatabase.CONFLICT_REPLACE);
    }
    public List<SourceSnapshot> sources(String symbol) {
        List<SourceSnapshot> results = new ArrayList<>();
        for (PriceSource source : PriceSource.values()) {
            Price price = null;
            try (Cursor c = getReadableDatabase().rawQuery("SELECT rial,source_time,received_time FROM source_latest WHERE source=? AND symbol=?",new String[]{source.id,symbol})) {
                if (c.moveToFirst()) price = new Price(symbol,c.getLong(0),c.getLong(1),c.getLong(2),source.id);
            }
            String status = "none", error = ""; long checkedAt = 0, latency = 0;
            try (Cursor c = getReadableDatabase().rawQuery("SELECT status,error,checked_at,latency FROM source_status WHERE source=? AND symbol=?",new String[]{source.id,symbol})) {
                if (c.moveToFirst()) { status=c.getString(0); error=c.getString(1); checkedAt=c.getLong(2); latency=c.getLong(3); }
            }
            results.add(new SourceSnapshot(source,price,status,error,checkedAt,latency));
        }
        return results;
    }
    public List<Price> history(String symbol, int limit, int offset) {
        List<Price> result = new ArrayList<>();
        try (Cursor c = getReadableDatabase().query("quotes",new String[]{"symbol","rial","source_time","received_time","source"},
                "symbol = ?",new String[]{symbol},null,null,"source_time DESC, id DESC",limit + " OFFSET " + offset)) {
            while (c.moveToNext()) result.add(new Price(c.getString(0),c.getLong(1),c.getLong(2),c.getLong(3),c.getString(4)));
        }
        return result;
    }
    public int count(String symbol) {
        try (Cursor c = getReadableDatabase().rawQuery("SELECT COUNT(*) FROM quotes WHERE symbol = ?",new String[]{symbol})) {
            c.moveToFirst(); return c.getInt(0);
        }
    }
}
