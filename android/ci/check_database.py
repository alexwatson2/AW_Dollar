#!/usr/bin/env python3
"""Exercise the exact schema/migration SQL from PriceStore using SQLite, without an SDK."""
from pathlib import Path
import re
import sqlite3
import unittest

SOURCE = (Path(__file__).resolve().parents[1] / "app/src/main/java/com/aw/dollar/PriceStore.java").read_text()
QUOTES = re.search(r'QUOTES = "([^"]+)"', SOURCE).group(1)


def run_statements(db, text):
    for statement in re.findall(r'db.execSQL\("([^"]+)"( \+ QUOTES)?\)', text):
        db.execute(statement[0] + (QUOTES if statement[1] else ""))


def create_sources(db):
    text = SOURCE.split("private void createSources", 1)[1].split("@Override", 1)[0]
    run_statements(db, text)


class MigrationTest(unittest.TestCase):
    def test_upgrade_preserves_ids_prices_dates_and_adds_source_caches(self):
        db = sqlite3.connect(":memory:")
        db.execute("CREATE TABLE quotes(id INTEGER PRIMARY KEY,symbol TEXT NOT NULL,rial INTEGER NOT NULL,source_time INTEGER NOT NULL,received_time INTEGER NOT NULL,UNIQUE(symbol,source_time,rial))")
        db.execute("CREATE INDEX quotes_time ON quotes(symbol,source_time DESC,id DESC)")
        old = [(7,"usd",2649000,100,110), (8,"usd",2650000,200,210), (9,"euro",2957500,200,210)]
        db.executemany("INSERT INTO quotes VALUES(?,?,?,?,?)", old)
        upgrade = SOURCE.split("onUpgrade(", 1)[1].split("private ContentValues", 1)[0]
        before, after = upgrade.split("createSources(db);")
        run_statements(db, before)
        create_sources(db)
        run_statements(db, after)
        self.assertEqual([row + ("pricetoday",) for row in old], db.execute("SELECT * FROM quotes ORDER BY id").fetchall())
        self.assertEqual(3, db.execute("SELECT COUNT(*) FROM source_history").fetchone()[0])
        self.assertEqual([("euro",2957500),("usd",2650000)], db.execute("SELECT symbol,rial FROM source_latest ORDER BY symbol").fetchall())

    def test_duplicate_identity_includes_provider(self):
        db = sqlite3.connect(":memory:")
        db.execute("CREATE TABLE quotes " + QUOTES)
        sql = "INSERT OR IGNORE INTO quotes(symbol,rial,source_time,received_time,source) VALUES(?,?,?,?,?)"
        for row in [("usd",100,200,300,"pricetoday"),("usd",100,200,400,"pricetoday"),("usd",100,200,300,"iranmarket")]:
            db.execute(sql,row)
        self.assertEqual(2,db.execute("SELECT COUNT(*) FROM quotes").fetchone()[0])

    def test_error_status_does_not_delete_source_cache(self):
        db = sqlite3.connect(":memory:")
        create_sources(db)
        db.execute("INSERT INTO source_latest VALUES('pricetoday','usd',100,200,300)")
        db.execute("INSERT INTO source_status VALUES('pricetoday','usd','error','timeout',400,6000)")
        self.assertEqual(100,db.execute("SELECT rial FROM source_latest").fetchone()[0])


if __name__ == "__main__":
    unittest.main()
