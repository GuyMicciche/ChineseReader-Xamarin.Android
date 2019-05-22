using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Support.V4.App;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Java.Util.Concurrent;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    [Service(Label = "Pinyiner Clipboard Monitor", Exported = false)]
    public class PinyinerClipboardService : Service, ClipboardManager.IOnPrimaryClipChangedListener
    {
        public const int NOTIFICATION_ID = 123;
        private const string FILENAME = "clipboard-history.txt";

        private Context context;
        private File mHistoryFile;
        private IExecutorService mThreadPool = Executors.NewSingleThreadExecutor();
        private ClipboardManager mClipboardManager;
        private ISharedPreferences sharedPrefs;
        public Dict dict = null;

        private AnnTask.IAnnTask DisplayNotificationAnnInterface;

        public override void OnCreate()
        {
            base.OnCreate();

            context = this;

            mHistoryFile = new File(GetExternalFilesDir(null), FILENAME);
            mClipboardManager = (ClipboardManager)GetSystemService(ClipboardService);
            mClipboardManager.AddPrimaryClipChangedListener(this);

            sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(context);

            DisplayNotificationAnnInterface = new DisplayNotificationAnnTask(this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (mClipboardManager != null)
            {
                mClipboardManager.RemovePrimaryClipChangedListener(this);
            }
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        private bool ExternalStorageWritable
        {
            get
            {
                string state = global::Android.OS.Environment.ExternalStorageState;
                if (global::Android.OS.Environment.MediaMounted.Equals(state))
                {
                    return true;
                }
                return false;
            }
        }

        public void OnPrimaryClipChanged()
        {
            string pastedText = MainActivity.GetBufText(context);

            if (!sharedPrefs.GetBoolean("pref_monitor", true) || MainActivity.isActive)
            {
                return;
            }

            int textLen = pastedText.Length;
            for (int i = 0; i < textLen; i++)
            {
                if (pastedText[i] >= '\u25CB' && pastedText[i] <= '\u9FA5')
                {
                    if (dict == null)
                    {
                        dict = new Dict();
                    }
                    if (Dict.entries == null)
                    {
                        Dict.LoadDict(context);
                    }
                    List<List<object>> lines = new List<List<object>>();
                    AnnTask annotateTask = new AnnTask(context, AnnTask.TASK_ANNOTATE, MainActivity.ANNOTATE_BUFFER, 0, 0, 0, 5, 0, lines, new List<object>(), pastedText, pastedText.Length, null, new LineView(context), DisplayNotificationAnnInterface, true, null);
                    annotateTask.Execute();

                    break;
                }
            }
        }

        public class DisplayNotificationAnnTask : AnnTask.IAnnTask
        {
            PinyinerClipboardService Service;

            public DisplayNotificationAnnTask(PinyinerClipboardService service)
            {
                Service = service;
            }

            public void OnCompleted(int task, int splitLineIndex, string pastedText, List<List<object>> tempLines, int curPos, long tempStartPos, long tempEndPos, bool isRemaining, List<Bookmark> foundBookmarks)
            {
                StringBuilder pinyin = new StringBuilder("");

                RemoteViews smallView = null;

                if (Build.VERSION.SdkInt < BuildVersionCodes.JellyBean)
                {
                    foreach (List<object> line in tempLines)
                    {
                        object lastWord = null;

                        foreach (object word in line)
                        {
                            if (lastWord != null && (lastWord is int? || lastWord.GetType() != word.GetType()))
                            {
                                pinyin.Append(" ");
                            }

                            if (word is string)
                            {
                                pinyin.Append((string)word);
                            }
                            else
                            {
                                pinyin.Append(Dict.PinyinToTones(Dict.GetPinyin((int)word)));
                            }

                            lastWord = word;
                        }
                    }

                    smallView = new RemoteViews(Service.PackageName, Resource.Layout.Notification_Small);
                    smallView.SetTextViewText(Resource.Id.notifsmall_text, pinyin);
                }
                else
                {
                    smallView = new RemoteViews(Service.PackageName, Resource.Layout.Notification_Big);
                }

                Service.dict = null;

                NotificationCompat.Builder mBuilder = (new NotificationCompat.Builder(Service.ApplicationContext))
                    .SetSmallIcon(Resource.Drawable.notification)
                    .SetContentTitle("ChineseReader")
                    .SetContentText(pinyin)
                    .SetContent(smallView)
                    .SetPriority(NotificationCompat.PriorityMax)
                    .SetVibrate(new long[0]);


                Intent resultIntent = new Intent(Service.Application, typeof(MainActivity));
                resultIntent.SetAction(Intent.ActionSend);
                resultIntent.SetType("text/plain");
                resultIntent.PutExtra(Intent.ExtraText, pastedText);

                // The stack builder object will contain an artificial back stack for the
                // started Activity.
                // This ensures that navigating backward from the Activity leads out of
                // your application to the Home screen.
                global::Android.Support.V4.App.TaskStackBuilder stackBuilder = global::Android.Support.V4.App.TaskStackBuilder.Create(Service.ApplicationContext);

                // Adds the back stack for the Intent (but not the Intent itself)
                stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));

                // Adds the Intent that starts the Activity to the top of the stack
                stackBuilder.AddNextIntent(resultIntent);
                PendingIntent resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);
                mBuilder.SetContentIntent(resultPendingIntent);
                NotificationManager mNotificationManager = (NotificationManager)Service.GetSystemService(Context.NotificationService);
                mNotificationManager.CancelAll();

                // mId allows you to update the notification_big later on.
                Notification notif = mBuilder.Build();

                if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
                {
                    LineView lv = new LineView(Service.context);
                    lv.line = tempLines[0];
                    lv.lines = tempLines;
                    lv.hlIndex = new Point(-1, -1);
                    lv.top = new List<string>();
                    lv.bottom = new List<string>();
                    lv.tones = new List<int>();
                    lv.charTypeface = Typeface.Default;

                    int wordHeight = (int)(lv.WordHeight);
                    int lineCount = (int)System.Math.Min(tempLines.Count, System.Math.Floor(256 * lv.scale / wordHeight) + 1);
                    int width = (int)System.Math.Round((double)AnnTask.screenWidth);
                    lv.Measure(width, wordHeight);
                    lv.Layout(0, 0, width, wordHeight);
                    Bitmap bitmap = Bitmap.CreateBitmap(width, (int)System.Math.Min(System.Math.Max(64 * lv.scale, wordHeight * lineCount), 256 * lv.scale), Bitmap.Config.Argb8888);
                    Canvas canvas = new Canvas(bitmap);
                    Paint whiteBg = new Paint();
                    whiteBg.Color = Color.ParseColor("FFFFFFFF");
                    canvas.DrawRect(0, 0, canvas.Width, canvas.Height, whiteBg);

                    for (int i = 0; i < lineCount; i++)
                    {
                        lv.lines = tempLines;
                        lv.line = tempLines[i];
                        lv.bottom.Clear();
                        lv.top.Clear();
                        lv.tones.Clear();
                        int count = lv.lines[i].Count;

                        if (count == 0 || lv.line[count - 1] is string && ((string)lv.line[count - 1]).Length == 0 || tempEndPos >= pastedText.Length && i == tempLines.Count - 1)
                        {
                            lv.lastLine = true;
                        }
                        else
                        {
                            lv.lastLine = false;
                        }

                        for (int j = 0; j < count; j++)
                        {
                            object word = lv.lines[i][j];

                            if (word is string)
                            {
                                lv.bottom.Add((string)word);
                                lv.top.Add("");
                                lv.tones.Add(0);
                            }
                            else
                            {
                                int entry = (int)word;
                                string key = Dict.GetCh(entry);
                                lv.bottom.Add(key);
                                if (Service.sharedPrefs.GetString("pref_pinyinType", "marks").Equals("none"))
                                {
                                    lv.top.Add("");
                                }
                                else
                                {
                                    lv.top.Add(Dict.PinyinToTones(Dict.GetPinyin(entry)));
                                }

                                if (Service.sharedPrefs.GetString("pref_toneColors", "none").Equals("none"))
                                {
                                    lv.tones.Add(0);
                                }
                                else
                                {
                                    int tones = int.Parse(Regex.Replace(Dict.GetPinyin(entry), @"[\\D]", ""));
                                    int reverseTones = 0;
                                    while (tones != 0)
                                    {
                                        reverseTones = reverseTones * 10 + tones % 10;
                                        tones = tones / 10;
                                    }
                                    lv.tones.Add(reverseTones);
                                }
                            }
                        }

                        lv.Draw(canvas);
                        canvas.Translate(0, wordHeight);
                    }

                    RemoteViews bigView = new RemoteViews(Service.PackageName, Resource.Layout.Notification_Big);
                    bigView.SetImageViewBitmap(Resource.Id.notif_img, bitmap);
                    smallView.SetImageViewBitmap(Resource.Id.notif_img, Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, (int)(64 * lv.scale)));
                    notif.BigContentView = bigView;
                }

                mNotificationManager.Notify(NOTIFICATION_ID, notif);
            }
        }
    }
}