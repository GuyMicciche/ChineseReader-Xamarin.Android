using Android.Content;
using Android.Preferences;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace ChineseReader.Android
{
    public class SeekBarPreference : DialogPreference, SeekBar.IOnSeekBarChangeListener
    {
        private const string androidns = "http://schemas.android.com/apk/res/android";

        private SeekBar mSeekBar;
        private TextView mSplashText, mValueText;
        private Context mContext;

        private string mDialogMessage, mSuffix;
        private int mDefault, mMax, mValue = 0;

        public SeekBarPreference(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            mContext = context;

            mDialogMessage = attrs.GetAttributeValue(androidns, "dialogMessage");
            mSuffix = attrs.GetAttributeValue(androidns, "text");
            mDefault = attrs.GetAttributeIntValue(androidns, "defaultValue", 0);
            mMax = attrs.GetAttributeIntValue(androidns, "max", 100);
            mValue = GetPersistedInt(mDefault);
        }
        protected override View OnCreateDialogView()
        {
            LinearLayout.LayoutParams parameters;
            LinearLayout layout = new LinearLayout(mContext);
            layout.Orientation = Orientation.Vertical;
            layout.SetPadding(6, 6, 6, 6);

            mSplashText = new TextView(mContext);
            if (mDialogMessage != null)
            {
                mSplashText.Text = mDialogMessage;
            }
            layout.AddView(mSplashText);

            mValueText = new TextView(mContext);
            mValueText.Gravity = GravityFlags.CenterHorizontal;
            mValueText.TextSize = 32;
            parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            layout.AddView(mValueText, parameters);

            mSeekBar = new SeekBar(mContext);
            mSeekBar.SetOnSeekBarChangeListener(this);
            layout.AddView(mSeekBar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent));

            if (ShouldPersist())
            {
                mValue = GetPersistedInt(mDefault);
            }

            mSeekBar.Max = mMax;
            mSeekBar.Progress = mValue;
            return layout;
        }
        protected override void OnBindDialogView(View v)
        {
            base.OnBindDialogView(v);
            mSeekBar.Max = mMax;
            mSeekBar.Progress = mValue;
        }
        protected override void OnSetInitialValue(bool restore, Object defaultValue)
        {
            base.OnSetInitialValue(restore, defaultValue);
            if (restore)
            {
                mValue = ShouldPersist() ? GetPersistedInt(mDefault) : 0;
            }
            else
            {
                mValue = (int)defaultValue;
            }
        }

        public void OnProgressChanged(SeekBar seek, int value, bool fromTouch)
        {
            string t = value.ToString();
            mValueText.Text = mSuffix == null ? t : t + mSuffix;
        }
        public void OnStartTrackingTouch(SeekBar seek)
        {
        }
        public void OnStopTrackingTouch(SeekBar seek)
        {

        }

        public int Max
        {
            set
            {
                mMax = value;
            }
            get
            {
                return mMax;
            }
        }

        public int Progress
        {
            set
            {
                mValue = value;
                if (mSeekBar != null)
                {
                    mSeekBar.Progress = value;
                }
            }
            get
            {
                return mValue;
            }
        }

        protected override void OnDialogClosed(bool positiveResult)
        {
            base.OnDialogClosed(positiveResult);

            if (positiveResult)
            {
                int value = mSeekBar.Progress;
                if (CallChangeListener(value))
                {
                    if (mValue != value)
                    {
                        mValue = value;
                        PersistInt(value);
                        NotifyChanged();
                    }
                }
            }
        }
    }
}