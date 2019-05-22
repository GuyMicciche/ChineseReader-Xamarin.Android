using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Lang;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChineseReader.Android
{
    public class AnnTask : AsyncTask<Java.Lang.Void, Java.Lang.Void, int>
    {
        public const int TASK_ANNOTATE = 0, TASK_ANNOTATE_BACK = 2, TASK_SPLIT = 4;
        public List<List<object>> lines, tempLines = new List<List<object>>(), tempBackLines = new List<List<object>>();
        public long textLen, startPos = -1, endPos = 0, tempStartPos, tempEndPos, curFilePos;
        private int task, splitLineIndex, curPos, bufLen, curWidth, notFound, annoMode, hMargin;
        public static int screenWidth, screenHeight, visibleLines, perLine;
        public Status status = Status.Finished;
        private string PastedText, BufText;
        private List<object> curLine;
        private Context Activity;
        private RandomAccessFile OpenedFile;
        private static LineView TestView;
        private IAnnTask inter;
        private static bool Formatting;
        private bool mHopelessBreak;
        private List<Bookmark> Bookmarks, FoundBookmarks;

        private TaskScheduler Scheduler { get; set; }
        private CancellationTokenSource CancelToken { get; set; }
        private Task<object> ProgressTask { get; set; }

        public interface IAnnTask
        {
            void OnCompleted(int task, int splitLineIndex, string pastedText, List<List<object>> tempLines, int curPos, long tempStartPos, long tempEndPos, bool isRemaining, List<Bookmark> foundBookmarks);
        }

        public AnnTask(Context activity, int task, int annoMode, int curPos, long startPos, long endPos, int curWidth, int splitLineIndex, List<List<object>> lines, List<object> curLine, string pastedText, long textLen, RandomAccessFile openedFile, LineView testView, IAnnTask inter, bool formatting, List<Bookmark> bookmarks) : base()
        {
            this.status = Status.Running;

            this.Activity = activity;
            this.task = task;
            this.annoMode = annoMode;
            this.curPos = curPos;
            this.startPos = startPos;
            this.endPos = endPos;
            this.curWidth = curWidth;
            this.splitLineIndex = splitLineIndex;
            this.lines = lines;
            this.curLine = curLine;
            this.PastedText = pastedText;
            this.textLen = textLen;
            this.OpenedFile = openedFile;
            TestView = testView;
            this.inter = inter;
            Formatting = formatting;
            this.Bookmarks = bookmarks;
            this.FoundBookmarks = new List<Bookmark>();
            curFilePos = -1;

            Scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            UpdateVars(activity);
        }

        protected override int RunInBackground(params Java.Lang.Void[] @params)
        {
            throw new System.NotImplementedException();
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] native_parms)
        {
            try
            {
                switch (task)
                {
                    case TASK_SPLIT:
                        curPos = 0;
                        BufText = "";
                        bufLen = 0;
                        notFound = 0;
                        break;

                    case TASK_ANNOTATE:
                    case TASK_ANNOTATE_BACK:
                        curPos = 0;
                        BufText = "";
                        bufLen = 0;
                        curLine = new List<object>();
                        hMargin = TestView != null ? TestView.hMargin : 0;
                        curWidth = hMargin;
                        notFound = 0;
                        break;
                }

                tempEndPos = endPos;
                tempStartPos = startPos;
                mHopelessBreak = false;

                while ((task == TASK_ANNOTATE || task == TASK_SPLIT) && (curPos < bufLen || curPos == bufLen && tempEndPos < textLen) && (tempLines.Count < visibleLines * 2 || (!Formatting && tempEndPos - endPos < 500)) || task == TASK_ANNOTATE_BACK && (curPos < bufLen || curPos == bufLen && tempStartPos > 0))
                {
                    if ((task == TASK_ANNOTATE || task == TASK_SPLIT) && bufLen - curPos < 18 && tempEndPos < textLen)
                    {
                        if (notFound > 0)
                        {
                            curLine = AddNotFound(notFound, curLine);
                            notFound = 0;
                        }
                        BufText = NextBuffer;
                        bufLen = BufText.Length;
                    }
                    else if (task == TASK_ANNOTATE_BACK && curPos == bufLen)
                    {
                        if (notFound > 0)
                        {
                            curLine = AddNotFound(notFound, curLine);
                            notFound = 0;
                        }
                        if (curLine.Count > 0)
                        {
                            tempLines.Add(curLine);
                            curLine = new List<object>();
                        }
                        tempBackLines.InsertRange(0, tempLines);
                        tempLines.Clear();
                        if (tempBackLines.Count < visibleLines * 2)
                        {
                            BufText = PrevBuffer;
                            bufLen = BufText.Length;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (BufText[curPos] < '\u25CB' || BufText[curPos] > '\u9FA5')
                    {
                        if (CheckCancelled())
                        {
                            //return (-1);
                            return (null);
                        }

                        notFound++;

                        if (BufText[curPos] == ' ' && notFound > 1)
                        {
                            curPos++;
                            curLine = AddNotFound(notFound, curLine);
                            notFound = 0;

                            if (curFilePos != -1)
                            {
                                curFilePos++;
                            }

                            continue;
                        }

                        if (notFound > perLine * visibleLines * 2 && task == TASK_ANNOTATE)
                        {
                            notFound--;
                            break;
                        }

                        if (curFilePos != -1)
                        {
                            curFilePos += Encoding.UTF8.GetBytes(BufText.SubstringSpecial(curPos, curPos + 1)).Length;
                        }
                        curPos++;
                        continue;
                    }

                    if (notFound > 0)
                    {
                        curLine = AddNotFound(notFound, curLine);
                        notFound = 0;
                    }

                    int last = -1;

                    int i = 3;
                    for (; i >= 0; i--)
                    {
                        int j = 1;
                        for (; j <= i && curPos + j < bufLen; j++)
                        {
                            if (BufText[curPos + j] < '\u25CB' || BufText[curPos + j] > '\u9FA5')
                            {
                                break;
                            }
                        }

                        if (j == i + 1)
                        {
                            if (i == 3)
                            {
                                last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos + i + 1), true);
                            }
                            else
                            {
                                if (last >= 0)
                                {
                                    last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos + i + 1), 0, last - 1, false);
                                }
                                else
                                {
                                    last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos + i + 1), false);
                                }
                            }

                            if (last >= 0)
                            {
                                if (i == 3)
                                { //the found entry may be longer than 4 (3 + 1)
                                    if (Dict.GetLength(last) > bufLen - curPos)
                                    { //the found may be longer than the ending
                                        continue;
                                    }
                                    string word = BufText.SubstringSpecial(curPos, curPos + Dict.GetLength(last));
                                    if (Dict.Equals(last, word))
                                    {
                                        curLine = AddWord(last, curLine);

                                        Bookmark bookmark = Bookmark.Search(curFilePos, Bookmarks);
                                        if (bookmark != null)
                                        {
                                            bookmark.SetAnnotatedPosition(tempLines.Count, curLine.Count - 1);
                                            FoundBookmarks.Add(bookmark);
                                        }

                                        if (CheckCancelled())
                                        {
                                            return (null);
                                            //return (-1);
                                        }

                                        if (curFilePos != -1)
                                        {
                                            curFilePos += word.Length * 3;
                                        }
                                        curPos += word.Length;
                                        break;
                                    }
                                }
                                else
                                {
                                    curLine = AddWord(last, curLine);

                                    Bookmark bookmark = Bookmark.Search(curFilePos, Bookmarks);
                                    if (bookmark != null)
                                    {
                                        bookmark.SetAnnotatedPosition(tempLines.Count, curLine.Count - 1);
                                        FoundBookmarks.Add(bookmark);
                                    }

                                    if (CheckCancelled())
                                    {
                                        //return (-1);
                                        return (null);
                                    }

                                    if (curFilePos != -1)
                                    {
                                        curFilePos += (i + 1) * 3;
                                    }
                                    curPos += i + 1;

                                    break;
                                }
                            }
                        }
                    }

                    if (i < 0)
                    {
                        notFound++;
                        if (curFilePos != -1)
                        {
                            curFilePos += Encoding.UTF8.GetBytes(BufText.SubstringSpecial(curPos, curPos + 1)).Length;
                        }
                        curPos++;
                    }
                }

                if (notFound > 0)
                {
                    curLine = AddNotFound(notFound, curLine);
                    notFound = 0;
                }

                if (curLine.Count > 0)
                {
                    if (task == TASK_ANNOTATE_BACK || tempEndPos == textLen && curPos == bufLen || tempLines.Count == 0)
                    { //back annotation or end of text or 1-line text
                        tempLines.Add(curLine);
                        curLine = new List<object>();
                    }
                    else
                    {
                        curPos -= (int)LineView.GetLineSize(curLine, false);
                    }
                }

                if (task == TASK_ANNOTATE || task == TASK_SPLIT)
                {
                    if (annoMode == AnnotationActivity.ANNOTATE_FILE)
                    {
                        tempEndPos -= Encoding.UTF8.GetBytes(BufText.Substring(curPos)).Length;
                    }
                    else
                    {
                        tempEndPos -= bufLen - curPos;
                    }
                }

                if (task == TASK_ANNOTATE_BACK)
                {
                    tempBackLines.InsertRange(0, tempLines);
                    tempLines = tempBackLines;
                }

                return (task);
            }
            catch (Java.Lang.Exception e)
            {
                status = Status.Finished;
                System.Console.WriteLine("Annotation DoInBackground ERROR => " + e.Message);
                Log.Equals("ChineseReader", "Annotation error");
            }

            return task;
        }

        protected override void OnProgressUpdate(Java.Lang.Void[] unused)
        {

        }

        protected override void OnPostExecute(int task)
        {
            System.Console.WriteLine("POST EXECUTE => " + task);

            status = Status.Finished;

            if (CheckCancelled())
            {
                return;
            }

            inter.OnCompleted(task, splitLineIndex, PastedText, tempLines, curPos, tempStartPos, tempEndPos, curPos < bufLen || (annoMode == AnnotationActivity.ANNOTATE_FILE && tempEndPos + Encoding.UTF8.GetBytes(BufText.SubstringSpecial(0, curPos)).Length < textLen), FoundBookmarks);
        }

        public string NextBuffer
        {
            get
            {
                if (annoMode == AnnotationActivity.ANNOTATE_FILE)
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        tempEndPos -= Encoding.UTF8.GetBytes(BufText.Substring(curPos)).Length;
                        OpenedFile.Seek(tempEndPos);
                        int readCount = OpenedFile.Read(buffer);

                        int i = 0;
                        while ((buffer[i] & 0x000000FF) >= 0x80 && (buffer[i] & 0x000000FF) < 0xC0 && i < readCount)
                        {
                            if (tempStartPos == tempEndPos + i) //make sure it's only adjusted for the starting condition
                            {
                                tempStartPos++;
                            }
                            i++;
                        }

                        if (tempStartPos == tempEndPos + 1 && tempStartPos > 0)
                        {
                            if (buffer[i] == System.Convert.ToChar(@"\n"))
                            {
                                tempStartPos++;
                                i++;
                            }
                            else if (buffer[i] == System.Convert.ToChar(@"\r") && buffer[i + 1] == System.Convert.ToChar(@"\n"))
                            {
                                tempStartPos += 2;
                                i += 2;
                            }
                        }

                        if (tempEndPos + readCount < textLen)
                        {
                            while ((buffer[readCount - 1] & 0x000000FF) >= 0x80 && (buffer[readCount - 1] & 0x000000FF) < 0xC0)
                            {
                                readCount--;
                            }
                            if ((buffer[readCount - 1] & 0x000000FF) >= 0xC0)
                            {
                                readCount--;
                            }
                        }

                        int bookmarkStart = Bookmark.SearchClosest(tempEndPos, Bookmarks);
                        if (bookmarkStart != -1 && Bookmarks.Count > bookmarkStart && Bookmarks[bookmarkStart].mPosition < tempEndPos + readCount)
                        {
                            curFilePos = tempEndPos;
                        }
                        else
                        {
                            curFilePos = -1;
                        }

                        curPos = 0;
                        tempEndPos += readCount;

                        //return StringHelperClass.NewString(buffer, i, readCount - i, "UTF-8");
                        return Encoding.UTF8.GetString(buffer, i, readCount - i);
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(Activity, e.Message, ToastLength.Long).Show();
                        return null;
                    }
                }
                else
                { //ANNOTATE_BUFFER
                    tempEndPos -= bufLen - curPos;
                    long length = Math.Min(PastedText.Length - (int)tempEndPos, 300);
                    curPos = 0;
                    tempEndPos += length;
                    return PastedText.SubstringSpecial((int)(tempEndPos - length), (int)tempEndPos);
                }
            }
        }

        public string PrevBuffer
        {
            get
            {
                if (annoMode == AnnotationActivity.ANNOTATE_FILE)
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        int readCount = 0, i = 0, j = 0;

                        long toRead = Math.Min(tempStartPos, buffer.Length); //+2 - in case the character at startPos is broken
                        OpenedFile.Seek(tempStartPos - toRead);
                        readCount = OpenedFile.Read(buffer, 0, (int)toRead);
                        while ((buffer[i] & 0x000000FF) >= 0x80 && (buffer[i] & 0x000000FF) < 0xC0 && i < readCount)
                        {
                            i++;
                        }

                        if (tempStartPos > readCount)
                        { //not at the beginning of file
                            j = i;
                            while (buffer[j] != System.Convert.ToChar(@"\n") && j < readCount)
                            {
                                j++;
                            }

                            if (j < readCount - 1)
                            {
                                i = j + 1;
                            }
                        }

                        //string text = StringHelperClass.NewString(buffer, i, readCount - i, "UTF-8");
                        string text = Encoding.UTF8.GetString(buffer, i, readCount - i);
                        tempStartPos -= readCount - i;

                        curPos = 0;

                        int bookmarkStart = Bookmark.SearchClosest(tempStartPos, Bookmarks);
                        if (bookmarkStart != -1 && Bookmarks.Count > bookmarkStart && Bookmarks[bookmarkStart].mPosition < tempEndPos)
                        {
                            curFilePos = tempStartPos;
                        }
                        else
                        {
                            curFilePos = -1;
                        }

                        return text;
                    }
                    catch (Exception e)
                    {
                       System.Console.WriteLine("PrevBuffer ERROR => " + e.Message);

                        Toast.MakeText(Activity, e.Message, ToastLength.Long).Show();
                        return null;
                    }
                }
                else
                { //ANNOTATE_BUFFER
                    int start = (int)Math.Max(tempStartPos - 300, 0);
                    int end = (int)tempStartPos;

                    int j = start;
                    if (start > 0)
                    {
                        while (PastedText[j] != System.Convert.ToChar(@"\n") && j < end)
                        {
                            j++;
                        }

                        if (j < end - 1)
                        {
                            start = j + 1;
                        }
                    }

                    tempStartPos = start;
                    return PastedText.SubstringSpecial(start, end);
                }
            }
        }

        public string emptyString = "";

        public List<object> AddNotFound(int notFound, List<object> curLine)
        {
            string str = BufText.SubstringSpecial(curPos - notFound, curPos);
            int nIndex = 0, oldIndex = 0;
            while ((nIndex = str.IndexOf(@"\n", oldIndex)) > -1)
            {
                if (nIndex != oldIndex)
                {
                    string words = str.SubstringSpecial(oldIndex, nIndex);
                    curLine = AddWord(words, curLine);
                }
                oldIndex = nIndex + 1;

                curLine.Add(emptyString);
                tempLines.Add(curLine);
                curLine = new List<object>();

                curWidth = hMargin;
            }

            if (oldIndex < str.Length)
            {
                string words = str.Substring(oldIndex);
                curLine = AddWord(words, curLine);
            }

            return curLine;
        }

        public List<object> CheckPunctAdd(object word, int tvWidth, List<object> curLine)
        {
            string stickyPrev = "!~)]}\"':;,.?\uFF0C\u3002\uFF01\uFF1F\u2026\uFF09\uFF1B\uFF1A\u2019\u201D\u300B\u3011\u300F\u3001";
            const string stickyNext = "([{<\u300A\u201C\u2018\u3010\u300E\uFF08";

            if (curLine.Count == 0 && tempLines.Count > 0)
            {
                List<object> lastLine = tempLines[tempLines.Count - 1];

                int lastLineLen = lastLine.Count;
                if (lastLineLen == 0)
                {
                    curLine.Add(word);
                    curWidth = hMargin + tvWidth;
                    return curLine;
                }

                object last = lastLine[lastLineLen - 1];
                if (word is string && ((string)word).Length > 0 && stickyPrev.IndexOf(((string)word)[0]) > -1 && !mHopelessBreak)
                {
                    if (last is string)
                    {
                        int lastIndex = ((string)last).Length - 1;
                        char c;
                        while (lastIndex >= 0 && ((c = ((string)last)[lastIndex]) < 'a' || c > 'Z'))
                        {
                            lastIndex--;
                        }
                        while (lastIndex >= 0 && (c = ((string)last)[lastIndex]) >= 'a' && c <= 'Z')
                        {
                            lastIndex--;
                        }

                        string strMove = ((string)last).Substring(lastIndex + 1);
                        string rest = ((string)last).SubstringSpecial(0, lastIndex + 1);
                        if (lastLineLen > 1 || rest.Length > 0)
                        { //make sure no empty line is left
                            lastLine.Remove(lastLine.Count - 1);
                            curWidth = hMargin + TestView.GetWordWidth(strMove) + TestView.Margins;
                            if (rest.Length > 0)
                            {
                                lastLine.Add(rest);
                                curLine.Add(strMove);
                            }
                            else
                            {
                                curLine = CheckPunctAdd(strMove, TestView.GetWordWidth(strMove) + TestView.Margins, curLine);
                            }
                        }
                        else
                        {
                            mHopelessBreak = true;
                        }
                    }
                    else
                    {
                        lastLine.Remove(lastLine.Count - 1);
                        curLine = CheckPunctAdd(last, TestView.GetWordWidth(last), curLine);
                        curWidth = hMargin + TestView.GetWordWidth(last);
                    }

                    curLine = AddWord(word, curLine);
                    return curLine;
                }
                else if (last is string && !mHopelessBreak)
                {
                    string strLast = (string)last;
                    int lastIndex = strLast.Length - 1;
                    if (lastIndex >= 0 && stickyNext.Contains(strLast.Substring(lastIndex)))
                    {
                        lastIndex -= 1;
                        while (lastIndex >= 0 && stickyNext.IndexOf(strLast[lastIndex]) > -1)
                        {
                            lastIndex--;
                        }

                        string strMove = ((string)last).Substring(lastIndex + 1);
                        string rest = ((string)last).SubstringSpecial(0, lastIndex + 1);
                        if (lastLineLen > 1 || rest.Length > 0)
                        { //make sure no empty line is left
                            lastLine.Remove(lastLine.Count - 1);

                            if (rest.Length > 0)
                            {
                                lastLine.Add(rest);
                            }

                            curLine.Add(strMove);
                            curWidth = hMargin + TestView.GetWordWidth(strMove) + TestView.Margins;

                            return AddWord(word, curLine);
                        }
                        else
                        {
                            curLine.Add(word);
                            curWidth += TestView.GetWordWidth(word);
                        }

                        return curLine;
                    }
                }
            }

            curLine.Add(word);
            curWidth += tvWidth;

            mHopelessBreak = false;

            return curLine;
        }

        public List<object> AddWord(object word, List<object> curLine)
        {
            int curLen = curLine.Count;
            if (curLen > 0)
            {
                if (word is string)
                {
                    object last = curLine[curLen - 1];
                    if (last is string)
                    {
                        int lastLen = ((string)last).Length;
                        if (lastLen > 0 && ((string)last)[lastLen - 1] != ' ')
                        {
                            word = ((string)last) + (string)word;
                            curLine.RemoveAt(curLen - 1);

                            if (Formatting)
                            {
                                curWidth -= TestView.GetWordWidth(last);
                            }
                        }
                    }
                }
            }

            if (!Formatting)
            {
                curLine.Add(word);
                return curLine;
            }

            while (true)
            {
                int tvWidth = TestView.GetWordWidth(word);

                if (curWidth + tvWidth > screenWidth)
                {
                    if (word is string)
                    {
                        string str = (string)word;
                        int sep = BreakWord(str);

                        List<object> newCurLine = curLine;

                        if (sep != 0)
                        {
                            newCurLine = CheckPunctAdd(str.Substring(0, sep), tvWidth, curLine);
                            word = str.Substring(sep);
                        }
                        else
                        {
                            if (curLine.Count == 0)
                            {
                                sep = BreakWordHard(str);
                                newCurLine = CheckPunctAdd(str.Substring(0, sep), tvWidth, curLine);
                                word = str.Substring(sep);
                                mHopelessBreak = true;
                            }

                            if (curLine.Count == 1)
                            {
                                mHopelessBreak = true;
                            }
                        }

                        if (newCurLine == curLine)
                        { //if new lines have been added in the process, don't add a new line
                            tempLines.Add(curLine);
                            curLine = new List<object>();
                            curWidth = hMargin;
                        }
                        else
                        {
                            curLine = newCurLine;
                        }

                    }
                    else
                    {
                        if (curLine.Count > 0)
                        {
                            tempLines.Add(curLine);
                            curLine = new List<object>();
                        }
                        curWidth = hMargin;
                        curLine = CheckPunctAdd(word, tvWidth, curLine);
                        break;
                    }
                }
                else
                {
                    if (!(word is string) || ((string)word).Length > 0)
                    {
                        curLine = CheckPunctAdd(word, tvWidth, curLine);
                    }
                    break;
                }
            }

            return curLine;
        }

        public int BreakWord(string str)
        {
            //string[] strs = str.Split("(?<=[\\p{Punct}\\s+\uFF0C\u3002\uFF01\uFF1F\u2026\uFF09\uFF1B\uFF1A\u2019\u201D\u300B\u3011\u300F\u3001\u300A\u201C\u2018\u3010\u300E\uFF08])", false);
            string[] strs = Regex.Split(str, @"(?<=[\p{P}\s+\uFF0C\u3002\uFF01\uFF1F\u2026\uFF09\uFF1B\uFF1A\u2019\u201D\u300B\u3011\u300F\u3001\u300A\u201C\u2018\u3010\u300E\uFF08])");
            int width = curWidth + TestView.Margins, i = 0, candLen = 0;

            while (width < screenWidth && i < strs.Length)
            {
                candLen += strs[i].Length;
                width = curWidth + TestView.Margins + TestView.GetWordWidth(str.Substring(0, candLen));
                i++;
            }

            if (width <= screenWidth)
            {
                return candLen;
            }
            else if (i > 1)
            {
                return candLen - strs[i - 1].Length;
            }
            else
            {
                return 0;
            }
        }

        public int BreakWordHard(string str)
        {
            int width = curWidth + TestView.Margins, i = 0;

            int lo = 0;
            int hi = str.Length - 1;
            int mid = 0, res = 0;
            while (lo < hi)
            {
                mid = (hi + lo) / 2;
                res = width + TestView.Margins + TestView.GetWordWidth(str.Substring(0, mid + 1));
                if (res > screenWidth)
                {
                    hi = mid - 1;
                }
                else if (res < screenWidth)
                {
                    lo = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            if (res <= screenWidth)
            {
                return mid + 1;
            }
            else
            {
                return Math.Max(1, mid);
            }
        }

        public void RedrawLines(RecyclerView listView)
        {
            int currentLine = Math.Max(((LinearLayoutManager)listView.GetLayoutManager()).FindFirstVisibleItemPosition() - 1, 0);
            int linesSize = lines.Count;

            for (int j = 0; j < currentLine && j < linesSize; j++)
            {
                List<object> line = lines[j];
                startPos += LineView.GetLineSize(line, annoMode == AnnotationActivity.ANNOTATE_FILE);
            }

            List<object> curLine = new List<object>();
            tempLines = new List<List<object>>();
            curWidth = hMargin;
            int linesCount = lines.Count;
            int i = 0;
            for (i = currentLine; i < linesCount && tempLines.Count < visibleLines * 2; i++)
            {
                List<object> line = lines[i];
                int wordCount = line.Count;
                for (int j = 0; j < wordCount; j++)
                {
                    object word = line[j];

                    if (word is string && ((string)word).Length == 0)
                    {
                        curLine.Add("");
                        tempLines.Add(curLine);
                        curLine = new List<object>();
                        curWidth = hMargin;
                    }
                    else
                    {
                        curLine = AddWord(word, curLine);
                    }
                }
            }

            if (curLine.Count > 0 && endPos >= textLen)
            {
                tempLines.Add(curLine);
            }
            else
            {
                endPos -= LineView.GetLineSize(curLine, annoMode == AnnotationActivity.ANNOTATE_FILE);
            }

            if (i < linesCount)
            {
                for (; i < linesCount; i++)
                {
                    endPos -= LineView.GetLineSize(lines[i], annoMode == AnnotationActivity.ANNOTATE_FILE);
                }
            }


            lines.Clear();
            lines.AddRange(tempLines);

            status = Status.Finished;
        }

        public static void UpdateVars(Context activity)
        {
            IWindowManager wm = activity.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            Display display = wm.DefaultDisplay;

            if (Formatting)
            {
                TestView.UpdateVars();
                if (Build.VERSION.SdkInt > BuildVersionCodes.Honeycomb)
                {
                    Point screen = new Point();
                    display.GetSize(screen);
                    perLine = (int)Math.Max(Math.Min(Math.Round((double)(screen.X / TestView.GetWordWidth("W"))), int.MaxValue / 2), 1);
                    visibleLines = (int)Math.Max(Math.Min(Math.Round(screen.Y / TestView.WordHeight), (int.MaxValue / perLine) / 2), 1);
                    screenWidth = screen.X;
                    screenHeight = screen.Y;
                }
                else
                {
                    perLine = (int)Math.Max(Math.Min(Math.Round((double)(display.Width / TestView.GetWordWidth("W"))), int.MaxValue / 2), 1);
                    visibleLines = (int)Math.Max(Math.Min(Math.Round(display.Height / TestView.WordHeight), (int.MaxValue / perLine) / 2), 1);
                    screenWidth = display.Width;
                    screenHeight = display.Height;
                }
            }
            else
            {
                visibleLines = 1;
                perLine = int.MaxValue / 2;
            }
        }

        public bool CheckCancelled()
        {
            // Android way
            //if (IsCancelled)
            // C# way
            if (CancelToken.IsCancellationRequested)
            {
                status = Status.Finished;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ExecuteWrapper()
        {
            // C# way
            if (ProgressTask == null || ProgressTask.Status == TaskStatus.RanToCompletion)
            {
                ProgressTask = CreateTask();
                ProgressTask.Start();
            }
            else
            {
                CancelToken.Cancel();
                ProgressTask.Wait();
                object val = ProgressTask.Result;
                ProgressTask = null;
            }

            // Android way
            //if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            //{
            //    ExecuteOnExecutor(AsyncTask.ThreadPoolExecutor);
            //}
            //else
            //{
            //    Execute();
            //}
        }

        private Task<object> CreateTask()
        {
            CancelToken = new CancellationTokenSource();
            var t = new Task<object>(() =>
            {
                try
                {
                    //System.Console.WriteLine("STEP 1");

                    switch (task)
                    {
                        case TASK_SPLIT:
                            curPos = 0;
                            BufText = "";
                            bufLen = 0;
                            notFound = 0;
                            break;

                        case TASK_ANNOTATE:
                        case TASK_ANNOTATE_BACK:
                            curPos = 0;
                            BufText = "";
                            bufLen = 0;
                            curLine = new List<object>();
                            hMargin = TestView != null ? TestView.hMargin : 0;
                            curWidth = hMargin;
                            notFound = 0;
                            break;
                    }

                    tempEndPos = endPos;
                    tempStartPos = startPos;
                    mHopelessBreak = false;
                    //System.Console.WriteLine("STEP 2");

                    while ((task == TASK_ANNOTATE || task == TASK_SPLIT) && (curPos < bufLen || curPos == bufLen && tempEndPos < textLen) && (tempLines.Count < visibleLines * 2 || (!Formatting && tempEndPos - endPos < 500)) || task == TASK_ANNOTATE_BACK && (curPos < bufLen || curPos == bufLen && tempStartPos > 0))
                    {
                        //System.Console.WriteLine("STEP 3");

                        if ((task == TASK_ANNOTATE || task == TASK_SPLIT) && bufLen - curPos < 18 && tempEndPos < textLen)
                        {
                            if (notFound > 0)
                            {
                                curLine = AddNotFound(notFound, curLine);
                                notFound = 0;
                            }
                            BufText = NextBuffer;
                            bufLen = BufText.Length;
                        }
                        else if (task == TASK_ANNOTATE_BACK && curPos == bufLen)
                        {
                            if (notFound > 0)
                            {
                                curLine = AddNotFound(notFound, curLine);
                                notFound = 0;
                            }
                            if (curLine.Count > 0)
                            {
                                tempLines.Add(curLine);
                                curLine = new List<object>();
                            }
                            tempBackLines.InsertRange(0, tempLines);
                            tempLines.Clear();
                            if (tempBackLines.Count < visibleLines * 2)
                            {
                                BufText = PrevBuffer;
                                bufLen = BufText.Length;
                            }
                            else
                            {
                                break;
                            }
                        }
                        //System.Console.WriteLine("STEP 4");

                        if (BufText[curPos] < '\u25CB' || BufText[curPos] > '\u9FA5')
                        {
                            if (CheckCancelled())
                            {
                                //return (-1);
                                return (null);
                            }

                            notFound++;

                            if (BufText[curPos] == ' ' && notFound > 1)
                            {
                                curPos++;
                                curLine = AddNotFound(notFound, curLine);
                                notFound = 0;

                                if (curFilePos != -1)
                                {
                                    curFilePos++;
                                }

                                continue;
                            }

                            if (notFound > perLine * visibleLines * 2 && task == TASK_ANNOTATE)
                            {
                                notFound--;
                                break;
                            }

                            if (curFilePos != -1)
                            {
                                curFilePos += Encoding.UTF8.GetBytes(BufText.SubstringSpecial(curPos, curPos + 1)).Length;
                            }
                            curPos++;
                            continue;
                        }
                        //System.Console.WriteLine("STEP 5");

                        if (notFound > 0)
                        {
                            curLine = AddNotFound(notFound, curLine);
                            notFound = 0;
                        }

                        int last = -1;
                        //System.Console.WriteLine("STEP 6");

                        int i = 3;
                        for (; i >= 0; i--)
                        {
                            int j = 1;
                            for (; j <= i && curPos + j < bufLen; j++)
                            {
                                if (BufText[curPos + j] < '\u25CB' || BufText[curPos + j] > '\u9FA5')
                                {
                                    break;
                                }
                            }
                            //System.Console.WriteLine("STEP 7");

                            if (j == i + 1)
                            {
                                if (i == 3)
                                {
                                    last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos + i + 1), true);
                                }
                                else
                                {
                                    if (last >= 0)
                                    {
                                        last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos + i + 1), 0, last - 1, false);
                                    }
                                    else
                                    {
                                        last = Dict.BinarySearch(BufText.SubstringSpecial(curPos, curPos +  i + 1), false);
                                    }
                                }
                                //System.Console.WriteLine("STEP 8");

                                if (last >= 0)
                                {
                                    if (i == 3)
                                    { //the found entry may be longer than 4 (3 + 1)
                                        if (Dict.GetLength(last) > bufLen - curPos)
                                        { //the found may be longer than the ending
                                            continue;
                                        }
                                        string word = BufText.SubstringSpecial(curPos, curPos + Dict.GetLength(last));
                                        if (Dict.Equals(last, word))
                                        {
                                            curLine = AddWord(last, curLine);

                                            Bookmark bookmark = Bookmark.Search(curFilePos, Bookmarks);
                                            if (bookmark != null)
                                            {
                                                bookmark.SetAnnotatedPosition(tempLines.Count, curLine.Count - 1);
                                                FoundBookmarks.Add(bookmark);
                                            }

                                            if (CheckCancelled())
                                            {
                                                return (null);
                                                //return (-1);
                                            }

                                            if (curFilePos != -1)
                                            {
                                                curFilePos += word.Length * 3;
                                            }
                                            curPos += word.Length;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        curLine = AddWord(last, curLine);

                                        Bookmark bookmark = Bookmark.Search(curFilePos, Bookmarks);
                                        if (bookmark != null)
                                        {
                                            bookmark.SetAnnotatedPosition(tempLines.Count, curLine.Count - 1);
                                            FoundBookmarks.Add(bookmark);
                                        }

                                        if (CheckCancelled())
                                        {
                                            //return (-1);
                                            return (null);
                                        }

                                        if (curFilePos != -1)
                                        {
                                            curFilePos += (i + 1) * 3;
                                        }
                                        curPos += i + 1;

                                        break;
                                    }
                                }
                            }
                        }
                        //System.Console.WriteLine("STEP 9");

                        if (i < 0)
                        {
                            notFound++;
                            if (curFilePos != -1)
                            {
                                curFilePos += Encoding.UTF8.GetBytes(BufText.SubstringSpecial(curPos, curPos + 1)).Length;
                            }
                            curPos++;
                        }
                    }
                    //System.Console.WriteLine("STEP 10");

                    if (notFound > 0)
                    {
                        curLine = AddNotFound(notFound, curLine);
                        notFound = 0;
                    }
                    //System.Console.WriteLine("STEP 11");

                    if (curLine.Count > 0)
                    {
                        if (task == TASK_ANNOTATE_BACK || tempEndPos == textLen && curPos == bufLen || tempLines.Count == 0)
                        { //back annotation or end of text or 1-line text
                            tempLines.Add(curLine);
                            curLine = new List<object>();
                        }
                        else
                        {
                            curPos -= (int)LineView.GetLineSize(curLine, false);
                        }
                    }
                    //System.Console.WriteLine("STEP 12");

                    if (task == TASK_ANNOTATE || task == TASK_SPLIT)
                    {
                        if (annoMode == AnnotationActivity.ANNOTATE_FILE)
                        {
                            tempEndPos -= Encoding.UTF8.GetBytes(BufText.Substring(curPos)).Length;
                        }
                        else
                        {
                            tempEndPos -= bufLen - curPos;
                        }
                    }

                    if (task == TASK_ANNOTATE_BACK)
                    {
                        tempBackLines.InsertRange(0, tempLines);
                        tempLines = tempBackLines;
                    }

                    return (task);
                }
                catch (Exception e)
                {
                    status = Status.Finished;
                    System.Console.WriteLine("Annotation CreateTask ERROR => " + e.Message);

                    Log.Equals("ChineseReader", "Annotation error");
                }

                return task;
            }, CancelToken.Token, TaskCreationOptions.None);

            t.ContinueWith(antecendent =>
            {
                status = Status.Finished;

                System.Console.WriteLine("C# Post Execute");

                if (CheckCancelled())
                {   
                    return;
                }

                inter.OnCompleted(task, splitLineIndex, PastedText, tempLines, curPos, tempStartPos, tempEndPos, curPos < bufLen || (annoMode == AnnotationActivity.ANNOTATE_FILE && tempEndPos + Encoding.UTF8.GetBytes(BufText.SubstringSpecial(0, curPos)).Length < textLen), FoundBookmarks);

            }, CancelToken.Token, TaskContinuationOptions.None, Scheduler);

            return t;
        }
    }
}