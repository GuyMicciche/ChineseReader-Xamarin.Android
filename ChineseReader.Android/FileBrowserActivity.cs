using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Java.IO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    //Project type now is Android library:
    //  http://developer.android.com/guide/developing/projects/projects-eclipse.html#ReferencingLibraryProject

    [Activity(Label = "File Browser", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden)]
    [IntentFilter(new[] { INTENT_ACTION_SELECT_DIR })]
    public class FileBrowserActivity : Activity, IFilenameFilter
    {
        // Intent Action Constants
        public const string INTENT_ACTION_SELECT_DIR = "com.gem.chinesereader.SELECT_DIRECTORY_ACTION";
        public const string INTENT_ACTION_SELECT_FILE = "com.gem.chinesereader.SELECT_FILE_ACTION";

        // Intent parameters names constants
        public const string startDirectoryParameter = "com.gem.chinesereader.directoryPath";
        public const string returnDirectoryParameter = "com.gem.chinesereader.directoryPathRet";
        public const string returnFileParameter = "com.gem.chinesereader.filePathRet";
        public const string showCannotReadParameter = "com.gem.chinesereader.showCannotRead";
        public const string filterExtension = "com.gem.chinesereader.filterExtension";

        // Stores names of traversed directories
        public List<Item> pathDirsList = new List<Item>();

        // Check if the first level of the directory structure is the one showing
        // private Boolean firstLvl = true;

        private const string LOGTAG = "F_PATH";

        private List<Item> fileList = new List<Item>();
        private List<Item> sdList = new List<Item>();
        private File path = null;
        private string chosenFile;
        // private static final int DIALOG_LOAD_FILE = 1000;

        public ArrayAdapter<Item> Adapter;

        private bool showHiddenFilesAndDirs = true;

        private bool directoryShownIsEmpty = false;

        private string filterFileExtension = null;

        // Action constants
        private static int currentAction = -1;
        private const int SELECT_DIRECTORY = 1;
        private const int SELECT_FILE = 2;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // In case of
            // ua.com.vassiliev.androidfilebrowser.SELECT_DIRECTORY_ACTION
            // Expects com.mburman.fileexplore.directoryPath parameter to
            // point to the start folder.
            // If empty or null, will start from SDcard root.
            SetContentView(Resource.Layout.FileBrowser);

            // Set action for this activity
            Intent thisInt = this.Intent;
            currentAction = SELECT_DIRECTORY; // This would be a default action in
                                              // case not set by intent
            if (thisInt.Action.Equals(INTENT_ACTION_SELECT_FILE))
            {
                currentAction = SELECT_FILE;
            }

            showHiddenFilesAndDirs = thisInt.GetBooleanExtra(showCannotReadParameter, true);

            filterFileExtension = thisInt.GetStringExtra(filterExtension);

            SetInitialDirectory();
            GetSdCards();

            ParseDirectoryPath();

            this.CreateFileListAdapter();
            this.InitializeButtons();
            this.InitializeFileListView();

            LoadFileList();
            UpdateCurrentDirectoryTextView();
        }

        private void GetSdCards()
        {
            sdList.Clear();

            sdList.Add(new Item(Java.Lang.JavaSystem.Getenv("EXTERNAL_STORAGE"), "Internal storage", Resource.Drawable.folder_icon));
            string sSecondary = Java.Lang.JavaSystem.Getenv("SECONDARY_STORAGE");

            if (sSecondary != null)
            {
                //string[] sCards = sSecondary.Split(":", true);
                string[] sCards = Regex.Split(sSecondary, ":");
                int i = 1;
                foreach (string card in sCards)
                {
                    File file = new File(card);
                    if (file.CanRead())
                    {
                        sdList.Add(new Item(card, "SD card" + (i > 1 ? " " + i : ""), Resource.Drawable.folder_icon));
                        i++;
                    }
                }
            }
        }

        private void SetInitialDirectory()
        {
            Intent thisInt = this.Intent;
            string requestedStartDir = thisInt.GetStringExtra(startDirectoryParameter);

            if (requestedStartDir != null && requestedStartDir.Length > 0)
            { // if(requestedStartDir!=null
                File tempFile = new File(requestedStartDir);
                if (tempFile.IsDirectory)
                {
                    this.path = tempFile;
                }
            } // if(requestedStartDir!=null

            if (this.path == null)
            { // No or invalid directory supplied in intent
              // parameter
                if (global::Android.OS.Environment.ExternalStorageDirectory.IsDirectory && global::Android.OS.Environment.ExternalStorageDirectory.CanRead())
                {
                    path = new File(global::Android.OS.Environment.ExternalStorageDirectory, "/ChineseReader/");
                    if (!path.CanRead())
                    {
                        path = new File(global::Android.OS.Environment.ExternalStorageDirectory, "");
                    }
                }
                else
                {
                    path = null;
                }
            }
        } // private void setInitialDirectory() {

        private void ParseDirectoryPath()
        {
            pathDirsList.Clear();

            File pathCopy = path;
            while (pathCopy != null && pathCopy.Parent != null)
            {
                foreach (Item sdcard in sdList)
                {
                    if (sdcard.file.Equals(pathCopy.AbsolutePath))
                    {
                        pathDirsList.Insert(0, sdcard);
                        return;
                    }
                }

                pathDirsList.Insert(0, new Item(pathCopy.AbsolutePath, pathCopy.Name, 0));
                pathCopy = pathCopy.ParentFile;
            }
        }

        private void InitializeButtons()
        {
            Button upDirButton = (Button)this.FindViewById(Resource.Id.upDirectoryButton);
            upDirButton.Click += (sender, e) =>
                {
                    if (pathDirsList.Count == 0)
                    {
                        return;
                    }

                    // present directory removed from list
                    pathDirsList.RemoveAt(pathDirsList.Count - 1);
                    if (pathDirsList.Count > 0)
                    {
                        path = new File(pathDirsList[pathDirsList.Count - 1].file);
                    }

                    LoadFileList();
                    Adapter.NotifyDataSetChanged();
                    UpdateCurrentDirectoryTextView();
                };
            
            Button selectFolderButton = (Button)this.FindViewById(Resource.Id.selectCurrentDirectoryButton);
            if (currentAction == SELECT_DIRECTORY)
            {
                selectFolderButton.Click += (sender, e) =>
                  {
                      ReturnDirectoryFinishActivity();
                  };
            }
            else
            {
                selectFolderButton.Visibility = ViewStates.Gone;
            }
        }

        private void UpdateCurrentDirectoryTextView()
        {
            int i = 0;
            string curDirString = "";
            while (i < pathDirsList.Count)
            {
                curDirString += pathDirsList[i].title + "/";
                i++;
            }
            if (pathDirsList.Count == 0)
            {
                ((Button)this.FindViewById(Resource.Id.upDirectoryButton)).Enabled = false;
            }
            else
            {
                ((Button)this.FindViewById(Resource.Id.upDirectoryButton)).Enabled = true;
            }

            ((TextView)this.FindViewById(Resource.Id.currentDirectoryTextView)).Text = "Current directory: " + curDirString;
        }

        private void showToast(string message)
        {
            Toast.MakeText(this, message, ToastLength.Long).Show();
        }

        private void InitializeFileListView()
        {
            ListView lView = (ListView)this.FindViewById(Resource.Id.fileListView);
            lView.SetBackgroundColor(Color.LightGray);
            LinearLayout.LayoutParams lParam = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.FillParent);
            lParam.SetMargins(15, 5, 15, 5);
            lView.Adapter = this.Adapter;
            lView.ItemClick += (sender, e) =>
                {
                    File sel = null;
                    chosenFile = fileList[e.Position].file;
                    sel = new File(fileList[e.Position].file);

                    if (sel.IsDirectory)
                    {
                        if (sel.CanRead())
                        {
                            // Adds chosen directory to list
                            if (pathDirsList.Count == 0)
                            { //cards lsit
                                pathDirsList.Add(sdList[e.Position]);
                                path = new File(sdList[e.Position].file);
                            }
                            else
                            {
                                pathDirsList.Add(new Item(chosenFile, (new System.IO.FileInfo(chosenFile)).Name, 0));
                                path = new File(sel + "");
                            }
                            LoadFileList();
                            Adapter.NotifyDataSetChanged();
                            UpdateCurrentDirectoryTextView();
                        }
                        else
                        {
                            showToast("Path does not exist or cannot be read");
                        }
                    }
                      // File picked or an empty directory message clicked
                    else
                    { 
                        if (!directoryShownIsEmpty)
                        {
                            ReturnFileFinishActivity(sel.AbsolutePath);
                        }
                    }
                };
        }

        private void ReturnDirectoryFinishActivity()
        {
            Intent retIntent = new Intent();
            retIntent.PutExtra(returnDirectoryParameter, path.AbsolutePath);
            SetResult(Result.Ok, retIntent);
            Finish();
        }

        private void ReturnFileFinishActivity(string filePath)
        {
            Intent retIntent = new Intent();
            retIntent.PutExtra(returnFileParameter, filePath);
            SetResult(Result.Ok, retIntent);
            Finish();
        }

        private void LoadFileList()
        {
            fileList.Clear();

            if (pathDirsList.Count == 0)
            {
                Adapter.Clear();
                for (int i = 0; i < sdList.Count; i++)
                {
                    Adapter.Add(sdList[i]);
                }

                return;
            }

            try
            {
                path.Mkdirs();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("FileBrowserActivity LoadFileList ERROR => " + e.Message);

                return;
            }

            if (path.Exists() && path.CanRead())
            {
                string[] fList = path.List(this);
                this.directoryShownIsEmpty = false;
                for (int i = 0; i < fList.Length; i++)
                {
                    // Convert into file path
                    File sel = new File(path, fList[i]);
                    int drawableID = Resource.Drawable.file_icon;
                    bool canRead = sel.CanRead();
                    // Set drawables
                    if (sel.IsDirectory)
                    {
                        if (canRead)
                        {
                            drawableID = Resource.Drawable.folder_icon;
                        }
                        else
                        {
                            drawableID = Resource.Drawable.folder_icon_light;
                        }
                    }
                    fileList.Insert(i, new Item(sel.AbsolutePath, fList[i], drawableID));
                }
                if (fileList.Count == 0)
                {
                    this.directoryShownIsEmpty = true;
                    fileList.Insert(0, new Item("", "Directory is empty", -1));
                }
                else
                { // sort non empty list
                    fileList.Sort(new ItemFileNameComparator(this));
                }
            }
            else
            {

            }
        }

        public bool Accept(File dir, string filename)
        {
            File sel = new File(dir, filename);
            bool showReadableFile = showHiddenFilesAndDirs || sel.CanRead();
            // Filters based on whether the file is hidden or not
            if (currentAction == SELECT_DIRECTORY)
            {
                return (sel.IsDirectory && showReadableFile);
            }
            if (currentAction == SELECT_FILE)
            {
                // If it is a file check the extension if provided
                if (sel.IsFile && filterFileExtension != null)
                {
                    return (showReadableFile && sel.Name.EndsWith(filterFileExtension));
                }
                return (showReadableFile);
            }
            return true;
        }

        private void CreateFileListAdapter()
        {
            Adapter = new FileListAdapter(this, global::Android.Resource.Layout.SelectDialogItem, global::Android.Resource.Id.Text1, fileList); // adapter = new ArrayAdapter<Item>(this,
        }

        private class FileListAdapter : ArrayAdapter<Item>
        {
            private FileBrowserActivity context;

            public FileListAdapter(FileBrowserActivity context, int item, int text1, IList<Item> fileList) : base(context, item, text1, fileList)
            {
                this.context = context;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                // creates view
                View view = base.GetView(position, convertView, parent);
                TextView textView = (TextView)view.FindViewById(global::Android.Resource.Id.Text1);
                // put the image on the text view
                int drawableID = 0;
                if (context.fileList[position].icon != -1)
                {
                    // If icon == -1, then directory is empty
                    drawableID = context.fileList[position].icon;
                }
                textView.SetCompoundDrawablesWithIntrinsicBounds(drawableID, 0, 0, 0);

                textView.Ellipsize = null;

                // add margin between image and text (support various screen
                // densities)
                // int dp5 = (int) (5 *
                // getResources().getDisplayMetrics().density + 0.5f);
                int dp3 = (int)(3 * context.Resources.DisplayMetrics.Density + 0.5f);
                // TODO: change next line for empty directory, so text will be
                // centered
                textView.CompoundDrawablePadding = dp3;
                textView.SetBackgroundColor(Color.LightGray);
                return view;
            }
        }

        public class Item
        {
            public string file;
            public string title;
            public int icon;

            public Item(string file, string title, int icon)
            {
                this.file = file;
                this.title = title;
                this.icon = icon;
            }

            public override string ToString()
            {
                return title;
            }
        }

        private class ItemFileNameComparator : IComparer<Item>
        {
            private readonly FileBrowserActivity outerInstance;

            public ItemFileNameComparator(FileBrowserActivity outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public virtual int Compare(Item lhs, Item rhs)
            {
                return lhs.file.ToLower().CompareTo(rhs.file.ToLower());
            }
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            // Layout apparently changes itself, only have to provide good onMeasure
            // in custom components
            // TODO: check with keyboard
            // if(newConfig.keyboard == Configuration.KEYBOARDHIDDEN_YES)
        }

        public static long GetFreeSpace(string path)
        {
            StatFs stat = new StatFs(path);
            long availSize = (long)stat.AvailableBlocks * (long)stat.BlockSize;
            return availSize;
        }

        public static string FormatBytes(long bytes)
        {
            // TODO: add flag to which part is needed (e.g. GB, MB, KB or bytes)
            string retStr = "";
            // One binary gigabyte equals 1,073,741,824 bytes.
            if (bytes > 1073741824)
            { // Add GB
                long gbs = bytes / 1073741824;
                retStr += (new long?(gbs)).ToString() + "GB ";
                bytes = bytes - (gbs * 1073741824);
            }
            // One MB - 1048576 bytes
            if (bytes > 1048576)
            { // Add GB
                long mbs = bytes / 1048576;
                retStr += (new long?(mbs)).ToString() + "MB ";
                bytes = bytes - (mbs * 1048576);
            }
            if (bytes > 1024)
            {
                long kbs = bytes / 1024;
                retStr += (new long?(kbs)).ToString() + "KB";
                bytes = bytes - (kbs * 1024);
            }
            else
            {
                retStr += (new long?(bytes)).ToString() + " bytes";
            }
            return retStr;
        }
    }
}