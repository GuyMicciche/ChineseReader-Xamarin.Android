using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;

namespace ChineseReader.Android
{
    public class AboutDialog : Dialog
    {
        private Activity mActivity;

        public AboutDialog(Context context) : base(context)
        {
            mActivity = (Activity)context;
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Window.RequestFeature(WindowFeatures.NoTitle);
            SetContentView(Resource.Layout.About);
        }
    }
}
