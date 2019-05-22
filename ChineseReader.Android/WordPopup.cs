using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Content;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    public class WordPopup
    {
        protected internal AnnotationActivity mContext;
        protected internal PopupWindow mWindow;
        protected internal View mRootView;
        public View parent;
        protected internal IWindowManager mWindowManager;
        private ImageView mArrowUp;
        private ImageView mArrowDown;
        private TextView mChars, mContent, mBookmark;
        private LinearLayout mBubble;
        public int screenX, screenY, showX;
        public float scale;
        public List<object> line;
        public int wordIndex, entry;
        private ScrollView mScroll;
        internal Button splitButton, bookmarkButton;
        public List<int> history;

        public WordPopup(AnnotationActivity activity)
        {
            mContext = activity;
            mWindow = new PopupWindow(mContext);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            {
                mWindow.SoftInputMode = SoftInput.AdjustNothing;
            }
            mWindowManager = activity.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            history = new List<int>();

            LayoutInflater inflater = (LayoutInflater)mContext.GetSystemService(Context.LayoutInflaterService);
            mRootView = inflater.Inflate(Resource.Layout.Popup, null);
            mBubble = (LinearLayout)mRootView.FindViewById(Resource.Id.bubble);
            mScroll = (ScrollView)mRootView.FindViewById(Resource.Id.scroller);
            mArrowDown = (ImageView)mRootView.FindViewById(Resource.Id.arrow_down);
            mArrowUp = (ImageView)mRootView.FindViewById(Resource.Id.arrow_up);
            mChars = (TextView)mRootView.FindViewById(Resource.Id.charsText);
            mChars.LinksClickable = true;
            mChars.MovementMethod = LinkMovementMethod.Instance;
            mContent = (TextView)mRootView.FindViewById(Resource.Id.content);
            mContent.LinksClickable = true;
            mContent.MovementMethod = LinkMovementMethod.Instance;
            mBookmark = (TextView)mRootView.FindViewById(Resource.Id.bookmarkTitle);

            this.Configure(mContext);

            mWindow.SetBackgroundDrawable(new BitmapDrawable());
            mWindow.Width = ViewGroup.LayoutParams.WrapContent;
            mWindow.Height = ViewGroup.LayoutParams.WrapContent;
            mWindow.Touchable = true;
            mWindow.Focusable = false;
            mWindow.OutsideTouchable = false;
            mWindow.ContentView = mRootView;

            Button copyButton = (Button)mRootView.FindViewById(Resource.Id.charsCopy);
            copyButton.Touch += Button_Touch;
            copyButton.Click += CopyButton_Click;

            splitButton = (Button)mRootView.FindViewById(Resource.Id.button_split);
            splitButton.Touch += Button_Touch;
            splitButton.Click += SplitButton_Click;

            Button starButton = (Button)mRootView.FindViewById(Resource.Id.button_star);
            starButton.Touch += Button_Touch;
            starButton.Click += StarButton_Click;

            bookmarkButton = (Button)mRootView.FindViewById(Resource.Id.button_bookmark);
            bookmarkButton.Touch += Button_Touch;
            bookmarkButton.Click += BookmarkButton_Click;

            Button shareButton = (Button)mRootView.FindViewById(Resource.Id.button_share);
            shareButton.Touch += Button_Touch;
            shareButton.Click += ShareButton_Click;

            LinearLayout popupButtons = (LinearLayout)mRootView.FindViewById(Resource.Id.popupButtons);
            Dictionaries.DictInfo[] dicts = Dictionaries.DictList;
            PackageManager pm = mContext.PackageManager;
            foreach (Dictionaries.DictInfo dict in dicts)
            {
                bool installed = Dictionaries.IsPackageInstalled(pm, dict.packageName);
                if (installed)
                {
                    Button dictBtn = new Button(mContext);
                    dictBtn.Text = dict.id;
                    dictBtn.TextSize = 20;
                    dictBtn.SetTextColor(Color.ParseColor("#99333333"));
                    //dictBtn.Tag = dict;
                    dictBtn.SetPadding((int)(10 * scale), (int)(2 * scale), 0, (int)(2 * scale));
                    dictBtn.SetBackgroundColor(Color.Transparent);
                    dictBtn.SetMinimumWidth(0);
                    dictBtn.SetMinWidth(0);
                    dictBtn.SetMinimumHeight(0);
                    dictBtn.SetMinHeight(0);
                    dictBtn.SetSingleLine(true);
                    dictBtn.Touch += Button_Touch;
                    popupButtons.AddView(dictBtn);
                    dictBtn.Click += DictBtn_Click;
                }
            }
        }

        private void DictBtn_Click(object sender, EventArgs e)
        {
            Dictionaries.DictInfo dict = (Dictionaries.DictInfo)sender;

            //Dictionaries.DictInfo dict = (Dictionaries.DictInfo)view.Tag;
            try
            {
                Dictionaries.FindInDictionary(mContext, dict, Dict.GetCh(entry, "simplified"));
            }
            catch (Exception ex)
            {
                Toast.MakeText(mContext, "Error: " + ex.Message, ToastLength.Long).Show();
            }
        }

        private void ShareButton_Click(object sender, EventArgs e)
        {
            string ch = Dict.GetCh(entry);
            Intent sendIntent = new Intent();
            sendIntent.SetAction(Intent.ActionSend);
            sendIntent.PutExtra(Intent.ExtraText, ch);
            sendIntent.SetType("text/plain");
            mContext.StartActivity(sendIntent);
        }

        private void BookmarkButton_Click(object sender, EventArgs e)
        {
            int lineNum = mContext.lines.IndexOf(line);
            long bookmarkPos = mContext.GetPosition(mContext.lines, lineNum + 1, wordIndex, true);
            int bookmarksPos = Bookmark.SearchClosest(bookmarkPos, mContext.mBookmarks);

            if (mContext.mBookmarks.Count > bookmarksPos && mContext.mBookmarks[bookmarksPos].mPosition == bookmarkPos)
            {
                mContext.mBookmarks.RemoveAt(bookmarksPos);
                if (!Bookmark.SaveToFile(mContext.mBookmarks, mContext.curFilePath + ".bookmarks"))
                {
                    Toast.MakeText(mContext, "Bookmarks could not be stored. File location is not writable.", ToastLength.Long).Show();
                }

                int foundBookmarksPos = Bookmark.SearchClosest(bookmarkPos, mContext.mFoundBookmarks);
                if (mContext.mFoundBookmarks.Count > foundBookmarksPos && mContext.mFoundBookmarks[foundBookmarksPos].mPosition == bookmarkPos)
                {
                    mContext.mFoundBookmarks.RemoveAt(foundBookmarksPos);
                }

                mContext.linesAdapter.NotifyItemChanged(lineNum + 1);
                show(mContext.linesLayoutManager.FindViewByPosition(lineNum + 1), line, wordIndex, showX, false);
            }
            else
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(mContext);
                builder.SetTitle("New bookmark");
                EditText inputBookmark = new EditText(mContext);
                inputBookmark.InputType = InputTypes.TextFlagCapSentences;
                inputBookmark.Text = Dict.PinyinToTones(Dict.GetPinyin(entry));
                inputBookmark.SelectAll();
                builder.SetView(inputBookmark);
                builder.SetCancelable(true);
                builder.SetPositiveButton("OK", delegate
                {
                    try
                    {
                        int lineNum1 = mContext.lines.IndexOf(line);
                        long bookmarkPos1 = mContext.GetPosition(mContext.lines, lineNum1 + 1, wordIndex, true);
                        int bookmarksPos1 = Bookmark.SearchClosest(bookmarkPos1, mContext.mBookmarks);
                        int foundBookmarksPos1 = Bookmark.SearchClosest(bookmarkPos1, mContext.mFoundBookmarks);

                        Bookmark newBookmark = new Bookmark(bookmarkPos1, inputBookmark.Text.ToString());
                        newBookmark.SetAnnotatedPosition(lineNum1, wordIndex);
                        mContext.mFoundBookmarks.Insert(foundBookmarksPos1, newBookmark);
                        mContext.mBookmarks.Insert(bookmarksPos1, newBookmark);

                        if (!Bookmark.SaveToFile(mContext.mBookmarks, mContext.curFilePath + ".bookmarks"))
                        {
                            Toast.MakeText(mContext, "Bookmarks could not be stored. File location is not writable.", ToastLength.Long).Show();
                        }

                        mContext.linesAdapter.NotifyItemChanged(lineNum1 + 1);
                        show(mContext.linesLayoutManager.FindViewByPosition(lineNum1 + 1), line, wordIndex, showX, false);
                    }
                    catch (FormatException)
                    {
                        Toast.MakeText(mContext, "", ToastLength.Long).Show();
                    }

                });
                builder.SetNegativeButton("Cancel", delegate
                {

                });
                AlertDialog dialog = builder.Create();
                dialog.Window.SetSoftInputMode(SoftInput.StateVisible);

                dialog.Show();
            }
        }

        private void StarButton_Click(object sender, EventArgs e)
        {
            View view = sender as View;

            string stars = AnnotationActivity.sharedPrefs.GetString("stars", "");
            string ch = Dict.GetCh(entry, "simplified");
            int index = stars.StartsWith(ch + "\n") ? 0 : stars.IndexOf("\n" + ch + "\n");
            if (index > -1)
            {
                AnnotationActivity.sharedPrefs.Edit().PutString("stars", stars.SubstringSpecial(0, index) + stars.Substring(index + ch.Length + 1)).Commit();
                Toast.MakeText(mContext, "Unstarred", ToastLength.Short).Show();
                ToggleStar(view, false);
            }
            else
            {
                if (stars.EndsWith("    ")) //Sometimes after closing the app, empty spaces are added to prefs
                {
                    stars = stars.SubstringSpecial(0, stars.Length - 4);
                }
                AnnotationActivity.sharedPrefs.Edit().PutString("stars", stars + ch + "\n").Commit();
                Toast.MakeText(mContext, "Starred", ToastLength.Short).Show();
                ToggleStar(view, true);
            }
        }

        private void SplitButton_Click(object sender, EventArgs e)
        {
            WordPopup view = sender as WordPopup;

            view.dismiss();

            List<object> subWords = BreakWord(Dict.GetCh(entry));
            int lineIndex = mContext.lines.IndexOf(line);

            mContext.linesAdapter.NotifyDataSetChanged();

            object firstWord = subWords[0];
            if (mContext.annoMode == AnnotationActivity.ANNOTATE_FILE)
            {
                mContext.curPos = 0;
                mContext.endPos = mContext.GetPosition(mContext.lines, lineIndex + 1, wordIndex, true) + (firstWord is int ? Dict.GetLength((int)firstWord) : ((string)firstWord).Length) * 3;
            }
            else
            {
                mContext.endPos = (int)mContext.GetPosition(mContext.lines, lineIndex + 1, wordIndex, false) + (firstWord is int ? Dict.GetLength((int)firstWord) : ((string)firstWord).Length);
            }

            List<object> curLine = (List<object>)line;
            int toRemove = curLine.Count - wordIndex;
            while (toRemove-- > 0)
            {
                curLine.RemoveAt(wordIndex);
            }
            curLine.Add(subWords[0]);

            int curWidth = 5;
            foreach (object word in curLine)
            {
                curWidth += mContext.testView.GetWordWidth((string)word);
            }

            mContext.SplitAnnotation(lineIndex, curWidth, curLine);
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            string ch = Dict.GetCh(entry);

            if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
            {
                global::Android.Text.ClipboardManager clipboard = (global::Android.Text.ClipboardManager)mContext.GetSystemService(Context.ClipboardService);
                clipboard.Text = ch;
            }
            else
            {
                global::Android.Content.ClipboardManager clipboard = (global::Android.Content.ClipboardManager)mContext.GetSystemService(Context.ClipboardService);
                ClipData clip = ClipData.NewPlainText("Chinese", ch);
                clipboard.PrimaryClip = clip;
            }

            AnnotationActivity.sharedPrefs.Edit().PutString("lastText", ch).Commit();
            Toast.MakeText(mContext, "Copied to clipboard", ToastLength.Short).Show();
        }

        private void Button_Touch(object sender, View.TouchEventArgs e)
        {
            View view = sender as View;

            switch (e.Event.Action)
            {
                case MotionEventActions.Down:
                    {
                        ((Button)view).SetTextColor(Color.ParseColor("#E0f47521"));
                        view.Background.SetColorFilter(Color.ParseColor("#E0f47521"), PorterDuff.Mode.SrcAtop);
                        view.Invalidate();
                        break;
                    }
                case MotionEventActions.Up:
                    {
                        ((Button)view).SetTextColor(Color.ParseColor("#99333333"));
                        view.Background.ClearColorFilter();
                        view.Invalidate();
                        break;
                    }
            }
        }
        
        public virtual void ToggleStar(View starButton, bool set)
        {
            Drawable starBg;

            if (starButton == null)
            {
                starButton = mRootView.FindViewById(Resource.Id.button_star);
            }

            if (set)
            {
                starBg = ContextCompat.GetDrawable(mContext, Resource.Drawable.star_on);
            }
            else
            {
                starBg = ContextCompat.GetDrawable(mContext, Resource.Drawable.star);
            }

            if (Build.VERSION.SdkInt < BuildVersionCodes.JellyBean)
            {
                starButton.SetBackgroundDrawable(starBg);
            }
            else
            {
                starButton.Background = starBg;
            }
        }

        public virtual void ToggleBookmark(View bookmarkButton, bool set)
        {
            Drawable bookmarkBg;

            if (bookmarkButton == null)
            {
                bookmarkButton = mRootView.FindViewById(Resource.Id.button_bookmark);
            }

            if (set)
            {
                bookmarkBg = ContextCompat.GetDrawable(mContext, Resource.Drawable.bookmark_on);
            }
            else
            {
                bookmarkBg = ContextCompat.GetDrawable(mContext, Resource.Drawable.bookmark);
            }

            if (Build.VERSION.SdkInt < BuildVersionCodes.JellyBean)
            {
                bookmarkButton.SetBackgroundDrawable(bookmarkBg);
            }
            else
            {
                bookmarkButton.Background = bookmarkBg;
            }
        }

        public virtual void Configure(Context context)
        {
            IWindowManager wm = context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            Display display = wm.DefaultDisplay;
            scale = context.Resources.DisplayMetrics.Density;

            if (Build.VERSION.SdkInt > BuildVersionCodes.Honeycomb)
            {
                Point screen = new Point();
                display.GetSize(screen);
                screenX = screen.X;
                screenY = screen.Y;
            }
            else
            {
                screenX = display.Width;
                screenY = display.Height;
            }

            mChars.SetMaxWidth((int)(screenX - 16 * scale));
            mContent.SetMaxHeight((int)(screenY - 5 * scale));
            mContent.SetMaxWidth((int)(screenX - 16 * scale));
            mBookmark.SetMaxWidth((int)(screenX - 45 * scale));
        }

        public static List<object> BreakWord(string theText)
        {
            int textLen = theText.Length, curPos = 0, last;
            List<object> words = new List<object>();

            while (curPos < textLen)
            {
                int i = Math.Min(textLen - curPos - 1, 3);

                if (curPos == 0)
                {
                    i = Math.Min(textLen - curPos - 2, 3);
                }

                last = -1;
                for (; i >= 0; i--)
                {
                    if (i == 3 && curPos > 0)
                    {
                        last = Dict.BinarySearch(theText.SubstringSpecial(curPos, curPos + i + 1), true);
                    }
                    else
                    {
                        if (last >= 0)
                        {
                            last = Dict.BinarySearch(theText.SubstringSpecial(curPos, curPos +  i + 1), 0, last - 1, false);
                        }
                        else
                        {
                            last = Dict.BinarySearch(theText.SubstringSpecial(curPos, curPos +  i + 1), false);
                        }
                    }

                    if (last >= 0)
                    {
                        if (i == 3)
                        { //the found entry may be longer than 4 (3 + 1)
                            if (Dict.GetLength(last) > textLen - curPos)
                            { //the found may be longer than the ending
                                continue;
                            }
                            string word = theText.SubstringSpecial(curPos, curPos + Dict.GetLength(last));
                            if (Dict.Equals(last, word))
                            {
                                words.Add(last);
                                curPos += word.Length;
                                break;
                            }
                        }
                        else
                        {
                            words.Add(last);
                            curPos += i + 1;
                            break;
                        }
                    }
                }

                if (i == -1 && last < 0)
                {
                    words.Add(theText.SubstringSpecial(curPos, curPos + 1));
                    curPos++;
                }
            }

            return words;
        }

        internal class WordSpan : ClickableSpan
        {
            private readonly WordPopup outerInstance;

            public WordSpan(WordPopup outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public string link;

            public override void OnClick(View view)
            {
                int wordNum = Dict.BinarySearch(link, false);
                if (wordNum != -1)
                {
                    if (outerInstance.history.Count > 0)
                    {
                        outerInstance.history.Add(outerInstance.entry);
                    }
                    else
                    {
                        outerInstance.history.Add(outerInstance.wordIndex);
                    }
                    outerInstance.show(outerInstance.parent, null, wordNum, outerInstance.showX, false);
                }
                else
                {
                    Toast.MakeText(outerInstance.mContext, "The word is not in the dictionary", ToastLength.Long).Show();
                }
            }
        }

        internal class CopySpan : ClickableSpan
        {
            private readonly WordPopup outerInstance;

            public CopySpan(WordPopup outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public string link;

            public override void OnClick(View view)
            {
                if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
                {
                    global::Android.Text.ClipboardManager clipboard = (global::Android.Text.ClipboardManager)outerInstance.mContext.GetSystemService(Context.ClipboardService);
                    clipboard.Text = link;
                }
                else
                {
                    global::Android.Content.ClipboardManager clipboard = (global::Android.Content.ClipboardManager)outerInstance.mContext.GetSystemService(Context.ClipboardService);
                    ClipData clip = ClipData.NewPlainText("Chinese", link);
                    clipboard.PrimaryClip = clip;
                }

                AnnotationActivity.sharedPrefs.Edit().PutString("lastText", link).Commit();
                Toast.MakeText(outerInstance.mContext, "Copied to clipboard", ToastLength.Short).Show();
            }
        }

        public virtual void show(View anchor, List<object> line, int wordNum, int showX, bool redraw)
        {
            try
            {
                this.showX = showX;

                if (redraw)
                {
                    this.dismiss();
                }

                if (line != null)
                {
                    this.line = line;
                    this.wordIndex = wordNum;
                    this.entry = (int)line[wordNum];
                    splitButton.Visibility = ViewStates.Visible;
                    bookmarkButton.Visibility = ViewStates.Visible;
                }
                else
                {
                    this.entry = wordNum;
                    splitButton.Visibility = ViewStates.Gone;
                    bookmarkButton.Visibility = ViewStates.Gone;
                }

                string sTrad = Dict.GetCh(entry, "traditional");
                string sSimp = Dict.GetCh(entry, "simplified");

                SpannableStringBuilder text = new SpannableStringBuilder();

                if (sSimp.Length > 1)
                {
                    List<object> subWords = BreakWord(sSimp);
                    foreach (object word in subWords)
                    {
                        if (word is int)
                        { //was found
                            string ch = Dict.GetCh((int)word, "simplified");
                            text.Append(ch).Append(new Java.Lang.String(" "));
                            WordSpan clickable = new WordSpan(this);
                            clickable.link = ch;
                            text.SetSpan(clickable, text.Length() - ch.Length - 1, text.Length() - 1, SpanTypes.User);
                        }
                        else
                        {
                            text.Append((string)word).Append(new Java.Lang.String(" "));
                        }
                    }
                }
                else
                {
                    text.Append(sSimp).Append(new Java.Lang.String(" "));
                    text.SetSpan(new AbsoluteSizeSpan(24, true), text.Length() - sSimp.Length - 1, text.Length() - 1, SpanTypes.User);
                    splitButton.Visibility = ViewStates.Gone;
                }

                if (!sTrad.Equals(sSimp))
                {
                    text.Append(sTrad);
                }

                mChars.SetText(text, TextView.BufferType.Spannable);

                text.Clear();
                string pinyin = Dict.PinyinToTones(Dict.GetPinyin(entry));
                text.Append("[ ").Append(new Java.Lang.String(pinyin)).Append(new Java.Lang.String(" ]  "));

                appendInlineButtons(text, pinyin);

                text.Append("\n");

                string[] parts = Regex.Split(Regex.Replace(Dict.GetEnglish(entry), @"/", "\n• "), @"\\$");

                int i = 0;
                foreach (string s in parts)
                {
                    if (i++ % 2 == 1)
                    {
                        pinyin = Dict.PinyinToTones(s);
                        text.Append("\n\n[ ").Append(new Java.Lang.String(pinyin)).Append(new Java.Lang.String(" ]  "));
                        appendInlineButtons(text, pinyin);
                        text.Append("\n");
                    }
                    else
                    {
                        text.Append("• ");

                        int beforeAppended = text.Length();

                        int bracketIndex, bracketEnd = 0;
                        while ((bracketIndex = s.IndexOf("[", bracketEnd, StringComparison.Ordinal)) > -1)
                        {
                            text.Append(s, bracketEnd, bracketIndex);
                            bracketEnd = s.IndexOf("]", bracketIndex);
                            text.Append(Dict.PinyinToTones(s.SubstringSpecial(bracketIndex, bracketEnd)));
                        }

                        text.Append(s, bracketEnd, s.Length);

                        int afterAppended = text.Length();

                        for (int m = beforeAppended; m < afterAppended; m++)
                        {
                            if (text.CharAt(m) >= '\u25CB' && text.CharAt(m) <= '\u9FA5')
                            {
                                int n = m + 1;
                                while (n < text.Length() && text.CharAt(n) >= '\u25CB' && text.CharAt(n) <= '\u9FA5')
                                {
                                    n++;
                                }

                                WordSpan clickable = new WordSpan(this);
                                clickable.link = text.SubSequence(m, n - m).ToString();
                                text.SetSpan(clickable, m, n, SpanTypes.User);
                            }
                        }
                    }
                }

                mContent.SetText(text, TextView.BufferType.Spannable);

                LinearLayout bookmarkTitleLayout = (LinearLayout)mRootView.FindViewById(Resource.Id.bookmark);
                if (mContext.annoMode != AnnotationActivity.ANNOTATE_FILE)
                {
                    bookmarkButton.Visibility = ViewStates.Gone;
                    bookmarkTitleLayout.Visibility = ViewStates.Gone;
                }
                else
                {
                    int lineNum = mContext.lines.IndexOf(line);
                    long bookmarkPos = mContext.GetPosition(mContext.lines, lineNum + 1, wordIndex, true);
                    int bookmarksPos = Bookmark.SearchClosest(bookmarkPos, mContext.mBookmarks);
                    bool isBookmarked = mContext.mBookmarks.Count > bookmarksPos && mContext.mBookmarks[bookmarksPos].mPosition == bookmarkPos;

                    ToggleBookmark(bookmarkButton, isBookmarked);

                    if (isBookmarked)
                    {
                        bookmarkTitleLayout.Visibility = ViewStates.Visible;
                        ((TextView)mRootView.FindViewById(Resource.Id.bookmarkTitle)).Text = mContext.mBookmarks[bookmarksPos].mTitle;
                    }
                    else
                    {
                        bookmarkTitleLayout.Visibility = ViewStates.Gone;
                    }
                }

                ToggleStar(null, AnnotationActivity.sharedPrefs.GetString("stars", "").StartsWith(Dict.GetCh(entry, "simplified") + "\n") || AnnotationActivity.sharedPrefs.GetString("stars", "").Contains("\n" + Dict.GetCh(entry, "simplified") + "\n"));

                this.parent = anchor;

                float xPos = 0, yPos = 0, arrowPos;

                int[] location = new int[2];

                anchor.GetLocationOnScreen(location);

                Rect anchorRect = new Rect(location[0], location[1], location[0] + anchor.Width, location[1] + anchor.Height);

                if (redraw)
                {
                    mWindow.ShowAtLocation(anchor, GravityFlags.NoGravity, 0, 0);
                }
                mBubble.Measure(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);

                float rootHeight = mBubble.MeasuredHeight;
                float rootWidth = mBubble.MeasuredWidth;

                xPos = showX - (rootWidth / 2);

                if ((xPos + rootWidth) > screenX)
                {
                    xPos = screenX - rootWidth;
                }
                else if (xPos < 0)
                {
                    xPos = 0;
                }

                arrowPos = showX - xPos;

                float dyTop = anchorRect.Top - 60 * scale;
                float dyBottom = screenY - anchorRect.Bottom - 20 * scale;

                bool onTop = dyBottom < rootHeight && dyTop > dyBottom;

                if (onTop)
                {
                    if (rootHeight + 20 * scale > dyTop)
                    {
                        yPos = 60 * scale;
                        rootHeight = anchorRect.Top - yPos - 20 * scale;
                    }
                    else
                    {
                        yPos = anchorRect.Top - rootHeight - 20 * scale;
                    }
                }
                else
                {
                    yPos = anchorRect.Bottom;

                    if (rootHeight > dyBottom)
                    {
                        rootHeight = dyBottom;
                    }
                }

                showArrow((onTop ? Resource.Id.arrow_down : Resource.Id.arrow_up), arrowPos, rootWidth);

                mWindow.Update((int)Math.Round(xPos), (int)Math.Round(yPos), (int)Math.Round(rootWidth), (int)Math.Round(rootHeight + 21 * scale));
                mScroll.FullScroll(FocusSearchDirection.Up);
            }
            catch (Exception e)
            {
                Toast.MakeText(mContext, "Error: " + e.Message, ToastLength.Long).Show();
            }
        }

        private void appendInlineButtons(SpannableStringBuilder text, string target)
        {
            CopySpan copySpan = new CopySpan(this);
            copySpan.link = target;
            text.Append(" ");
            Drawable copyImage = ContextCompat.GetDrawable(mContext, Resource.Drawable.ic_menu_copy_holo_light);
            copyImage.SetBounds(0, 0, (int)(mContent.LineHeight * 1.2), (int)(mContent.LineHeight * 1.2));
            text.SetSpan(new ImageSpan(copyImage), text.Length() - 2, text.Length() - 1, 0);
            text.SetSpan(copySpan, text.Length() - 2, text.Length() - 1, SpanTypes.User);
        }

        private void showArrow(int whichArrow, float requestedX, float rootWidth)
        {
            View showArrow = (whichArrow == Resource.Id.arrow_up) ? mArrowUp : mArrowDown;
            View hideArrow = (whichArrow == Resource.Id.arrow_up) ? mArrowDown : mArrowUp;

            showArrow.Visibility = ViewStates.Visible;

            int arrowWidth = showArrow.MeasuredWidth;

            ViewGroup.MarginLayoutParams param = (ViewGroup.MarginLayoutParams)showArrow.LayoutParameters;

            if (requestedX > rootWidth - 17 - arrowWidth / 2)
            {
                requestedX = rootWidth - 17 - arrowWidth / 2;
            }
            else if (requestedX < 17 + arrowWidth / 2)
            {
                requestedX = 17 + arrowWidth / 2;
            }

            param.LeftMargin = (int)Math.Round(requestedX - arrowWidth / 2);

            hideArrow.Visibility = ViewStates.Invisible;
            showArrow.Invalidate();
            hideArrow.Invalidate();
        }

        public virtual void dismiss()
        {
            int oldHlIndex = mContext.hlIndex.Y;
            mContext.hlIndex.Y = -1;

            if (mWindow.IsShowing)
            {
                if (oldHlIndex != -1)
                {
                    mContext.linesLayoutManager.FindViewByPosition(oldHlIndex + 1).Invalidate();
                }
                history.Clear();
                mWindow.Dismiss();
            }
        }

        public virtual bool Showing
        {
            get
            {
                return mWindow.IsShowing;
            }
        }
    }
}