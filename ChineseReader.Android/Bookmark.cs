using System;
using System.Collections;
using System.Collections.Generic;

namespace ChineseReader.Android
{
    public class Bookmark
    {
        public long mPosition;
        public string mTitle;
        public int mLine;
        public int mWord;

        public Bookmark(long position, string title)
        {
            mPosition = position;
            mTitle = title;
        }

        public virtual void SetAnnotatedPosition(int line, int word)
        {
            mLine = line;
            mWord = word;
        }

        public static List<Bookmark> ReadFromFile(string filePath)
        {
            List<Bookmark> bookmarks = new List<Bookmark>();

            System.IO.StreamReader br = null;
            try
            {
                br = new System.IO.StreamReader(filePath);
                bookmarks.Clear();
                string line;
                while ((line = br.ReadLine()) != null)
                {
                    int bmPos = int.Parse(line);
                    string bmTitle = br.ReadLine();
                    br.ReadLine();
                    bookmarks.Add(new Bookmark(bmPos, bmTitle));
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Bookmarks ReadFromFIle ERROR => " + e.Message);
            }

            return bookmarks;
        }

        public static bool SaveToFile(List<Bookmark> bookmarks, string filePath)
        {
            System.IO.StreamWriter bw;
            try
            {
                bw = new System.IO.StreamWriter(filePath);
                foreach (Bookmark bm in bookmarks)
                {
                    bw.BaseStream.WriteByte(Convert.ToByte(Convert.ToString(bm.mPosition)));
                    bw.BaseStream.WriteByte(Convert.ToByte(@"\n"));
                    bw.BaseStream.WriteByte(Convert.ToByte(bm.mTitle));
                    bw.BaseStream.WriteByte(Convert.ToByte(@"\n"));
                    bw.BaseStream.WriteByte(Convert.ToByte(@"\n"));
                }
                bw.Flush();
                bw.Close();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Bookmarks SaveToFile ERROR => " + e.Message);

                return false;
            }

            return true;
        }

        public static Bookmark Search(long position, List<Bookmark> bookmarks)
        {
            if (bookmarks == null)
            {
                return null;
            }

            int lo = 0;
            int hi = bookmarks.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                long res = ((Bookmark)bookmarks[mid]).mPosition;
                if (res > position)
                {
                    hi = mid - 1;
                }
                else if (res < position)
                {
                    lo = mid + 1;
                }
                else
                {
                    return (Bookmark)bookmarks[mid];
                }
            }
            return null;
        }

        public static int SearchClosest(long position, List<Bookmark> bookmarks)
        {
            if (bookmarks == null)
            {
                return -1;
            }

            if (bookmarks.Count == 0)
            {
                return 0;
            }

            int lo = 0;
            int hi = bookmarks.Count - 1;
            int mid = 0;
            while (lo <= hi)
            {
                mid = lo + (hi - lo) / 2;
                long res = ((Bookmark)bookmarks[mid]).mPosition;
                if (res > position)
                {
                    hi = mid - 1;
                }
                else if (res < position)
                {
                    lo = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            //if (((Bookmark)bookmarks.get(lo)).mPosition < position)
            //  lo++;

            return lo;
        }

        public static Bookmark SearchAnnotated(int line, int word, List<Bookmark> bookmarks)
        {
            int lo = 0;
            int hi = bookmarks.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                Bookmark res = (Bookmark)bookmarks[mid];
                if (res.mLine > line || res.mLine == line && res.mWord > word)
                {
                    hi = mid - 1;
                }
                else if (res.mLine < line || res.mLine == line && res.mWord < word)
                {
                    lo = mid + 1;
                }
                else
                {
                    return res;
                }
            }
            return null;
        }
    }
}