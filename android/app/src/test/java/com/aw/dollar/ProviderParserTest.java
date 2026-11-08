package com.aw.dollar;

import org.junit.Test;
import org.json.JSONObject;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import static org.junit.Assert.*;

public class ProviderParserTest {
    private long now() { return PriceApi.parseTime("2026-11-08T18:00:00Z"); }
    private String fixture(String name) throws Exception {
        try (InputStream stream = getClass().getResourceAsStream("/" + name)) {
            assertNotNull(stream);
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            byte[] buffer = new byte[4096]; int count;
            while ((count = stream.read(buffer)) != -1) bytes.write(buffer,0,count);
            return bytes.toString(StandardCharsets.UTF_8.name());
        }
    }
    @Test public void pricetodayIsRialNotToman() throws Exception {
        Price quote = PriceApi.parse(PriceSource.PRICETODAY,"usd",
                "{\"success\":true,\"price\":2649000,\"time\":\"2026-11-08T14:58:00Z\"}",now());
        assertEquals(2649000,quote.rial); assertEquals("pricetoday",quote.source);
    }
    @Test public void iranMarketNormalizesTomanAndUsesItemTimeNotPublicationTime() throws Exception {
        String json = fixture("iran-market.json");
        Price usd = PriceApi.parse(PriceSource.IRAN_MARKET,"usd",json,now());
        Price euro = PriceApi.parse(PriceSource.IRAN_MARKET,"euro",json,now());
        JSONObject data = new JSONObject(json);
        JSONObject usdRow = data.getJSONArray("data").getJSONObject(0);
        assertEquals(usdRow.getLong("price")*10,usd.rial);
        assertEquals(PriceApi.parseTime(usdRow.getString("timestamp")),usd.sourceTime);
        assertEquals("iranmarket",euro.source);
        assertTrue(euro.rial > 0);
    }
    @Test public void nerkhsanjNormalizesTomanAndJalaliTehranTime() throws Exception {
        Price usd = PriceApi.parse(PriceSource.NERKHSANJ,"usd",fixture("nerkhsanj.json"),now());
        Price euro = PriceApi.parse(PriceSource.NERKHSANJ,"euro",fixture("nerkhsanj.json"),now());
        assertEquals(2649000,usd.rial); assertEquals(2957500,euro.rial);
        assertEquals(PriceApi.parseTime("2026-11-08T14:58:00Z"),usd.sourceTime);
    }
    @Test(expected=IllegalArgumentException.class) public void refusesFutureTime() throws Exception {
        PriceApi.parse(PriceSource.PRICETODAY,"usd","{\"success\":true,\"price\":10,\"time\":\"2027-01-01T00:00:00Z\"}",now());
    }
    @Test(expected=IllegalArgumentException.class) public void refusesZeroPrice() throws Exception {
        PriceApi.parse(PriceSource.PRICETODAY,"usd","{\"success\":true,\"price\":0,\"time\":\"2026-11-08T14:58:00Z\"}",now());
    }
    @Test(expected=IllegalArgumentException.class) public void refusesUnknownUnits() throws Exception {
        String body=fixture("iran-market.json").replace("\"IRT\"","\"USD\"");
        PriceApi.parse(PriceSource.IRAN_MARKET,"usd",body,now());
    }
    @Test(expected=Exception.class) public void refusesMissingCurrencyInsteadOfUsingTether() throws Exception {
        PriceApi.parse(PriceSource.IRAN_MARKET,"usd","{\"data\":[{\"symbol\":\"USDT_IRR\"}]}",now());
    }
    @Test(expected=IllegalArgumentException.class) public void refusesOfficialMarket() throws Exception {
        PriceApi.parse(PriceSource.IRAN_MARKET,"usd",fixture("iran-market.json").replace("iran_free","official"),now());
    }
    @Test public void leapDateAndYearBoundary() {
        assertEquals(PriceApi.parseTime("2025-03-20T08:30:00Z"),SourceTime.tehranJalali("1403/12/30","12:00"));
        assertEquals(PriceApi.parseTime("2025-03-21T08:30:00Z"),SourceTime.tehranJalali("1404/01/01","12:00"));
    }
    @Test(expected=IllegalArgumentException.class) public void rejectsInvalidJalaliLeapDay() {
        SourceTime.tehranJalali("1404/12/30","12:00");
    }
}
