using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database.Sqlite;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Util;
using Java.Util.Zip;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    [Activity(Label = "Starred", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize, LaunchMode = LaunchMode.SingleTask)]
    public class StarredActivity : AnnotationActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            wPopup.dismiss();
            pastedText = sharedPrefs.GetString("stars", "");
            textLen = pastedText.Length;
            annoMode = ANNOTATE_STARRED;
            Annotate(-1);

            int count = 0;
            for (int c = 0; c < textLen; c++)
            {
                if (pastedText.ToCharArray()[c] == '\n')
                {
                    count++;
                }
            }
            Title = "Starred(" + count + ")";
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.starred_menu, menu);

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            // Handle presses on the action bar items
            try
            {
                switch (item.ItemId)
                {
                    case Resource.Id.menu_starred_export:
                    case Resource.Id.menu_starred_export_pinyin:
                        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, item.ItemId == Resource.Id.menu_starred_export ? REQUEST_STORAGE_FOR_STARRED_EXPORT : REQUEST_STORAGE_FOR_STARRED_EXPORT_PINYIN);
                        }
                        else
                        {
                            StarredExport(item.ItemId);
                        }

                        return true;

                    case Resource.Id.menu_starred_export_pleco:
                        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, REQUEST_STORAGE_FOR_STARRED_EXPORT_PLECO);
                        }
                        else
                        {
                            StarredExportPleco();
                        }

                        return true;

                    case Resource.Id.menu_starred_clear:
                        (new AlertDialog.Builder(this)).SetTitle("Clear all starred")
                            .SetMessage("Are you sure you want to clear all starred words?")
                            .SetPositiveButton(global::Android.Resource.String.Yes, delegate
                            {
                                sharedPrefs.Edit().PutString("stars", "").Commit();
                                pastedText = "";
                                textLen = 0;
                                lines.Clear();
                                linesAdapter.NotifyDataSetChanged();
                                linesAdapter.ShowHeader = false;
                                linesAdapter.ShowFooter = false;
                                Title = "Starred(0)";
                                Toast.MakeText(this, "All starred words cleared", ToastLength.Long).Show();
                            })
                            .SetNegativeButton(global::Android.Resource.String.No, delegate { })
                            .SetIcon(global::Android.Resource.Drawable.IcDialogAlert).Show();

                        return true;
                    default:
                        return base.OnOptionsItemSelected(item);
                }
            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
            }

            return false;
        }

        public void StarredExport(int exportId)
        {

            string stars = sharedPrefs.GetString("stars", "");
            if (stars == null || stars.Length < 2)
            {
                Toast.MakeText(this, "Starred list is empty. Nothing to export.", ToastLength.Long).Show();
                return;
            }

            System.Random random = new System.Random();
            SQLiteDatabase db = OpenOrCreateDatabase("anki.db", FileCreationMode.Private, null);
            string[] commands = Regex.Split(GetString(Resource.String.anki_scheme), ";;");
            foreach (string command in commands)
            {
                db.ExecSQL(command);
            }

            int oldIndex = 0;
            int nIndex;
            while ((nIndex = stars.IndexOf("\n", oldIndex)) > -1)
            {

                int entry = Dict.BinarySearch(stars.SubstringSpecial(oldIndex, nIndex), false);
                oldIndex = nIndex + 1;

                if (entry == -1)
                {
                    continue;
                }

                string id = Convert.ToString(Math.Abs(random.Next())), uuid = UUID.RandomUUID().ToString().SubstringSpecial(0, 10);
                StringBuilder english = new StringBuilder();

                if (exportId == Resource.Id.menu_starred_export_pinyin)
                {
                    english.Append(Dict.PinyinToTones(Dict.GetPinyin(entry))).Append("<br/>");
                }

                english.Append(Dict.GetCh(entry)).Append("\u001F");

                if (exportId == Resource.Id.menu_starred_export)
                {
                    english.Append("[ ").Append(Dict.PinyinToTones(Dict.GetPinyin(entry))).Append(" ]<br/>");
                }

                string[] parts = Regex.Split(Regex.Replace(Dict.GetEnglish(entry), @"/", "<br/>• "), @"\\$");
                int j = 0;
                foreach (string str in parts)
                {
                    if (j++ % 2 == 1)
                    {
                        english.Append("<br/><br/>[ ").Append(Dict.PinyinToTones(str)).Append(" ]<br/>");
                    }
                    else
                    {
                        english.Append("• ");

                        int bracketIndex, bracketEnd = 0;
                        while ((bracketIndex = str.IndexOf("[", bracketEnd)) > -1)
                        {
                            english.Append(str, bracketEnd, bracketIndex);
                            bracketEnd = str.IndexOf("]", bracketIndex);
                            english.Append(Dict.PinyinToTones(str.SubstringSpecial(bracketIndex, bracketEnd)));
                        }
                        english.Append(str, bracketEnd, str.Length);
                    }
                }

                db.ExecSQL("insert into notes values(?, ?, 1399962367564, 1400901624, -1, '', ?, 0, 147133787, 0, '')", new Java.Lang.Object[] { id, uuid, english.ToString() });

                db.ExecSQL("insert into cards values(?, ?, 1400901287521, 0, 1400901624, -1, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, '')", new Java.Lang.Object[] { Convert.ToString(random.Next()), id });
            }
            db.Close();

            try
            {
                File dir = new File(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "/AnkiDroid");
                if (!dir.Exists())
                {
                    dir = new File(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "/ChineseReader");
                }

                dir.Mkdirs();
                System.IO.Stream fos = new System.IO.FileStream(dir.AbsolutePath + "/chinesereader_starred.apkg", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                System.IO.Stream fiz = new System.IO.FileStream(GetDatabasePath("anki.db").Path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                ZipOutputStream zos = new ZipOutputStream(fos);

                zos.PutNextEntry(new ZipEntry("collection.anki2"));
                byte[] buffer = new byte[1024];
                int readLen = 0;
                while ((readLen = fiz.Read(buffer, 0, buffer.Length)) > 0)
                {
                    zos.Write(buffer, 0, readLen);
                }
                zos.CloseEntry();

                zos.PutNextEntry(new ZipEntry("media"));
                buffer[0] = 0x7b;
                buffer[1] = 0x7d;
                zos.Write(buffer, 0, 2);
                zos.CloseEntry();
                zos.Flush();
                zos.Close();
                fiz.Close();
                fos.Flush();
                fos.Close();

                Toast.MakeText(this, "Successfully exported to " + dir.AbsolutePath + "/chinesereader_starred.apkg", ToastLength.Long).Show();

            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Could not export: " + e.Message, ToastLength.Long).Show();
            }
        }

        public virtual void StarredExportPleco()
        {
            string stars = sharedPrefs.GetString("stars", "");
            if (stars.Length < 2)
            {
                Toast.MakeText(this, "Starred list is empty. Nothing to export.", ToastLength.Long).Show();
                return;
            }

            try
            {
                File dir = new File(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "/ChineseReader");

                dir.Mkdirs();
                System.IO.FileStream os = new System.IO.FileStream(dir.AbsolutePath + "/chinesereader_starred.txt", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                BufferedOutputStream bos = new BufferedOutputStream(os);

                StringBuilder english = new StringBuilder();

                int oldIndex = 0, nIndex;
                while ((nIndex = stars.IndexOf("\n", oldIndex)) > -1)
                {
                    english.Length = 0;

                    int entry = Dict.BinarySearch(stars.SubstringSpecial(oldIndex, nIndex), false);
                    oldIndex = nIndex + 1;

                    if (entry == -1)
                    {
                        continue;
                    }

                    english.Append(Dict.GetCh(entry)).Append("\t").Append(Regex.Replace(Dict.GetPinyin(entry), @"(\\d)", "$1 ")).Append("\t");

                    string[] parts = Regex.Split(Regex.Replace(Dict.GetEnglish(entry), @"/", "; "), @"\\$");
                    int j = 0;
                    foreach (string str in parts)
                    {
                        if (j++ % 2 == 1)
                        {
                            english.Append(" [ ").Append(Regex.Replace(str, @"(\\d)", "$1 ")).Append("] ");
                        }
                        else
                        {
                            english.Append(str);
                        }
                    }
                    english.Append("\n");

                    bos.Write(Encoding.UTF8.GetBytes(english.ToString()));
                }
                os.Flush();
                bos.Flush();
                bos.Close();
                os.Close();

                Toast.MakeText(this, "Successfully exported to " + dir.AbsolutePath + "/chinesereader_starred.txt", ToastLength.Long).Show();

            }
            catch (Exception e)
            {
                Toast.MakeText(this, "Could not export: " + e.Message, ToastLength.Long).Show();
            }
        }


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                switch (requestCode)
                {
                    case REQUEST_STORAGE_FOR_SAVE:
                        SaveToFile();
                        break;
                    case REQUEST_STORAGE_FOR_STARRED_EXPORT:
                        StarredExport(Resource.Id.menu_starred_export);
                        break;
                    case REQUEST_STORAGE_FOR_STARRED_EXPORT_PINYIN:
                        StarredExport(Resource.Id.menu_starred_export_pinyin);
                        break;
                    case REQUEST_STORAGE_FOR_STARRED_EXPORT_PLECO:
                        StarredExportPleco();
                        break;
                }
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode)
            {
                case SETTINGS_ACTIVITY_CODE:
                    if ((int)resultCode == RESULT_SETTINGS_CHANGED)
                    {
                        settingsChanged = true;
                        parentSettingsChanged = true;
                    }
                    break;
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }

        public override void OnBackPressed()
        {
            if (wPopup.Showing)
            {
                if (wPopup.history.Count > 0)
                {
                    int lastWord = wPopup.history[wPopup.history.Count - 1];
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
                if (parentSettingsChanged)
                {
                    Intent resultIntent = new Intent();
                    SetResult((Result)AnnotationActivity.RESULT_SETTINGS_CHANGED, resultIntent);
                    Finish();
                }
                base.OnBackPressed();
            }
        }
    }
}