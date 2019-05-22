using Android.Content;
using Android.Graphics;
using Android.Preferences;
using Android.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    public class LineView : View, View.IOnTouchListener
    {
        public List<object> line;
        public IList<List<object>> lines;
        public Paint pinyinPaint, charPaint, hlPaint;
        public Rect pinyinBounds = new Rect(), charBounds = new Rect(), subCharBounds = new Rect();
        public string pinyinType, wordDist;
        public int textSizeInt, pinyinSizeInt;
        public int vMargin, hMargin;
        public float lastX;
        public List<string> top;
        public List<string> bottom;
        public List<int> tones;
        public List<Bookmark> bookmarks;
        public float scale, space;
        public bool lastLine = false;
        public static ISharedPreferences sharedPrefs;
        public Typeface charTypeface, charTypefaceNoBold;
        public int[] toneColors;
        public Point hlIndex;

        public LineView(Context context) : base(context)
        {
            SetOnTouchListener(this);

            if (sharedPrefs == null)
            {
                sharedPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
            }

            scale = context.Resources.DisplayMetrics.Density;
            Init();
            UpdateVars();
            bookmarks = new List<Bookmark>();
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            if (e.Action == MotionEventActions.Up)
            {
                lastX = e.GetX();
            }
            return false;
        }

        public void Init()
        {
            pinyinPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.LinearText);
            charPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.LinearText);
            hlPaint = new Paint();
            pinyinPaint.Color = new Color((int)long.Parse(sharedPrefs.GetString("pref_pinyinColor", "FF024D93"), NumberStyles.HexNumber));//3D9EFF
            charPaint.Color = Color.Black;
            hlPaint.Color = Color.ParseColor("#FFC4E1FF"); //#9ECEFF);
            pinyinPaint.SetStyle(Paint.Style.Fill);
            charPaint.SetStyle(Paint.Style.Fill);
        }

        public void UpdateVars()
        {
            pinyinType = sharedPrefs.GetString("pref_pinyinType", "marks");
            textSizeInt = sharedPrefs.GetInt("pref_textSizeInt", 16);
            pinyinSizeInt = sharedPrefs.GetInt("pref_pinyinSizeInt", 100);
            wordDist = sharedPrefs.GetString("pref_wordDist", "dynamic");
            pinyinPaint.Color = new Color((int)long.Parse(sharedPrefs.GetString("pref_pinyinColor", "FF024D93"), NumberStyles.HexNumber));//3D9EFF
            string fontName = sharedPrefs.GetString("pref_charFont", "default");
            charTypeface = FontCache.get(fontName, Context);
            charTypefaceNoBold = fontName.StartsWith("b_") ? FontCache.get(fontName.Substring(2), Context) : charTypeface;
            charPaint.SetTypeface(charTypeface);

            toneColors = new int[5];
            string toneColorsPrefs = sharedPrefs.GetString("pref_toneColors", "none");
            if (!toneColorsPrefs.Equals("none"))
            {
                for (int i = 0; i < 5; i++)
                {
                    //toneColors[i] = Color.ParseColor(toneColorsPrefs.Substring(i * 9, 8));
                    toneColors[i] = (int)long.Parse(toneColorsPrefs.Substring(i * 9, i * 9 + 8), NumberStyles.HexNumber);
                }
            }
            else
            {
                toneColors = null;
            }

            charPaint.TextSize = textSizeInt * scale; // + 0.5f);
            float pinyinSize = textSizeInt * pinyinSizeInt / 100;
            if (Math.Round(pinyinSize) == 0)
            {
                pinyinType = "none";
            }
            else
            {
                pinyinPaint.TextSize = pinyinSize * scale;
            }

            hMargin = (int)Math.Round(Math.Max(textSizeInt, pinyinSize) * scale / 5);
            vMargin = (int)Math.Round(Math.Max(textSizeInt, pinyinSize) * scale / 4);

            if ("none".Equals(wordDist))
            {
                hMargin = 1;
            }

            RequestLayout();
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            if (pinyinType.Equals("none"))
            {
                SetMeasuredDimension(MeasureSpec.GetSize(widthMeasureSpec), (int)Math.Round(vMargin + charPaint.TextSize));
            }
            else
            {
                SetMeasuredDimension(MeasureSpec.GetSize(widthMeasureSpec), (int)Math.Round(pinyinPaint.TextSize + vMargin + charPaint.TextSize));
            }
        }

        protected override void OnDraw(Canvas canvas)
        {
            float curX = hMargin, totalWidth = 0;
            int count = line.Count;
            int lineNum = lines.IndexOf(line);
            space = 0;

            if ("dynamic".Equals(wordDist) && !lastLine)
            {
                for (int i = 0; i < count; i++)
                {
                    object word = line[i];
                    float maxWidth;

                    if (word is string)
                    {
                        if (!Regex.IsMatch((string)word, @"[^\w]*"))
                        {
                            charPaint.SetTypeface(Typeface.Default);
                        }
                        else
                        {
                            charPaint.SetTypeface(charTypefaceNoBold);
                        }

                        charPaint.GetTextBounds((string)word, 0, ((string)word).Length, charBounds);
                        totalWidth += charPaint.MeasureText((string)word);
                    }
                    else
                    {
                        charPaint.SetTypeface(charTypeface);

                        string key = bottom[i];
                        if (pinyinType.Equals("none"))
                        {
                            charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                            maxWidth = charBounds.Width();
                        }
                        else
                        {
                            string pinyin = top[i];
                            pinyinPaint.GetTextBounds(pinyin, 0, pinyin.Length, pinyinBounds);
                            charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                            maxWidth = Math.Max(pinyinBounds.Width(), charBounds.Width());
                        }

                        totalWidth += maxWidth + hMargin * 2;
                    }
                }

                space = (Width - 2 * hMargin - totalWidth) / (count - 1);
            }

            for (int i = 0; i < count; i++)
            {
                object word = line[i];
                float maxWidth;

                if (word is string)
                {
                    if (!Regex.IsMatch((string)word, @"[^\w]*"))
                    {
                        charPaint.SetTypeface(Typeface.Default);
                    }
                    else
                    {
                        charPaint.SetTypeface(charTypefaceNoBold);
                    }

                    charPaint.Color = Color.Black;
                    charPaint.GetTextBounds((string)word, 0, ((string)word).Length, charBounds);
                    maxWidth = charPaint.MeasureText((string)word);
                    if (maxWidth > 0)
                    {
                        if (pinyinType.Equals("none"))
                        {
                            canvas.DrawText(bottom[i], curX - (charBounds.Left < 0 ? charBounds.Left : 0), (float)Math.Round(charPaint.TextSize), charPaint);
                        }
                        else
                        {
                            canvas.DrawText(bottom[i], curX - (charBounds.Left < 0 ? charBounds.Left : 0), (float)Math.Round(pinyinPaint.TextSize + charPaint.TextSize), charPaint);
                        }
                    }

                    curX += maxWidth + space;
                }
                else
                {
                    charPaint.SetTypeface(charTypeface);

                    string key = bottom[i];
                    if (pinyinType.Equals("none"))
                    {
                        charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                        maxWidth = charBounds.Width();

                        if (hlIndex.Y == lineNum && hlIndex.X == i)
                        {
                            canvas.DrawRect(curX, 0, curX + maxWidth + hMargin * 2, charPaint.TextSize + vMargin, hlPaint);
                        }

                        if (bookmarks.Count > 0 && bookmarks[i] != null)
                        {
                            DrawBookmark(canvas, bookmarks[i], curX);
                        }

                        if (toneColors != null)
                        { //means tone colors are selected
                            int charTones = tones[i];
                            int chars = key.Length;
                            int subCharX = 0;
                            for (int c = 0; c < chars; c++)
                            {
                                int tone = charTones % 10;
                                charTones /= 10;
                                charPaint.Color = new Color(toneColors[tone - 1]);

                                canvas.DrawText(key, c, c + 1, curX + subCharX + hMargin - charBounds.Left, charPaint.TextSize, charPaint);

                                charPaint.GetTextBounds(key, c, c + 1, subCharBounds);
                                subCharX += subCharBounds.Width() + subCharBounds.Left;
                            }
                        }
                        else
                        {
                            charPaint.Color = Color.Black;
                            canvas.DrawText(key, curX + hMargin - charBounds.Left, charPaint.TextSize, charPaint);
                        }
                    }
                    else
                    {
                        string pinyin = top[i];
                        pinyinPaint.GetTextBounds(pinyin, 0, pinyin.Length, pinyinBounds);
                        charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                        maxWidth = Math.Max(pinyinBounds.Width(), charBounds.Width());

                        if (hlIndex.Y == lineNum && hlIndex.X == i)
                        {
                            canvas.DrawRect(curX, 0, curX + maxWidth + hMargin * 2, pinyinPaint.TextSize + vMargin + charPaint.TextSize, hlPaint);
                        }

                        if (bookmarks.Count > 0 && bookmarks[i] != null)
                        {
                            DrawBookmark(canvas, bookmarks[i], curX);
                        }

                        canvas.DrawText(pinyin, curX + hMargin - pinyinBounds.Left + (maxWidth - pinyinBounds.Width()) / 2, pinyinPaint.TextSize, pinyinPaint);

                        if (toneColors != null)
                        { //means tone colors are selected
                            int charTones = tones[i];
                            int chars = key.Length;
                            int subCharX = 0;
                            for (int c = 0; c < chars; c++)
                            {
                                int tone = charTones % 10;
                                charTones /= 10;
                                charPaint.Color = new Color(toneColors[tone - 1]);

                                canvas.DrawText(key, c, c + 1, curX + subCharX + hMargin - charBounds.Left + (maxWidth - charBounds.Width()) / 2, pinyinPaint.TextSize + charPaint.TextSize, charPaint);

                                charPaint.GetTextBounds(key, c, c + 1, subCharBounds);
                                subCharX += subCharBounds.Width() + subCharBounds.Left;
                            }
                        }
                        else
                        {
                            charPaint.Color = Color.Black;
                            canvas.DrawText(key, curX + hMargin - charBounds.Left + (maxWidth - charBounds.Width()) / 2, pinyinPaint.TextSize + charPaint.TextSize, charPaint);
                        }
                    }

                    curX += maxWidth + hMargin * 2 + space;
                }
            }
        }

        public void DrawBookmark(Canvas canvas, Bookmark itsBookmark, float curX)
        {
            Color hlColor = hlPaint.Color;
            hlPaint.Color = Color.ParseColor("#ffff0000");
            //hlPaint.setAntiAlias(true);

            Path path = new Path();
            path.SetFillType(Path.FillType.EvenOdd);
            path.MoveTo(curX, 0);
            path.LineTo(curX + 12 * scale, 0);
            path.LineTo(curX, 12 * scale);
            path.Close();

            canvas.DrawPath(path, hlPaint);

            hlPaint.Color = hlColor;
        }

        public int GetWordWidth(object word)
        {
            if (pinyinPaint == null)
            {
                Init();
            }

            if (word is string)
            {
                Regex rx = new Regex(@"[^\w]*");
                if (!rx.IsMatch((string)word))
                {
                    charPaint.SetTypeface(Typeface.Default);
                }
                else
                {
                    charPaint.SetTypeface(charTypeface);
                }

                float wordWidth = charPaint.MeasureText((string)word);
                return (int)Math.Round(wordWidth);
            }
            else
            {
                charPaint.SetTypeface(charTypeface);

                int entry = (int)word;
                if (pinyinType.Equals("none"))
                {
                    string key = Dict.GetCh(entry);
                    charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                    return charBounds.Width() + hMargin * 2;
                }
                else
                {
                    string pinyin = Dict.PinyinToTones(Dict.GetPinyin(entry)), key = Dict.GetCh(entry);
                    pinyinPaint.GetTextBounds(pinyin, 0, pinyin.Length, pinyinBounds);
                    charPaint.GetTextBounds(key, 0, key.Length, charBounds);

                    return Math.Max(pinyinBounds.Width(), charBounds.Width()) + hMargin * 2;
                }
            }
        }

        public int GetWordWidth(string str)
        {
            return (int)Math.Round(charPaint.MeasureText(str));
        }

        public void GetTouchedWord(int[] touchedData)
        {
            int curX = hMargin, count = line.Count;
            touchedData[0] = -1;

            for (int i = 0; i < count; i++)
            {
                int width = GetWordWidth(line[i]);
                if (curX <= lastX && curX + width >= lastX)
                {
                    touchedData[0] = i;
                    touchedData[1] = curX + width / 2;
                    return;
                }
                curX += (int)(width + space);
            }
        }

        public float WordHeight
        {
            get
            {
                if (pinyinType.Equals("none"))
                {
                    return charPaint.TextSize + hMargin;
                }
                else
                {
                    return pinyinPaint.TextSize + hMargin * 2 + charPaint.TextSize;
                }
            }
        }

        public int Margins
        {
            get
            {
                return hMargin * 2;
            }
        }

        public static int GetLineSize(List<object> line, bool isFile)
        {
            int res = 0;

            try
            {
                int length = line.Count;
                for (int j = 0; j < length; j++)
                {
                    object word = line[j];
                    if (word is string)
                    {
                        if (((string)word).Length == 0)
                        {
                            res += 1;
                        }
                        else
                        {
                            res += isFile ? Encoding.UTF8.GetBytes((string)word).Length : ((string)word).Length;
                        }
                    }
                    else
                    {
                        res += isFile ? Dict.GetLength((int)word) * 3 : Dict.GetLength((int)word);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("GetLineSize ERROR => " + e.Message);
            }

            return res;
        }
    }
}

internal static class StringHelperClass
{
    //----------------------------------------------------------------------------------
    //	This method replaces the Java String.substring method when 'start' is a
    //	method call or calculated value to ensure that 'start' is obtained just once.
    //----------------------------------------------------------------------------------
    internal static string SubstringSpecial(this string self, int start, int end)
    {
        return self.Substring(start, end - start);
    }

    //------------------------------------------------------------------------------------
    //	This method is used to replace calls to the 2-arg Java String.startsWith method.
    //------------------------------------------------------------------------------------
    internal static bool StartsWith(this string self, string prefix, int toffset)
    {
        return self.IndexOf(prefix, toffset, System.StringComparison.Ordinal) == toffset;
    }

    //------------------------------------------------------------------------------
    //	This method is used to replace most calls to the Java String.split method.
    //------------------------------------------------------------------------------
    internal static string[] Split(this string self, string regexDelimiter, bool trimTrailingEmptyStrings)
    {
        string[] splitArray = System.Text.RegularExpressions.Regex.Split(self, regexDelimiter);

        if (trimTrailingEmptyStrings)
        {
            if (splitArray.Length > 1)
            {
                for (int i = splitArray.Length; i > 0; i--)
                {
                    if (splitArray[i - 1].Length > 0)
                    {
                        if (i < splitArray.Length)
                            System.Array.Resize(ref splitArray, i);

                        break;
                    }
                }
            }
        }

        return splitArray;
    }

    //-----------------------------------------------------------------------------
    //	These methods are used to replace calls to some Java String constructors.
    //-----------------------------------------------------------------------------
    internal static string NewString(byte[] bytes)
    {
        return NewString(bytes, 0, bytes.Length);
    }
    internal static string NewString(byte[] bytes, int index, int count)
    {
        return System.Text.Encoding.UTF8.GetString((byte[])(object)bytes, index, count);
    }
    internal static string NewString(byte[] bytes, string encoding)
    {
        return NewString(bytes, 0, bytes.Length, encoding);
    }
    internal static string NewString(byte[] bytes, int index, int count, string encoding)
    {
        return System.Text.Encoding.GetEncoding(encoding).GetString((byte[])(object)bytes, index, count);
    }

    //--------------------------------------------------------------------------------
    //	These methods are used to replace calls to the Java String.getBytes methods.
    //--------------------------------------------------------------------------------
    internal static byte[] GetBytes(this string self)
    {
        return GetSBytesForEncoding(System.Text.Encoding.UTF8, self);
    }
    internal static byte[] GetBytes(this string self, string encoding)
    {
        return GetSBytesForEncoding(System.Text.Encoding.GetEncoding(encoding), self);
    }
    private static byte[] GetSBytesForEncoding(System.Text.Encoding encoding, string s)
    {
        byte[] bytes = new byte[encoding.GetByteCount(s)];
        encoding.GetBytes(s, 0, s.Length, (byte[])(object)bytes, 0);
        return bytes;
    }
}