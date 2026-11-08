package com.aw.dollar;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.view.View;
import java.util.ArrayList;
import java.util.List;
import java.text.SimpleDateFormat;
import java.util.Locale;

/** Displays actual, time-spaced observations; never interpolates missing historical data. */
public final class TrendChart extends View {
    private final List<Price> points;
    private final Paint pen = new Paint(Paint.ANTI_ALIAS_FLAG);
    public TrendChart(Context context, List<Price> newestFirst) {
        super(context);
        points = new ArrayList<>(newestFirst);
        setContentDescription("نمودار آخرین " + points.size() + " نرخ ثبت‌شده؛ اعداد دقیق در تاریخچه آمده‌اند");
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);
    }
    @Override protected void onDraw(Canvas c) {
        super.onDraw(c);
        float d = getResources().getDisplayMetrics().density;
        float left = 12*d, right = getWidth()-12*d, top = 30*d, bottom = getHeight()-28*d;
        pen.setTextSize(11*d);
        pen.setColor(0xff78908a);
        pen.setTextAlign(Paint.Align.LEFT);
        if (points.size() < 2) {
            c.drawText("نمودار پس از دریافت دو نرخ نمایش داده می‌شود", left, (top+bottom)/2, pen);
            return;
        }
        long min = Long.MAX_VALUE, max = 0;
        for (Price p : points) { min = Math.min(min, p.rial); max = Math.max(max, p.rial); }
        c.drawText(Price.toman(max), left, 16*d, pen);
        pen.setTextAlign(Paint.Align.RIGHT);
        c.drawText(Price.toman(min), right, 16*d, pen);
        long start = points.get(points.size()-1).sourceTime;
        long end = points.get(0).sourceTime;
        double range = Math.max(1.0, (double) max-min);
        Path line = new Path();
        for (int i=points.size()-1; i>=0; i--) {
            Price p = points.get(i);
            float x = left + (float)((p.sourceTime-start)/(double)Math.max(1L, end-start))*(right-left);
            float y = max == min ? (bottom+top)/2 : bottom - (float)((p.rial-min)/range)*(bottom-top);
            if (i == points.size()-1) line.moveTo(x,y); else line.lineTo(x,y);
        }
        pen.setStyle(Paint.Style.STROKE);
        pen.setStrokeWidth(2.5f*d);
        pen.setColor(0xff2c786d);
        c.drawPath(line,pen);
        pen.setStyle(Paint.Style.FILL);
        pen.setColor(0xff78908a);
        SimpleDateFormat f = new SimpleDateFormat("MM/dd HH:mm", Locale.US);
        pen.setTextAlign(Paint.Align.LEFT);
        c.drawText(f.format(start), left, getHeight()-6*d, pen);
        pen.setTextAlign(Paint.Align.RIGHT);
        c.drawText(f.format(end), right, getHeight()-6*d, pen);
    }
}
