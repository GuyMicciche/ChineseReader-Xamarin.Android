using Android.Content;
using Android.Preferences;
using Android.Util;
using Java.IO;
using Java.Nio;
using Java.Nio.Channels;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{   
    public class Dict
    {
        public static IntBuffer entries;
        public static RandomAccessFile dictFile;
        public static byte[] byteBuffer;
        public static List<byte[]> dictParts;
        public static List<int> dictIndexes;
        public static ISharedPreferences sharedPrefs;

        public static void LoadDict(Context context)
        {
            try
            {
                bool resaveEntries = false;
                dictParts = new List<byte[]>();
                dictIndexes = new List<int>();

                File dictFd = new File(context.FilesDir, "dict.db");
                if (!dictFd.Exists())
                { // || dictFd.length() != 4961308) {
                    System.Console.WriteLine("DOES NOT EXIST!!!!!");
                    CopyFile(context, "dict.db");
                    dictFd = new File(context.FilesDir, "dict.db");
                    resaveEntries = true;
                }
                dictFile = new RandomAccessFile(dictFd, "r");

                File idxFd = new File(context.FilesDir, "idx.db");
                if (!idxFd.Exists())
                { // || idxFd.length() != 3145553) {
                    CopyFile(context, "idx.db");
                    idxFd = new File(context.FilesDir, "idx.db");
                    resaveEntries = true;
                }
                FileInputStream idxBuf = new FileInputStream(idxFd);

                if (!new File(context.FilesDir, "entries.bin").Exists() || !new File(context.FilesDir, "parts.bin").Exists())
                {
                    resaveEntries = true;
                }

                entries = IntBuffer.Allocate(1649830);

                int index = 0;
                //System.Console.WriteLine("LoadDict STEP 1");

                if (idxBuf != null)
                {
                    int readLen, offset = 0, partLen = 200000;
                    byte[] dictPart = new byte[partLen];
                    int totalRead = 0;
                    int totalLen = (int)idxFd.Length();

                    while (totalRead < totalLen && (readLen = idxBuf.Read(dictPart, offset, dictPart.Length - offset)) > 0)
                    {
                        //System.Console.WriteLine("LoadDict \ntotalRead = " + totalRead + "\ntotalLen = " + totalLen + "\nreadLen = " + readLen + "\nidxBuf.Read = " + idxBuf.Read(dictPart, offset, dictPart.Length - offset));

                        totalRead += readLen;
                        int j = offset + readLen - 1;

                        byte[] newDictPart = null;

                        if (readLen == partLen - offset)
                        {
                            //System.Console.WriteLine("LoadDict STEP 4.1 " + dictPart[j] + " :: j => " + j);

                            while (dictPart[j] > 0)
                            {
                                //System.Console.WriteLine("j = " + j + "\ndictPart[j] = " + dictPart[j]);

                                j--;
                            }
                            //System.Console.WriteLine("LoadDict STEP 4.2");

                            while (dictPart[j] < 0)
                            {
                                System.Console.WriteLine("j = " + j);

                                j--;
                            }
                            //System.Console.WriteLine("LoadDict STEP 4.3");

                            offset = partLen - j - 1;
                            //System.Console.WriteLine("LoadDict STEP 4.4");

                            newDictPart = new byte[Math.Min(totalLen - totalRead + offset, partLen)];
                            //System.Console.WriteLine("LoadDict STEP 4.5");

                            Java.Lang.JavaSystem.Arraycopy(dictPart, j + 1, newDictPart, 0, offset);
                            //Array.Copy(dictPart, j + 1, newDictPart, 0, offset);
                        }
                        else
                        {
                            offset = 0;
                        }
                        //System.Console.WriteLine("LoadDict STEP 5");

                        if (resaveEntries)
                        {
                            dictIndexes.Add(index);
                            //System.Console.WriteLine("LoadDict STEP 6");

                            int i = 0;
                            while (i <= j)
                            {
                                entries.Put(index++, i);

                                while (i <= j && dictPart[i] < 0)
                                {
                                    i++;
                                }
                                while (i <= j && dictPart[i] >= 0)
                                {
                                    i++;
                                }
                            }
                        }
                        //System.Console.WriteLine("LoadDict STEP 7");

                        dictParts.Add(dictPart);
                        dictPart = newDictPart;
                        //System.Console.WriteLine("LoadDict STEP 8");

                    }
                    idxBuf.Close();
                }

                if (resaveEntries)
                {
                    //System.Console.WriteLine("LoadDict STEP 9");

                    DataOutputStream entriesOut = null, partsOut = null;
                    //System.Console.WriteLine("LoadDict STEP 10");

                    entriesOut = new DataOutputStream(context.OpenFileOutput("entries.bin", FileCreationMode.Private));
                    int count = entries.Capacity();
                    for (int i = 0; i < count; i++)
                    {
                        entriesOut.WriteInt(entries.Get(i));
                    }
                    //System.Console.WriteLine("LoadDict STEP 11");


                    partsOut = new DataOutputStream(context.OpenFileOutput("parts.bin", FileCreationMode.Private));
                    foreach (int i in dictIndexes)
                    {
                        partsOut.WriteInt(i);
                    }
                    //System.Console.WriteLine("LoadDict STEP 12");

                    if (entriesOut != null)
                    {
                        entriesOut.Flush();
                        entriesOut.Close();
                    }
                    if (partsOut != null)
                    {
                        partsOut.Flush();
                        partsOut.Close();
                    }
                }
                else
                {
                    //System.Console.WriteLine("LoadDict NOW RESAVING ENTRIES");

                    string documentpath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    Java.IO.File sdpath = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads);

                    File entriesDB = new File(documentpath, "entries.bin");
                    File partsDB = new File(documentpath, "parts.bin");

                    FileInputStream entriesIn = null, partsIn = null;

                    //entriesIn = context.OpenFileInput("entries.bin");
                    entriesIn = new FileInputStream(entriesDB);
                    //entriesIn = new FileInputStream(new File("entries.bin"));
                    FileChannel file = entriesIn.Channel;
                    ByteBuffer bb = ByteBuffer.Allocate(4 * 1649830);
                    file.Read(bb);
                    bb.Rewind();
                    entries = bb.AsIntBuffer();
                    file.Close();

                    partsIn = new FileInputStream(partsDB);
                    //partsIn = new FileInputStream(new File("parts.bin"));
                    //partsIn = (context.OpenFileInput("parts.bin");
                    file = partsIn.Channel;
                    bb = ByteBuffer.Allocate((int)file.Size());
                    file.Read(bb);
                    bb.Rewind();
                    IntBuffer ib = bb.AsIntBuffer();
                    int count = ib.Capacity();
                    //System.Console.WriteLine("LoadDict STEP 99 " + count);

                    for (int i = 0; i < count; i++)
                    {
                        dictIndexes.Add(ib.Get(i));
                    }
                    file.Close();

                    if (entriesIn != null)
                    {
                        entriesIn.Close();
                    }
                    if (partsIn != null)
                    {
                        partsIn.Close();
                    }
                }

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dict LoadDict ERROR => " +  e.Message);

                Log.Equals("chinesreader", e.Message);
            }

            byteBuffer = new byte[1090];

            sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
        }

        private static void CopyFile(Context context, string fileName)
        {
            try
            {
                System.IO.Stream fos = context.OpenFileOutput(fileName, FileCreationMode.Private);
                System.IO.Stream fis = context.Assets.Open(fileName);

                AnnotationActivity.CopyFile(fis, fos);

                fos.Flush();
                fos.Close();
                fis.Close();

                System.Console.WriteLine(fileName + " successfuly copied!!!!!");

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dict CopyFile ERROR => " + e.Message);
            }
        }

        public static byte[] GetDictPart(int entry)
        {
            //System.Console.WriteLine("GetDictPart STEP 1, ENTRY = " + entry);

            int part = Collections.BinarySearch(dictIndexes, entry);
            //int part = dictIndexes.BinarySearch(entry);

            //System.Console.WriteLine("GetDictPart STEP 2, part = " + part);

            if (part < 0)
            {
                part = -part - 2;
            }
            //System.Console.WriteLine("GetDictPart STEP 3, return = " + dictParts.Count);

            return dictParts[part];
        }

        public static int Compare(int entry, string another, bool broad)
        {
            int i = entries.Get(entry), j = 0, len = another.Length;

            byte[] dict = GetDictPart(entry);

            char c1 = (char)(((dict[i] & 0x0F) << 12) | ((dict[i + 1] & 0x3F) << 6) | (dict[i + 2] & 0x3F));
            char c2 = (char)0;

            while (j < len && dict[i] < 0)
            {
                c2 = another.ToCharArray()[j];
                if (c1 == c2)
                {
                    i += 3;
                    c1 = (char)(((dict[i] & 0x0F) << 12) | ((dict[i + 1] & 0x3F) << 6) | (dict[i + 2] & 0x3F));
                    j++;
                }
                else
                {
                    break;
                }
            }

            if (dict[i] >= 0)
            {
                if (j == len)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if (j == len)
                {
                    if (broad)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else
                {
                    if (c1 > c2)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
        }

        public static string GetCh(int entry, string charType)
        {
            try
            {
                byte[] dict = GetDictPart(entry);

                if (charType.Equals("original"))
                {
                    int i = entries.Get(entry);
                    while (dict[i++] < 0);
                    //return StringHelperClass.NewString(dict, entries.Get(entry), i - entries.Get(entry) - 1, "UTF-8");
                    return Encoding.UTF8.GetString(dict, entries.Get(entry), i - entries.Get(entry) - 1);
                }
                else if (charType.Equals("simplified"))
                {
                    int i = entries.Get(entry);

                    while (dict[i] < 0)
                    {
                        i++;
                    }

                    if (dict[i++] == 0)
                    {
                        int index = 0, mult = 1;
                        while (i < dict.Length && dict[i] >= 0)
                        {
                            index += mult * dict[i++];
                            mult *= 128;
                        }
                        i = entries.Get(index);
                        dict = GetDictPart(index);
                        while (dict[i++] < 0) ;

                        //return StringHelperClass.NewString(dict, entries.Get(index), i - entries.Get(index) - 1, "UTF-8");
                        return Encoding.UTF8.GetString(dict, entries.Get(index), i - entries.Get(index) - 1);
                    }

                    //return StringHelperClass.NewString(dict, entries.Get(entry), i - entries.Get(entry) - 1, "UTF-8");
                    return Encoding.UTF8.GetString(dict, entries.Get(entry), i - entries.Get(entry) - 1);
                }
                else if (charType.Equals("traditional"))
                {
                    int i = entries.Get(entry);

                    while (dict[i] < 0)
                    {
                        i++;
                    }

                    if (dict[i++] == 0)
                    {
                        //return StringHelperClass.NewString(dict, entries.Get(entry), i - entries.Get(entry), "UTF-8");
                        return Encoding.UTF8.GetString(dict, entries.Get(entry), i - entries.Get(entry));
                    }
                    else
                    {
                        while (dict[i++] != 0) ;
                        int index = 0, mult = 1;
                        while (i < dict.Length && dict[i] >= 0)
                        {
                            index += mult * dict[i++];
                            mult *= 128;
                        }

                        try
                        {
                            dictFile.Seek(index);
                            sbyte ch;
                            int j = 0;
                            while ((ch = dictFile.ReadByte()) < 0)
                            {
                                byteBuffer[j++] = (byte)ch;
                            }
                            //return StringHelperClass.NewString(byteBuffer, 0, j, "UTF-8");
                            return Encoding.UTF8.GetString(byteBuffer, 0, j);
                        }
                        catch (Exception e)
                        {
                            System.Console.WriteLine("Dict GetCh ERROR => " + e.Message);
                        }

                    }
                }

                return "";
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dict GetCh ERROR => " + e.Message);

                return "";
            }
        }

        public static string GetCh(int entry)
        {
            string charType = sharedPrefs.GetString("pref_charType", "original");

            return GetCh(entry, charType);
        }

        public static bool Equals(int entry, string str)
        {
            int len = str.Length;
            bool found = true;
            byte[] dict = GetDictPart(entry);

            for (int i = entries.Get(entry), j = 0; dict[i] < 0; i += 3, j++)
            {
                if (j > len || str[j] != (char)(((dict[i] & 0x0F) << 12) | ((dict[i + 1] & 0x3F) << 6) | (dict[i + 2] & 0x3F)))
                {
                    found = false;
                    break;
                }
            }

            return found;
        }

        public static int GetLength(int entry)
        {
            int i = entries.Get(entry);
            byte[] dict = GetDictPart(entry);
            while (dict[i++] < 0) ;
            return (i - entries.Get(entry) - 1) / 3;
        }

        public static string GetPinyin(int entry)
        {
            int i = entries.Get(entry);
            byte[] dict = GetDictPart(entry);

            while (dict[i] < 0)
            {
                i++;
            }

            if (dict[i] == 0)
            {
                i++;
                int index = 0, mult = 1;
                while (i < dict.Length && dict[i] >= 0)
                {
                    index += mult * dict[i++];
                    mult *= 128;
                }
                i = entries.Get(index);
                dict = GetDictPart(index);
                while (dict[i] < 0)
                {
                    i++;
                }
            }

            int j = i;
            while (dict[j] != 0)
            {
                j++;
            }

            try
            {
                //return StringHelperClass.NewString(dict, i, j - i, Encoding.ASCII.ToString());
                return Encoding.ASCII.GetString(dict, i, j - i);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("ERROR GetPinyin => " + e.Message);
                return "";
            }
        }

        public static string GetEnglish(int entry)
        {
            int index = 0, mult = 1;

            int i = entries.Get(entry);
            byte[] dict = GetDictPart(entry);

            while (dict[i] < 0)
            {
                i++;
            }

            if (dict[i++] == 0)
            { //traditional link
                index = 0;
                mult = 1;
                while (i < dict.Length && dict[i] >= 0)
                {
                    index += mult * dict[i++];
                    mult *= 128;
                }
                i = entries.Get(index);
                dict = GetDictPart(index);
                while (dict[i] < 0)
                {
                    i++;
                }
            }

            while (dict[i++] != 0) ;

            index = 0;
            mult = 1;
            while (i < dict.Length && dict[i] >= 0)
            {
                index += mult * dict[i++];
                mult *= 128;
            }

            try
            {
                dictFile.Seek(index);
                sbyte ch;
                int j = 0;
                while ((ch = dictFile.ReadByte()) < 0) ;

                byteBuffer[j++] = (byte)ch;
                while ((ch = dictFile.ReadByte()) != '\n')
                {
                    byteBuffer[j++] = (byte)ch;
                }
                //return StringHelperClass.NewString(byteBuffer, 0, j, "UTF-8");
                return Encoding.UTF8.GetString(byteBuffer, 0, j);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dict GetEnglish ERROR => " + e.Message);
            }

            return "";
        }

        public static int BinarySearch(string key, int start, int end, bool broad)
        {
            int lo = start;
            int hi = end;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                int res = Compare(mid, key, broad);
                if (res > 0)
                {
                    hi = mid - 1;
                }
                else if (res < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }

        public static int BinarySearch(string key, bool broad)
        {
            return BinarySearch(key, 0, entries.Capacity() - 1, broad);
        }


        public static string PinyinToTones(string py)
        {
            string pinyinType = sharedPrefs.GetString("pref_pinyinType", "marks");
            if (pinyinType.Equals("numbers"))
            {
                return py;
            }

            // THIS USES A CUSTOM SPLIT
            //string[] parts = py.Split("(?<=[1-5])", true);
            string[] parts = Regex.Split(py, @"(?<=[1-5])");
            int len = parts.Length;
            StringBuilder pinyin = new StringBuilder();
            for (int j = 0; j < len; j++)
            {
                pinyin.Append(ConvertToneNumber2ToneMark(parts[j].Trim()));
            }

            return pinyin.ToString();
        }

        private static string ConvertToneNumber2ToneMark(string pinyinStr)
        {

            const char defaultCharValue = '$';
            const int defaultIndexValue = -1;

            char unmarkedVowel = defaultCharValue;
            int indexOfUnmarkedVowel = defaultIndexValue;

            const char charA = 'a';
            const char charE = 'e';
            const string ouStr = "ou";
            const string allUnmarkedVowelStr = "aeiouv";
            const string allMarkedVowelStr = @"\u0101\u00E1\u0103\u00E0a\u0113\u00E9\u0115\u00E8e\u012B\u00ED\u012D\u00ECi\u014D\u00F3\u014F\u00F2o\u016B\u00FA\u016D\u00F9u\u01D6\u01D8\u01DA\u01DC\u00FC";

            //if (pinyinStr.matches("[a-z]*[1-5]")) {

            int tuneNumber = Java.Lang.Character.GetNumericValue(pinyinStr[pinyinStr.Length - 1]);

            if (tuneNumber == 5)
            {
                return pinyinStr.SubstringSpecial(0, pinyinStr.Length - 1);
            }
            else if (tuneNumber <= 0 || tuneNumber > 4)
            {
                return pinyinStr.Replace("v", "ü");
            }

            int indexOfA = pinyinStr.IndexOf(charA);
            int indexOfE = pinyinStr.IndexOf(charE);
            int ouIndex = pinyinStr.IndexOf(ouStr);

            if (-1 != indexOfA)
            {
                indexOfUnmarkedVowel = indexOfA;
                unmarkedVowel = charA;
            }
            else if (-1 != indexOfE)
            {
                indexOfUnmarkedVowel = indexOfE;
                unmarkedVowel = charE;
            }
            else if (-1 != ouIndex)
            {
                indexOfUnmarkedVowel = ouIndex;
                unmarkedVowel = ouStr[0];
            }
            else
            {
                for (int i = pinyinStr.Length - 1; i >= 0; i--)
                {
                    Regex rx = new Regex(@"[" + allUnmarkedVowelStr + @"]");
                    if (rx.IsMatch(pinyinStr[i].ToString()))
                    {
                        indexOfUnmarkedVowel = i;
                        unmarkedVowel = pinyinStr[i];
                        break;
                    }
                }
            }

            if ((defaultCharValue != unmarkedVowel) && (defaultIndexValue != indexOfUnmarkedVowel))
            {
                int rowIndex = allUnmarkedVowelStr.IndexOf(unmarkedVowel);
                int columnIndex = tuneNumber - 1;

                int vowelLocation = rowIndex * 5 + columnIndex;

                char markedVowel = allMarkedVowelStr[vowelLocation];

                StringBuilder resultBuffer = new StringBuilder();
                resultBuffer.Append(pinyinStr.SubstringSpecial(0, indexOfUnmarkedVowel).Replace(@"v", @"ü"));
                resultBuffer.Append(markedVowel);
                resultBuffer.Append(pinyinStr.SubstringSpecial(indexOfUnmarkedVowel + 1, pinyinStr.Length - 1).Replace("v", "ü"));

                return resultBuffer.ToString();

            }
            else
            {
                // error happens in the procedure of locating vowel
                return pinyinStr;
            }
        }
    }
}