using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using Android.Views;
using System;
using System.Text.RegularExpressions;
using Java.IO;
using Android.Support.V4.App;
using Android;
using Android.Content;
using Android.Support.V4.Content;

namespace ChineseReader.Android
{
    [Activity(Label = "@string/app_name",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation|ConfigChanges.ScreenSize,
        LaunchMode = LaunchMode.SingleTask,
        WindowSoftInputMode = SoftInput.AdjustNothing)]
    [IntentFilter(new[] { Intent.ActionSend },
        Categories = new[] { Intent.CategoryDefault },
        DataMimeType = "text/plain")]
    [IntentFilter(new[] { Intent.ActionProcessText },
        Categories = new[] { Intent.CategoryDefault },
        DataMimeType = "text/plain")]
    public class MainActivity : AnnotationActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            System.Console.WriteLine("MainActivity >> OnCreate");

            try
            {
                PackageInfo pInfo = PackageManager.GetPackageInfo(PackageName, 0);
                int oldVer = sharedPrefs.GetInt("version", 0);
                if (oldVer != pInfo.VersionCode)
                {
                    string stars = sharedPrefs.GetString("stars", "");
                    stars = stars.Replace(" ", "");
                    if (stars.EndsWith(";"))
                    {
                        stars = stars.Substring(1).Replace(";", @"\n");
                    }

                    sharedPrefs.Edit().PutString("stars", stars).Commit();

                    (new File(FilesDir, "dict.db")).Delete();
                    (new File(FilesDir, "idx.db")).Delete();
                    (new File(FilesDir, "entries.bin")).Delete();
                    (new File(FilesDir, "parts.bin")).Delete();
                    new File(global::Android.OS.Environment.ExternalStorageDirectory + "/ChineseReader/").Mkdir();
                    sharedPrefs.Edit().PutInt("version", pInfo.VersionCode).Commit();
                }
                if (oldVer <= 110)
                {
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, REQUEST_STORAGE_FOR_BOOK);
                }
                if (0 < oldVer && oldVer <= 370)
                {
                    string prefTextSize = sharedPrefs.GetString("pref_textSize", "medium");
                    int textSizeInt = 16;
                    if (prefTextSize.Equals("small"))
                    {
                        textSizeInt = 13;
                    }
                    if (prefTextSize.Equals("medium"))
                    {
                        textSizeInt = 16;
                    }
                    if (prefTextSize.Equals("large"))
                    {
                        textSizeInt = 19;
                    }
                    if (prefTextSize.Equals("extra"))
                    {
                        textSizeInt = 23;
                    }

                    string prefPinyinSize = sharedPrefs.GetString("pref_pinyinSize", "medium");
                    int pinyinSizeInt = 100;
                    if (prefPinyinSize.Equals("small"))
                    {
                        pinyinSizeInt = 13 * 100 / textSizeInt;
                    }
                    if (prefPinyinSize.Equals("medium"))
                    {
                        pinyinSizeInt = 16 * 100 / textSizeInt;
                    }
                    if (prefPinyinSize.Equals("large"))
                    {
                        pinyinSizeInt = 19 * 100 / textSizeInt;
                    }
                    if (prefPinyinSize.Equals("extra"))
                    {
                        pinyinSizeInt = 23 * 100 / textSizeInt;
                    }

                    sharedPrefs.Edit().PutInt("pref_textSizeInt", textSizeInt).PutInt("pref_pinyinSizeInt", pinyinSizeInt).Commit();
                }

                Dict.LoadDict(this.Application);

                pastedText = GetBufText(this);
                textLen = pastedText.Length;
                if (isFirstAnnotation)
                {
                    string lastFile = sharedPrefs.GetString("lastFile", "");
                    if (lastFile.Length > 0 && sharedPrefs.GetString("lastText", "").Equals(pastedText))
                    {
                        Annotate(lastFile);
                        return;
                    }
                }

                Title = "Chinese Reader";

                if (!CheckIsShared())
                {
                    if (textLen == 0)
                    {
                        Toast.MakeText(this.Application, GetString(Resource.String.msg_empty), ToastLength.Long).Show();
                    }
                    else
                    {
                        annoMode = ANNOTATE_BUFFER;
                        Annotate(-1);
                    }
                }

            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.main, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnPrepareOptionsMenu(IMenu menu)
        {
            IMenuItem openMenu = menu.FindItem(Resource.Id.menu_open_button);
            if (openMenu != null)
            {
                ISubMenu subMenu = openMenu.SubMenu;
                for (int i = 0; i < 4; i++)
                {
                    subMenu.RemoveItem(i);
                    string item = GetPreferences(global::Android.Content.FileCreationMode.Private).GetString("recent" + i, "");
                    if (item.Length > 0)
                    {
                        subMenu.Add(Menu.None, i, (int)MenuCategory.Secondary, item.Substring(item.LastIndexOf('/') + 1));
                    }
                }
            }

            IMenuItem gotoMenu = menu.FindItem(Resource.Id.action_goto);
            if (linesRecyclerView.progress == -1)
            {
                gotoMenu.SetVisible(false);
            }
            else
            {
                gotoMenu.SetVisible(true);
            }

            IMenuItem bookmarksMenu = menu.FindItem(Resource.Id.menu_bookmarks);
            if (annoMode == ANNOTATE_FILE)
            {
                bookmarksMenu.SetVisible(true);
            }
            else
            {
                bookmarksMenu.SetVisible(false);
            }

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            // Handle presses on the action bar items
            try
            {
                switch (item.ItemId)
                {
                    case Resource.Id.action_paste:
                        pastedText = GetBufText(this);
                        if (pastedText.Length == 0)
                        {
                            Toast.MakeText(this.Application, GetString(Resource.String.msg_empty), ToastLength.Long).Show();
                            return true;
                        }

                        SaveFilePos();
                        annoMode = ANNOTATE_BUFFER;
                        textLen = pastedText.Length;
                        Title = "Chinese Reader";
                        mBookmarks.Clear();
                        mFoundBookmarks.Clear();
                        Annotate(-1);
                        return true;

                    case Resource.Id.action_open:
                        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, REQUEST_STORAGE_FOR_FILEBROWSER);
                        }
                        else
                        {
                            OpenFileBrowser();
                        }

                        return true;

                    case Resource.Id.action_save:
                        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, REQUEST_STORAGE_FOR_SAVE);
                        }
                        else
                        {
                            SaveToFile();
                        }

                        return true;

                    case Resource.Id.action_about:
                        AboutDialog about = new AboutDialog(this);
                        about.Show();
                        return true;

                    case Resource.Id.menu_starred:
                        if (annTask != null && annTask.status == AsyncTask.Status.Running)
                        {
                            annTask.Cancel(true);
                        }
                        SaveFilePos();
                        StartActivityForResult(new Intent(this, typeof(StarredActivity)), STARRED_ACTIVITY_CODE);

                        return true;

                    case Resource.Id.menu_bookmarks:
                        AlertDialog.Builder builderSingle = new AlertDialog.Builder(this);
                        builderSingle.SetIcon(Resource.Drawable.ic_launcher);
                        builderSingle.SetTitle("Bookmarks");

                        ArrayAdapter<string> arrayAdapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SelectDialogItem);

                        for (int i = 0; i < mBookmarks.Count; i++)
                        {
                            arrayAdapter.Add(mBookmarks[i].mTitle);
                        }

                        builderSingle.SetNegativeButton("Cancel", (sender, e) =>
                        {
                            ((AlertDialog)sender).Dismiss();
                        });

                        builderSingle.SetAdapter(arrayAdapter, (sender, e) =>
                        {
                            Annotate(mBookmarks[e.Which].mPosition);
                        });
                        builderSingle.Show();

                        return true;

                    default:
                        if (item.ItemId < 4)
                        {
                            string path = GetPreferences(FileCreationMode.Private).GetString("recent" + item.ItemId, "");
                            SaveFilePos();
                            Annotate(path);
                        }

                        return base.OnOptionsItemSelected(item);
                }
            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
            }

            return false;
        }

        public void OpenFileBrowser()
        {
            Intent fileExploreIntent = new Intent(FileBrowserActivity.INTENT_ACTION_SELECT_FILE, null, this, typeof(FileBrowserActivity));
            string lastOpenDir = sharedPrefs.GetString("lastOpenDir", "");
            if (!lastOpenDir.Equals(""))
            {
                fileExploreIntent.PutExtra(FileBrowserActivity.startDirectoryParameter, lastOpenDir);
            }

            StartActivityForResult(fileExploreIntent, FILEBROWSER_ACTIVITY_CODE);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode)
            {
                case FILEBROWSER_ACTIVITY_CODE:
                    if (resultCode == Result.Ok)
                    {
                        SaveFilePos();
                        string file = data.GetStringExtra(FileBrowserActivity.returnFileParameter);
                        sharedPrefs.Edit().PutString("lastOpenDir", (new System.IO.DirectoryInfo(file)).Parent.FullName).Commit();
                        Annotate(file);
                    }
                    break;
                case STARRED_ACTIVITY_CODE:
                    if ((int)resultCode == RESULT_SETTINGS_CHANGED)
                    {
                        settingsChanged = true;
                    }
                    break;
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (grantResults.Length > 0 && grantResults[0] == (int)Permission.Granted)
            {
                switch (requestCode)
                {
                    case REQUEST_STORAGE_FOR_BOOK:
                        try
                        {
                            System.IO.Stream fis = Assets.Open("shei-shenghuo-de-geng-meihao.txt");
                            File outFile = new File(global::Android.OS.Environment.ExternalStorageDirectory, "/ChineseReader/shei-shenghuo-de-geng-meihao.txt");
                            System.IO.Stream fout = new System.IO.FileStream(outFile.Path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                            CopyFile(fis, fout);
                            fis.Close();
                            fout.Flush();
                            fout.Close();
                        }
                        catch (Exception e)
                        {
                            System.Console.WriteLine("MainActivity OnRequestPermissionResult ERROR => " + e.Message);
                        }
                        break;
                    case REQUEST_STORAGE_FOR_FILEBROWSER:
                        OpenFileBrowser();
                        break;
                    case REQUEST_STORAGE_FOR_SAVE:
                        SaveToFile();
                        break;
                }
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            SaveFilePos();

            if (annoMode == ANNOTATE_FILE)
            {
                sharedPrefs.Edit().PutString("lastFile", curFilePath).Commit();
            }
            else
            {
                sharedPrefs.Edit().PutString("lastFile", "").PutString("lastText", pastedText).Commit();
            }

            isActive = false;
            if (sharedPrefs.GetBoolean("pref_monitor", true) && Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            {
                StartService(new Intent(this, typeof(PinyinerClipboardService)));
            }
        }
    }
}