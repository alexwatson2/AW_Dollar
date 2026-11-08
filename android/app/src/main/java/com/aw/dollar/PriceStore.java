package com.aw.dollar;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import java.util.ArrayList;
import java.util.List;

/** Local, unbounded history. Identical upstream snapshots are not saved twice. */
public final class PriceStore extends SQLiteOpenHelper {
    public PriceStore(Context context) { super(context, "price-history.db", null, 1); }

    @Override public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE quotes (id INTEGER PRIMARY KEY, symbol TEXT NOT NULL, rial INTEGER NOT NULL, source_time INTEGER NOT NULL, received_time INTEGER NOT NULL, UNIQUE(symbol, source_time, rial))");
        db.execSQL("CREATE INDEX quotes_time ON quotes(symbol, source_time DESC, id DESC)");
    }

    @Override public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        // Future upgrades must migrate this table, not delete the user's history.
        throw new IllegalStateException("Database migration required");
    }

    public void save(Price price) {
        ContentValues row = new ContentValues();
        row.put("symbol", price.symbol);
        row.put("rial", price.rial);
        row.put("source_time", price.sourceTime);
        row.put("received_time", price.receivedTime);
        getWritableDatabase().insertWithOnConflict("quotes", null, row, SQLiteDatabase.CONFLICT_IGNORE);
    }

    public List<Price> history(String symbol, int limit, int offset) {
        List<Price> result = new ArrayList<>();
        try (Cursor c = getReadableDatabase().query("quotes", new String[]{"symbol", "rial", "source_time", "received_time"},
                "symbol = ?", new String[]{symbol}, null, null, "source_time DESC, id DESC", limit + " OFFSET " + offset)) {
            while (c.moveToNext()) result.add(new Price(c.getString(0), c.getLong(1), c.getLong(2), c.getLong(3)));
        }
        return result;
    }

    public int count(String symbol) {
        try (Cursor c = getReadableDatabase().rawQuery("SELECT COUNT(*) FROM quotes WHERE symbol = ?", new String[]{symbol})) {
            c.moveToFirst();
            return c.getInt(0);
        }
    }
}
