using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;

namespace ChineseReader.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    [IntentFilter(new[] { Intent.ActionBootCompleted})]
    public class PinyinerReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            ISharedPreferences sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
            if (sharedPrefs.GetBoolean("pref_monitor", true) && Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            {
                Intent monitorIntent = new Intent(context, typeof(PinyinerClipboardService));
                context.StartService(monitorIntent);
            }
        }
    }
}