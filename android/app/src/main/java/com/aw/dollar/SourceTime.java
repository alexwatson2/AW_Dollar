package com.aw.dollar;

import java.util.Calendar;
import java.util.GregorianCalendar;
import java.util.TimeZone;

/** Convert modern Solar Hijri dates supplied by Nerkhsanj, without assuming Gregorian years. */
public final class SourceTime {
    private SourceTime() { }
    static long jalaliDay(int year, int month, int day) {
        int y = year + 1595;
        return -355668L + 365L*y + (y/33)*8L + ((y%33)+3)/4 + day
                + (month < 7 ? (month-1)*31 : (month-7)*30+186);
    }
    public static long tehranJalali(String date, String time) {
        if (!date.matches("\\d{4}/\\d{2}/\\d{2}") || !time.matches("\\d{2}:\\d{2}(:\\d{2})?"))
            throw new IllegalArgumentException("Invalid Jalali timestamp");
        String[] parts = date.split("/");
        int y = Integer.parseInt(parts[0]), m = Integer.parseInt(parts[1]), d = Integer.parseInt(parts[2]);
        // This arithmetic calendar is deliberately bounded to modern, supported market dates.
        if (y < 1300 || y > 1499 || m < 1 || m > 12 || d < 1 || d > (m <= 6 ? 31 : 30)
                || (m == 12 && d == 30 && jalaliDay(y+1,1,1)-jalaliDay(y,1,1) != 366))
            throw new IllegalArgumentException("Invalid Jalali date");
        long days = jalaliDay(y,m,d);
        int gy = 400*(int)(days/146097);
        days %= 146097;
        if (days > 36524) { gy += 100*(int)(--days/36524); days %= 36524; if (days >= 365) days++; }
        gy += 4*(int)(days/1461); days %= 1461;
        if (days > 365) { gy += (int)((days-1)/365); days = (days-1)%365; }
        int gd = (int)days+1, gm = 1;
        int[] months = {0,31, gy%4 == 0 && (gy%100 != 0 || gy%400 == 0) ? 29 : 28,31,30,31,30,31,31,30,31,30,31};
        while (gd > months[gm]) gd -= months[gm++];
        String[] clock = time.split(":");
        Calendar calendar = new GregorianCalendar(TimeZone.getTimeZone("Asia/Tehran"));
        calendar.clear(); calendar.setLenient(false);
        calendar.set(gy,gm-1,gd,Integer.parseInt(clock[0]),Integer.parseInt(clock[1]),
                clock.length == 3 ? Integer.parseInt(clock[2]) : 0);
        return calendar.getTimeInMillis();
    }
}
