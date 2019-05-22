using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Java.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Toolbar = Android.Support.V7.Widget.Toolbar;
using AlertDialog = Android.App.AlertDialog;
using Android.Text;
using System.Linq;

namespace ChineseReader.Android
{
    public class AnnotationActivity : AppCompatActivity
    {
        public const int ANNOTATE_BUFFER = 0, ANNOTATE_SHARE = 1, ANNOTATE_FILE = 2, ANNOTATE_STARRED = 3;

        public List<List<object>> lines = new List<List<object>>();
        public AnnListAdapter linesAdapter;
        public LinearLayoutManager linesLayoutManager;
        public WordPopup wPopup;
        public Point hlIndex = new Point();
        public AnnTask annTask;
        public Toolbar mToolbar;
        public static ISharedPreferences sharedPrefs;
        public string curFilePath = "", curSaveName = "";
        public int annoMode;
        public bool isFirstAnnotation = true, isFirstFileAnnotation = true;
        public LineView testView;
        public CustomRecyclerView linesRecyclerView;
        public Activity app;
        public long textLen, startPos, endPos;
        public int curPos;
        public string pastedText = "";
        public RandomAccessFile openedFile;
        public static bool isActive = false;
        private RecyclerView.ItemAnimator defaultItemAnimator;
        public bool settingsChanged = false, parentSettingsChanged = false;
        public List<Bookmark> mBookmarks, mFoundBookmarks;
        public const int REQUEST_STORAGE_FOR_FILEBROWSER = 1, REQUEST_STORAGE_FOR_SAVE = 2, REQUEST_STORAGE_FOR_STARRED_EXPORT = 3, REQUEST_STORAGE_FOR_STARRED_EXPORT_PINYIN = 4, REQUEST_STORAGE_FOR_STARRED_EXPORT_PLECO = 5, REQUEST_STORAGE_FOR_BOOK = 6, FILEBROWSER_ACTIVITY_CODE = 1, SETTINGS_ACTIVITY_CODE = 2, STARRED_ACTIVITY_CODE = 3, RESULT_SETTINGS_CHANGED = 10;

        public AnnTask.IAnnTask UpdateLinesAnnInterface;
        public AnnTask.IAnnTask DumpPinyinAnnInterface;
        public AnnTask.IAnnTask DumpBothAnnInterface;

        public AnnotationActivity()
        {
            UpdateLinesAnnInterface = new UpdateLinesAnnTask(this);
            DumpPinyinAnnInterface = new DumpPinyinAnnTask(this);
            DumpBothAnnInterface = new DumpBothAnnTask(this);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            System.Console.WriteLine("AnnotationActivity >> OnCreate");

            app = this;

            try
            {
                if (FindViewById(Resource.Id.lines) != null)
                {
                    AnnTask.UpdateVars(this);
                    CheckIsShared();

                    return;
                }

                curFilePath = "";
                isFirstAnnotation = true;
                isFirstFileAnnotation = true;

                PreferenceManager.SetDefaultValues(this, Resource.Xml.preferences, false);
                sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(this);

                SetContentView(Resource.Layout.MainActivity);
                mToolbar = (Toolbar)FindViewById(Resource.Id.toolbar); // Attaching the layout to the toolbar object
                SetSupportActionBar(mToolbar);
                SupportActionBar.SetDisplayShowHomeEnabled(true);
                SupportActionBar.SetIcon(Resource.Drawable.logo_toolbar);

                wPopup = new WordPopup(this);
                testView = new LineView(this);
                testView.UpdateVars();

                lines = new List<List<object>>();
                linesRecyclerView = (CustomRecyclerView)FindViewById(Resource.Id.lines);
                linesRecyclerView.mMainActivity = this;
                linesLayoutManager = new LinearLayoutManager(this);
                linesRecyclerView.SetLayoutManager(linesLayoutManager);
                linesAdapter = new AnnListAdapter(this, this.lines, linesRecyclerView, wPopup);
                linesRecyclerView.SetAdapter(linesAdapter);
                defaultItemAnimator = linesRecyclerView.GetItemAnimator();

                linesRecyclerView.AddOnScrollListener(new RecyclerViewOnScrollListener(this));

                mBookmarks = new List<Bookmark>();
                mFoundBookmarks = new List<Bookmark>();
            }
            catch(Exception e)
            {
                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
            }
        }

        public void Annotate(long position)
        {
            if (annTask != null)
            {
                annTask.Cancel(true);
            }

            lines.Clear();
            linesAdapter.NotifyDataSetChanged();
            linesAdapter.ShowHeader = false;
            linesAdapter.ShowFooter = true;

            wPopup.dismiss();

            startPos = 0;
            curPos = 0;
            endPos = 0;
            isFirstFileAnnotation = true;

            if (annoMode == ANNOTATE_FILE)
            {
                startPos = Math.Max(0, GetPreferences(FileCreationMode.Private).GetInt(curFilePath, 0));
                if (startPos >= textLen)
                {
                    startPos = 0;
                }
                endPos = startPos;

                mBookmarks = Bookmark.ReadFromFile(curFilePath + ".bookmarks");
                mFoundBookmarks.Clear();
            }
            else if (annoMode == ANNOTATE_STARRED)
            {
                curFilePath = "";
                curSaveName = "ChineseReader_Starred";
            }
            else
            {
                curFilePath = "";
                curSaveName = "";
            }

            if (position >= 0)
            {
                startPos = endPos = position;
            }

            if (startPos == 0)
            {
                linesAdapter.ShowHeader = false;
            }

            //System.Console.WriteLine("THIS IS RIGHT BEFORE EXECUTE!!!!!!!!!!");
            annTask = new AnnTask(this, AnnTask.TASK_ANNOTATE, annoMode, curPos, startPos, endPos, 5, 0, lines, new List<object>(), pastedText, textLen, openedFile, testView, UpdateLinesAnnInterface, true, mBookmarks);
            annTask.ExecuteWrapper();
        }

        public void Annotate(string filePath)
        {
            File fileFd = new File(filePath);
            try
            {
                openedFile = new RandomAccessFile(fileFd, "r");
                textLen = Convert.ToInt32(fileFd.Length());
                if (annoMode != ANNOTATE_FILE)
                {
                    sharedPrefs.Edit().PutString("lastText", pastedText).Commit();
                }
                annoMode = ANNOTATE_FILE;
                curFilePath = filePath;
                curSaveName = Regex.Replace(fileFd.Name, @"(\.[^.]*)$", "_ChineseReader");

                Title = fileFd.Name;

                Annotate(-1);
            }
            catch (Exception e)
            {
                Toast.MakeText(Application, e.Message, ToastLength.Long).Show();
            }
        }

        public static string GetBufText(Context context)
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
            {
                global::Android.Text.ClipboardManager clipboard = (global::Android.Text.ClipboardManager)context.GetSystemService(Context.ClipboardService);
                if (!clipboard.HasText)
                {
                    return "";
                }
                else
                {
                    return clipboard.Text.ToString();
                }
            }
            else
            {
                global::Android.Content.ClipboardManager clipboard = (global::Android.Content.ClipboardManager)context.GetSystemService(Context.ClipboardService);
                if (clipboard.HasPrimaryClip && (clipboard.PrimaryClipDescription.HasMimeType(ClipDescription.MimetypeTextPlain) || clipboard.PrimaryClipDescription.HasMimeType(ClipDescription.MimetypeTextHtml)))
                {
                    return clipboard.PrimaryClip.GetItemAt(0).CoerceToText(context).ToString();
                }
                else
                {
                    return "";
                }
            }
        }

        public class UpdateLinesAnnTask : AnnTask.IAnnTask
        {
            AnnotationActivity Activity;

            public UpdateLinesAnnTask(AnnotationActivity activity)
            {
                Activity = activity;
            }


            public void OnCompleted(int task, int splitLineIndex, string pastedText, List<List<object>> tempLines, int curPos, long tempStartPos, long tempEndPos, bool isRemaining, List<Bookmark> foundBookmarks)
            {
                System.Console.WriteLine("UpdateLinesAnnTask => OnCompleted()");

                if (Activity.curSaveName.Equals("") && Activity.curPos == 0)
                {
                    //System.Console.WriteLine("UpdateLinesAnnTask => Step 1");

                    StringBuilder fName = new StringBuilder();
                    int p = 0, r = 0;
                    while (fName.Length < 16 && r < tempLines.Count && (p < tempLines[r].Count || r < tempLines.Count - 1))
                    {
                        var word = tempLines.ElementAt(r).ElementAt(p);
                        if (word is int)
                        {
                            System.Console.WriteLine("UpdateLinesAnnTask => IS INT");

                            if (fName.Length > 0)
                            {
                                fName.Append("-");
                            }
                            fName.Append(Regex.Replace(Dict.GetPinyin((int)word), @"[^a-zA-Z]+", ""));
                        }
                        else
                        {
                            System.Console.WriteLine("UpdateLinesAnnTask => IS STRING");

                            System.Console.WriteLine("word => " + (string)word);

                            string s = Regex.Replace((string)word, @"[^a-zA-Z0-9]+", "");
                            fName.Append(s.Substring(0, Math.Min(s.Length, 16)));
                        }
                        if (++p == tempLines[r].Count)
                        {
                            p = 0;
                            r++;
                        }
                    }

                    Activity.curSaveName = fName.ToString();
                }

                Activity.curPos = curPos;

                Activity.linesRecyclerView.SetItemAnimator(Activity.defaultItemAnimator);
                
                switch (task)
                {
                    case AnnTask.TASK_ANNOTATE:
                    case AnnTask.TASK_SPLIT:

                        if (isRemaining)
                        {
                            Activity.linesAdapter.ShowFooter = true;
                        }
                        else
                        {
                            Activity.linesAdapter.ShowFooter = false;
                        }

                        int firstVisiblePosition = Activity.linesLayoutManager.FindFirstVisibleItemPosition();
                        View firstVisible = Activity.linesLayoutManager.FindViewByPosition(firstVisiblePosition);
                        int top = firstVisible != null ? firstVisible.Top : 0;

                        if (task == AnnTask.TASK_SPLIT)
                        {
                            Activity.linesRecyclerView.SetItemAnimator(null);
                            int toRemove = Activity.lines.Count - splitLineIndex;
                            while (toRemove-- > 0)
                            {
                                Activity.lines.RemoveAt(splitLineIndex);
                                Activity.linesAdapter.NotifyItemRemoved(splitLineIndex + 1);
                            }

                            if (Activity.annoMode == ANNOTATE_FILE && Activity.mBookmarks.Count > 0)
                            {
                                int bookmarksRemoveFrom = Bookmark.SearchClosest(Activity.endPos, Activity.mFoundBookmarks);
                                while (Activity.mFoundBookmarks.Count > bookmarksRemoveFrom)
                                {
                                    Activity.mFoundBookmarks.RemoveAt(bookmarksRemoveFrom);
                                }
                            }
                        }

                        int rmCount = -1;
                        if (Activity.annoMode == ANNOTATE_FILE && !Activity.isFirstFileAnnotation && firstVisiblePosition > AnnTask.visibleLines)
                        {
                            rmCount = firstVisiblePosition - AnnTask.visibleLines;
                            tempStartPos = Activity.GetPosition(Activity.lines, rmCount + 1, 0, true);
                            for (int i = 0; i < rmCount; i++)
                            {
                                Activity.lines.RemoveAt(0);
                                Activity.linesAdapter.NotifyItemRemoved(1);
                            }
                            int bookmarksRemoveUntil = Bookmark.SearchClosest(tempStartPos, Activity.mFoundBookmarks);
                            for (int i = 0; i < bookmarksRemoveUntil; i++)
                            {
                                Activity.mFoundBookmarks.RemoveAt(0);
                            }
                            for (int i = 0; i < Activity.mFoundBookmarks.Count; i++)
                            {
                                Activity.mFoundBookmarks[i].mLine -= rmCount;
                            }
                        }

                        for (int i = 0; i < foundBookmarks.Count; i++)
                        {
                            foundBookmarks[i].mLine += Activity.lines.Count;
                        }
                        Activity.mFoundBookmarks.AddRange(foundBookmarks);

                        Activity.lines.AddRange(tempLines);
                        Activity.linesAdapter.NotifyItemRangeInserted(Activity.lines.Count - tempLines.Count + 1, tempLines.Count);

                        if (tempStartPos > 0)
                        {
                            Activity.linesAdapter.ShowHeader = true;
                            if (Activity.isFirstFileAnnotation)
                            {
                                Activity.linesLayoutManager.ScrollToPositionWithOffset(1, 0);
                            }
                        }
                        else
                        {
                            Activity.linesAdapter.ShowHeader = false;
                        }

                        if (rmCount > -1)
                        {
                            Activity.linesLayoutManager.ScrollToPositionWithOffset(AnnTask.visibleLines, top);
                        }

                        tempLines.Clear();

                        break;

                    case AnnTask.TASK_ANNOTATE_BACK:
                        int remainCount = Activity.linesLayoutManager.FindFirstVisibleItemPosition() + AnnTask.visibleLines * 2;
                        if (Activity.lines.Count > remainCount)
                        {
                            rmCount = Activity.lines.Count - remainCount;
                            for (int i = 0; i < rmCount; i++)
                            {
                                List<object> rmLine = Activity.lines[remainCount];
                                Activity.lines.RemoveAt(remainCount);
                                Activity.linesAdapter.NotifyItemRemoved(remainCount + 1);
                                tempEndPos -= LineView.GetLineSize(rmLine, Activity.annoMode == ANNOTATE_FILE);
                            }

                            if (Activity.annoMode == ANNOTATE_FILE && Activity.mFoundBookmarks.Count > 0)
                            {
                                int bookmarksRemoveFrom = Bookmark.SearchClosest(tempEndPos, Activity.mFoundBookmarks);
                                while (Activity.mFoundBookmarks.Count > bookmarksRemoveFrom)
                                {
                                    Activity.mFoundBookmarks.RemoveAt(bookmarksRemoveFrom);
                                }
                            }
                        }

                        firstVisiblePosition = Activity.linesLayoutManager.FindFirstVisibleItemPosition();
                        int newFirstVisiblePosition = firstVisiblePosition + tempLines.Count;
                        top = Activity.linesLayoutManager.FindViewByPosition(firstVisiblePosition).Top + Activity.linesLayoutManager.FindViewByPosition(firstVisiblePosition).Height - Activity.linesLayoutManager.FindViewByPosition(firstVisiblePosition + 1).Height;
                        if (tempStartPos == 0)
                        {
                            Activity.linesAdapter.ShowHeader = false;
                        }

                        for (int i = 0; i < Activity.mFoundBookmarks.Count; i++)
                        {
                            Activity.mFoundBookmarks[i].mLine += tempLines.Count;
                        }
                        Activity.mFoundBookmarks.InsertRange(0, foundBookmarks);

                        Activity.lines.InsertRange(0, tempLines);
                        Activity.linesAdapter.NotifyItemRangeInserted(1, tempLines.Count);
                        Activity.linesLayoutManager.ScrollToPositionWithOffset(newFirstVisiblePosition, top);

                        tempLines.Clear();

                        break;
                }

                Activity.isFirstAnnotation = false;
                Activity.isFirstFileAnnotation = false;

                Activity.startPos = tempStartPos;
                Activity.endPos = tempEndPos;

                if (Activity.settingsChanged)
                {
                    Activity.Redraw();
                }

                //update header and footer
                Activity.linesAdapter.NotifyItemChanged(0);
                Activity.linesAdapter.NotifyItemChanged(Activity.linesAdapter.ItemCount - 1);
            }
        }

        public void Redraw()
        {
            if (annTask != null && annTask.status == AsyncTask.Status.Running)
            {
                return;
            }

            settingsChanged = false;
            AnnTask.UpdateVars(this);
            int currentTop = linesRecyclerView.GetChildAt(0).Top;

            annTask = new AnnTask(this, AnnTask.TASK_ANNOTATE, annoMode, curPos, startPos, endPos, 5, 0, lines, new List<object>(), pastedText, textLen, openedFile, testView, UpdateLinesAnnInterface, true, mBookmarks);
            annTask.RedrawLines(linesRecyclerView);
            startPos = annTask.startPos;
            endPos = annTask.endPos;

            linesAdapter.NotifyDataSetChanged();

            if (startPos > 0)
            {
                linesAdapter.ShowHeader = true;
            }

            RecyclerView ll = linesRecyclerView;
            ll.ClearFocus();
            ll.Post(() =>
            {
                ll.RequestFocusFromTouch();
                ((LinearLayoutManager)ll.GetLayoutManager()).ScrollToPositionWithOffset(1, currentTop);
                ll.RequestFocus();
            });
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            // Handle presses on the action bar items
            try
            {
                switch (item.ItemId)
                {
                    case Resource.Id.action_settings:
                        Intent i = new Intent(this, typeof(SettingsActivity));
                        StartActivityForResult(i, SETTINGS_ACTIVITY_CODE);
                        return true;

                    case Resource.Id.action_goto:
                        AlertDialog.Builder builder = new AlertDialog.Builder(this);
                        builder.SetTitle("Go To ... %");
                        EditText inputGoTo = new EditText(this);
                        inputGoTo.InputType = InputTypes.ClassNumber;
                        IInputFilter[] fa = new IInputFilter[1];
                        fa[0] = new InputFilterLengthFilter(2);
                        inputGoTo.SetFilters(fa);
                        inputGoTo.Text = Convert.ToString((int)(Math.Min(linesRecyclerView.progress * 100, 99)));
                        inputGoTo.Gravity = GravityFlags.Center;
                        inputGoTo.SetSelection(inputGoTo.Text.Length);
                        builder.SetView(inputGoTo);
                        builder.SetPositiveButton("Go", (sender, e) =>
                        {
                            int newPercent = 0;
                            
                            try
                            {
                                newPercent = Math.Max(0, Math.Min(int.Parse(inputGoTo.Text.ToString()), 100));
                                int newPos = (int)Math.Round((double)textLen * newPercent / 100);
                                Annotate(newPos);
                                ((AlertDialog)sender).Dismiss();
                            }
                            catch (System.FormatException)
                            {
                                Toast.MakeText(this, "Invalid percent number", ToastLength.Long).Show();
                            }
                        });
                        builder.SetNegativeButton("Cancel", (sender, e) =>
                        {
                            ((AlertDialog)sender).Cancel();
                        });

                        AlertDialog dialog = builder.Create();
                        dialog.Window.SetSoftInputMode(SoftInput.StateVisible);

                        dialog.Show();
                        break;
                }
            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
            }

            return false;
        }

        private static string dumpFilePath;
        private static System.IO.StreamWriter dumpFileWriter;
        private static Button dumpStartButton;
        private static ProgressBar dumpProgress;
        private static TextView dumpProgressText;
        private static bool dumpCancelled;

        public class DumpPinyinAnnTask : AnnTask.IAnnTask
        {
            AnnotationActivity Activity;

            public DumpPinyinAnnTask(AnnotationActivity activity)
            {
                Activity = activity;
            }

            public void OnCompleted(int task, int splitLineIndex, string pastedText, List<List<object>> tempLines, int curPos, long tempStartPos, long tempEndPos, bool isRemaining, List<Bookmark> foundBookmarks)
            {
                try
                {
                    foreach (List<object> line in tempLines)
                    {
                        object lastWord = null;

                        foreach (object word in line)
                        {
                            if (lastWord != null && (lastWord is int || lastWord.GetType() != word.GetType()))
                            {
                                dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(" "));
                            }

                            if (word is string)
                            {
                                if (((string)word).Length == 0)
                                {
                                    dumpFileWriter.BaseStream.WriteByte(Convert.ToByte("\n\r"));
                                }
                                else
                                {
                                    dumpFileWriter.BaseStream.WriteByte(Convert.ToByte((string)word));
                                }
                            }
                            else
                            {
                                dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(Dict.PinyinToTones(Dict.GetPinyin((int)word))));
                            }

                            lastWord = word;
                        }
                    }

                    if (isRemaining && !dumpCancelled)
                    {
                        int progress = (int)Math.Round((double)tempEndPos * 100 / Activity.textLen);
                        dumpProgress.Progress = progress;
                        dumpProgressText.Text = Convert.ToString(progress) + "%";
                        Activity.DumpPinyin((int)tempStartPos, tempEndPos);
                    }
                    else
                    {
                        dumpFileWriter.Flush();
                        dumpFileWriter.Close();

                        dumpStartButton.Enabled = true;
                        dumpProgress.Visibility = ViewStates.Gone;
                        dumpProgressText.Visibility = ViewStates.Gone;
                        Toast.MakeText(Activity.Application, "Saved to " + dumpFilePath, ToastLength.Long).Show();
                    }

                }
                catch (Exception e)
                {
                    Toast.MakeText(Activity.Application, e.Message, ToastLength.Long).Show();
                }
            }
        }

        public void DumpPinyin(long startPos, long endPos)
        {
            AnnTask annTask = new AnnTask(ApplicationContext, AnnTask.TASK_ANNOTATE, annoMode, curPos, startPos, endPos, 5, 0, new List<List<object>>(), new List<object>(), pastedText, textLen, openedFile, null, DumpPinyinAnnInterface, false, null);
            annTask.ExecuteWrapper();
        }
        
        private class DumpBothAnnTask : AnnTask.IAnnTask
        {
            AnnotationActivity Activity;

            public DumpBothAnnTask(AnnotationActivity activity)
            {
                Activity = activity;
            }

            public void OnCompleted(int task, int splitLineIndex, string pastedText, List<List<object>> tempLines, int curPos, long tempStartPos, long tempEndPos, bool isRemaining, List<Bookmark> foundBookmarks)
            {
                try
                {
                    StringBuilder textLine = new StringBuilder();

                    foreach (List<object> line in tempLines)
                    {
                        object lastWord = null;

                        textLine.Length = 0;
                        foreach (object word in line)
                        {
                            if (lastWord != null && word is int)
                            {
                                dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(@"\t"));
                                textLine.Append(@"\t");
                            }

                            if (word is string)
                            {
                                if (((string)word).Length != 0)
                                {
                                    textLine.Append(Regex.Replace((string)word, @"\t", ""));
                                }
                            }
                            else
                            {
                                dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(Dict.PinyinToTones(Dict.GetPinyin((int)word))));
                                textLine.Append(Dict.GetCh((int)word));
                            }

                            if (lastWord != null || !(word is string))
                            {
                                lastWord = word;
                            }
                        }

                        dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(@"\n\r"));
                        dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(textLine.ToString()));
                        dumpFileWriter.BaseStream.WriteByte(Convert.ToByte(@"\n\r"));
                    }

                    if (isRemaining && !dumpCancelled)
                    {
                        int progress = (int)Math.Round((double)tempEndPos * 100 / Activity.textLen);
                        dumpProgress.Progress = progress;
                        dumpProgressText.Text = Convert.ToString(progress) + "%";
                        Activity.DumpBoth(tempStartPos, tempEndPos);
                    }
                    else
                    {
                        dumpFileWriter.Flush();
                        dumpFileWriter.Close();

                        dumpStartButton.Enabled = true;
                        dumpProgress.Visibility = ViewStates.Gone;
                        dumpProgressText.Visibility = ViewStates.Gone;
                        Toast.MakeText(Activity.Application, "Saved to " + dumpFilePath, ToastLength.Long).Show();
                    }

                }
                catch (Exception e)
                {
                    Toast.MakeText(Activity.Application, e.Message, ToastLength.Long).Show();
                }
            }
        }

        public void DumpBoth(long startPos, long endPos)
        {
            AnnTask annTask = new AnnTask(ApplicationContext, AnnTask.TASK_ANNOTATE, annoMode, curPos, startPos, endPos, 5, 0, new List<List<object>>(), new List<object>(), pastedText, textLen, openedFile, testView, DumpBothAnnInterface, true, null);
            annTask.ExecuteWrapper();
        }

        public void SaveToFile()
        {
            AlertDialog.Builder dialogBuilder = new AlertDialog.Builder(this);
            dialogBuilder.SetTitle("Save to file");

            View views = LayoutInflater.Inflate(Resource.Layout.AlertDialog_Save, null);
            EditText input = (EditText)views.FindViewById(Resource.Id.edit);

            int counter = 0;
            Java.Lang.StringBuilder tempName = new Java.Lang.StringBuilder(curSaveName);
            while (true)
            {
                File file = new File(global::Android.OS.Environment.ExternalStorageDirectory + "/ChineseReader/", tempName.ToString() + ".txt");
                if (file.Exists())
                {
                    counter++;
                    tempName.SetLength(0);
                    tempName.Append(curSaveName).Append('(').Append(Convert.ToChar(counter)).Append(')');
                }
                else
                {
                    input.SetText(tempName, TextView.BufferType.Spannable);
                    break;
                }
            }

            dialogBuilder.SetView(views);
            dialogBuilder.SetPositiveButton("Save", delegate { });
            dialogBuilder.SetNegativeButton("Cancel", delegate { });

            AlertDialog alert = dialogBuilder.Show();

            dumpStartButton = alert.GetButton((int)DialogButtonType.Positive);
            dumpStartButton.Click += (sender, e) =>
            {
                try
                {
                    View view = (View)sender;

                    File dir = new File(global::Android.OS.Environment.ExternalStorageDirectory + "/ChineseReader/");
                    if (!dir.Exists())
                    {
                        dir.Mkdirs();
                    }

                    if (((RadioButton)views.FindViewById(Resource.Id.radio_text)).Checked)
                    {
                        string path = dir.AbsolutePath + "/" + input.Text.ToString() + ".txt";

                        if (annoMode == ANNOTATE_FILE)
                        {
                            System.IO.FileStream fis = new System.IO.FileStream(curFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                            System.IO.FileStream fos = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                            CopyFile(fis, fos);
                            fis.Close();
                            fos.Close();
                        }
                        else
                        {
                            System.IO.StreamWriter fw = new System.IO.StreamWriter(path);
                            fw.Write(pastedText.Substring(0, pastedText.Length));
                            fw.Flush();
                            fw.Close();
                        }

                        Toast.MakeText(Application, "Saved to " + path, ToastLength.Long).Show();
                    }
                    else if (((RadioButton)views.FindViewById(Resource.Id.radio_pinyin)).Checked)
                    {
                        string path = dir.AbsolutePath + "/" + input.Text.ToString() + ".txt";

                        view.Enabled = false;
                        dumpFilePath = path;
                        dumpFileWriter = new System.IO.StreamWriter(path);
                        dumpProgress = (ProgressBar)views.FindViewById(Resource.Id.progress);
                        dumpProgressText = (TextView)views.FindViewById(Resource.Id.progress_text);
                        dumpProgress.Visibility = ViewStates.Visible;
                        dumpProgressText.Visibility = ViewStates.Visible;
                        dumpCancelled = false;
                        DumpPinyin(0, 0);
                    }
                    else if (((RadioButton)views.FindViewById(Resource.Id.radio_both)).Checked)
                    {
                        string path = dir.AbsolutePath + "/" + input.Text.ToString() + ".tsv";

                        view.Enabled = false;
                        dumpFilePath = path;
                        dumpFileWriter = new System.IO.StreamWriter(path);
                        dumpProgress = (ProgressBar)views.FindViewById(Resource.Id.progress);
                        dumpProgressText = (TextView)views.FindViewById(Resource.Id.progress_text);
                        dumpProgress.Visibility = ViewStates.Visible;
                        dumpProgressText.Visibility = ViewStates.Visible;
                        dumpCancelled = false;
                        DumpBoth(0, 0);
                    }

                }
                catch (Exception ex)
                {
                    Toast.MakeText(Application, ex.Message, ToastLength.Long).Show();
                }
            };

            Button dumpCancelButton = alert.GetButton((int)DialogButtonType.Negative);
            dumpCancelButton.Click += (sender, e) =>
            {
                dumpCancelled = true;

                alert.Dismiss();
            };
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode)
            {
                case SETTINGS_ACTIVITY_CODE:
                    if ((int)resultCode == RESULT_SETTINGS_CHANGED)
                    {
                        settingsChanged = true;
                    }
                    break;
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }

        public void SplitAnnotation(int splitLineIndex, int curWidth, List<object> curLine)
        {
            if (annTask != null && annTask.status == AsyncTask.Status.Running)
            {
                annTask.Cancel(true);
            }

            annTask = new AnnTask(this, AnnTask.TASK_SPLIT, annoMode, curPos, startPos, endPos, curWidth, splitLineIndex, lines, curLine, pastedText, textLen, openedFile, testView, UpdateLinesAnnInterface, true, mBookmarks);
            annTask.ExecuteWrapper();
        }

        public bool CheckIsShared()
        {
            Intent intent = Intent;
            string action = intent.Action;
            string type = intent.Type;

            if ((Intent.ActionSend.Equals(action) || Intent.ExtraProcessText.Equals(action)) && type != null && "text/plain".Equals(type))
            {
                string text = null;
                if (Intent.ActionEdit.Equals(action))
                {
                    text = intent.GetStringExtra(Intent.ExtraText);
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.M) //ACTION_PROCESS_TEXT
                {
                    text = intent.GetStringExtra(Intent.ExtraProcessText);
                }

                if (text != null && text.Length > 0)
                {
                    SaveFilePos();
                    pastedText = text;
                    textLen = pastedText.Length;
                }
                else
                {
                    return false;
                }
                intent.RemoveExtra(Intent.ExtraText);
                mFoundBookmarks.Clear();
                mBookmarks.Clear();
                annoMode = ANNOTATE_SHARE;
                Annotate(-1);
                return true;
            }

            return false;
        }

        //When rotated
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);

            settingsChanged = true;

            wPopup.Configure(this.Application);
            wPopup.dismiss();

            if (curPos > 0)
            {
                if (annTask.status == AsyncTask.Status.Running)
                {
                    annTask.Cancel(true);
                    return;
                }

                Redraw();
            }
        }

        //When settings are set
        protected override void OnResume()
        {
            base.OnResume();

            isActive = true;

            NotificationManager mNotificationManager = (NotificationManager)GetSystemService(Context.NotificationService);
            mNotificationManager.CancelAll();

            if (CheckIsShared())
            {
                return;
            }

            if (settingsChanged)
            {
                wPopup.dismiss();
                wPopup.Configure(this.Application);

                Redraw();
            }
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;

            CheckIsShared();
        }

        public long GetPosition(List<List<object>> list, int lineNum, int wordNum, bool isFile)
        {
            long pos = startPos;

            for (int i = 0; i < lineNum; i++)
            {
                List<object> row = list[i];
                int len = row.Count;

                if (i == lineNum - 1 && wordNum > -1)
                {
                    len = wordNum;
                }

                for (int j = 0; j < len; j++)
                {
                    object word = row[j];

                    if (word is string)
                    {
                        try
                        {
                            int wordLen;
                            if (isFile)
                            {
                                wordLen = Encoding.UTF8.GetBytes((string)word).Length;
                            }
                            else
                            {
                                wordLen = ((string)word).Length;
                            }

                            if (wordLen == 0)
                            {
                                pos += 1;
                            }
                            else
                            {
                                pos += wordLen;
                            }
                        }
                        catch (Exception e)
                        {
                            Toast.MakeText(this.Application, e.Message, ToastLength.Long).Show();
                        }
                    }
                    else
                    {
                        if (isFile)
                        {
                            pos += Dict.GetLength((int)word) * 3;
                        }
                        else
                        {
                            pos += Dict.GetLength((int)word);
                        }
                    }
                }
            }

            return pos;
        }
        //TODO: called too often
        public void SaveFilePos()
        {
            if (curFilePath.Equals(""))
            {
                return;
            }

            int curRow = linesLayoutManager.FindFirstVisibleItemPosition() - 1;

            GetPreferences(FileCreationMode.Private).Edit().PutLong(curFilePath, GetPosition(lines, curRow, -1, true)).Commit();

            List<string> recent = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                string item = GetPreferences(FileCreationMode.Private).GetString("recent" + i, "");
                if (item.Length > 0)
                {
                    recent.Add(item);
                }
            }
            int pos = recent.IndexOf(curFilePath);
            if (pos > -1)
            {
                recent.RemoveAt(pos);
            }
            if (recent.Count > 3)
            {
                recent.RemoveAt(3);
            }
            recent.Insert(0, curFilePath);

            for (int i = 0; i < recent.Count; i++)
            {
                GetPreferences(FileCreationMode.Private).Edit().PutString("recent" + i, recent[i]).Commit();
            }

            if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
            {
                SupportInvalidateOptionsMenu();
            }
            else
            {
                InvalidateOptionsMenu();
            }
        }

        public static void CopyFile(System.IO.Stream fis, System.IO.Stream fos)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int readLen = 0;
                while ((readLen = fis.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fos.Write(buffer, 0, readLen);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("AnnotationActivity CopyFile ERROR => " + e.Message);
            }
        }

        public override void OnBackPressed()
        {
            if (wPopup.Showing)
            {
                if (wPopup.history.Count > 0)
                {
                    int lastWord = wPopup.history[(wPopup.history.Count - 1)];
                    wPopup.history.Remove(wPopup.history.Count - 1);
                    wPopup.show(wPopup.parent, wPopup.history.Count > 0 ? null : wPopup.line, lastWord, wPopup.showX, false);
                }
                else
                {
                    wPopup.dismiss();
                }
            }
            else
            {
                base.OnBackPressed();
            }
        }
    }

    public class RecyclerViewOnScrollListener : RecyclerView.OnScrollListener
    {
        private readonly AnnotationActivity Activity;

        public RecyclerViewOnScrollListener(AnnotationActivity activity)
        {
            this.Activity = activity;
        }

        public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
        {
            base.OnScrolled(recyclerView, dx, dy);

            int totalItemCount = Activity.linesLayoutManager.ItemCount - 2;
            int firstVisibleItem = Activity.linesLayoutManager.FindFirstVisibleItemPosition();
            int lastVisibleItem = Activity.linesLayoutManager.FindLastVisibleItemPosition() - 1;
            int visibleItemCount = lastVisibleItem - firstVisibleItem;

            if (totalItemCount != 0 && Activity.textLen != 0 && totalItemCount > visibleItemCount)
            {
                ((CustomRecyclerView)recyclerView).progress = (Activity.startPos + (Activity.endPos - Activity.startPos) * lastVisibleItem / (float)totalItemCount) / Activity.textLen;
            }
            else
            {
                ((CustomRecyclerView)recyclerView).progress = -1;
            }

            if (totalItemCount <= lastVisibleItem && Activity.endPos < Activity.textLen)
            {
                if (Activity.annTask == null || Activity.annTask.status != AsyncTask.Status.Running)
                {
                    Activity.annTask = new AnnTask(Activity, AnnTask.TASK_ANNOTATE, Activity.annoMode, Activity.curPos, Activity.startPos, Activity.endPos, 5, 0, Activity.lines, new List<object>(), Activity.pastedText, Activity.textLen, Activity.openedFile, Activity.testView, Activity.UpdateLinesAnnInterface, true, Activity.mBookmarks);
                    Activity.annTask.ExecuteWrapper();
                }
                return;
            }

            if (firstVisibleItem == 0 && Activity.linesAdapter.ShowHeader && Activity.startPos > 0)
            {
                if (Activity.annTask.status != AsyncTask.Status.Running)
                {
                    Activity.annTask = new AnnTask(Activity, AnnTask.TASK_ANNOTATE_BACK, Activity.annoMode, Activity.curPos, Activity.startPos, Activity.endPos, 5, 0, Activity.lines, new List<object>(), Activity.pastedText, Activity.textLen, Activity.openedFile, Activity.testView, Activity.UpdateLinesAnnInterface, true, Activity.mBookmarks);
                    Activity.annTask.ExecuteWrapper();
                }
            }
        }

        public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
        {
            Activity.wPopup.dismiss();
        }
    }
}