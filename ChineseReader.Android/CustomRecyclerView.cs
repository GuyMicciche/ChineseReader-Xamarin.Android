using Android.Content;
using Android.Graphics;
using Android.Preferences;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using System;

namespace ChineseReader.Android
{
    public class CustomRecyclerView : RecyclerView, ScaleGestureDetector.IOnScaleGestureListener
    {
        public float progress = 0, length = 0, bannerHeight = 0;
        private Paint progressPaint;
        private ScaleGestureDetector mScaleDetector;
        private Context mContext;
        public AnnotationActivity mMainActivity;
        private float density;

        private ISharedPreferences sharedPrefs;
        private int initTextSize;

        public CustomRecyclerView(Context context) : base(context)
        {

        }

        public CustomRecyclerView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            mContext = context;
            density = context.Resources.DisplayMetrics.Density;
            progressPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.LinearText);
            progressPaint.StrokeWidth = 2 * density;
            progressPaint.SetARGB(255, 0, 183, 235);
            progressPaint.TextSize = 17 * density;
            progressPaint.FakeBoldText = true;

            mScaleDetector = new ScaleGestureDetector(context, this);
        }

        public void OnScaleEnd(ScaleGestureDetector detector)
        {

        }
        public bool OnScaleBegin(ScaleGestureDetector detector)
        {
            sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(mContext);
            initTextSize = sharedPrefs.GetInt("pref_textSizeInt", 16);
            return true;
        }
        public bool OnScale(ScaleGestureDetector detector)
        {
            int textSizeInt = (int)Math.Max(10, Math.Round(initTextSize * detector.ScaleFactor));
            sharedPrefs.Edit().PutInt("pref_textSizeInt", textSizeInt).Commit();
            mMainActivity.parentSettingsChanged = true;
            mMainActivity.Redraw();
            mMainActivity.wPopup.dismiss();
            return false;
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            mScaleDetector.OnTouchEvent(e);

            return base.OnTouchEvent(e);
        }

        protected override void DispatchDraw(Canvas canvas)
        {
            if (progress != -1)
            {
                Rect visibilityRect = new Rect();
                GetLocalVisibleRect(visibilityRect);
                float x = canvas.Width - 2 * mMainActivity.testView.scale;
                int maxHeight = (int)(visibilityRect.Height() - bannerHeight - 2 * density);
                length = maxHeight * progress;

                progressPaint.Alpha = 255;
                canvas.DrawLine(x, 1 * mMainActivity.testView.scale, x, length, progressPaint);

                progressPaint.Alpha = 80;
                string percent = (int)(progress * 100) + "%";
                float percentWidth = progressPaint.MeasureText(percent);
                canvas.DrawText(percent, x - percentWidth - 2 * density, maxHeight, progressPaint);
            }

            base.DispatchDraw(canvas);
        }

        public override bool DispatchTouchEvent(MotionEvent e)
        {
            if (e.Action == MotionEventActions.Down && FindChildViewUnder(e.XPrecision, e.YPrecision) == null)
            {
                mMainActivity.wPopup.dismiss();
            }
            return base.DispatchTouchEvent(e);
        }
    }
}