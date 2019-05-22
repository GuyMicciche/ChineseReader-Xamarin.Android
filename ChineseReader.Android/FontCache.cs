using Android.Content;
using Android.Graphics;
using System;
using System.Collections.Generic;

namespace ChineseReader.Android
{
    public class FontCache
    {
        private static Dictionary<string, Typeface> fontCache = new Dictionary<string, Typeface>();

        public static Typeface get(string name, Context context)
        {
            Typeface tf = fontCache.GetValue(name);

            if (tf == null)
            {
                try
                {
                    if (name.Equals("default"))
                    {
                        tf = Typeface.Default;
                    }
                    else
                    {
                        if (name.StartsWith("b_"))
                        {
                            tf = Typeface.Create(Typeface.CreateFromAsset(context.Assets, name.Substring(2)), TypefaceStyle.Bold);
                        }
                        else
                        {
                            tf = Typeface.CreateFromAsset(context.Assets, name);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("FontCache get ERROR => " + e.Message);

                    return null;
                }
                fontCache[name] = tf;
            }
            return fontCache[name];
        }
    }

    public static class DictionaryExtension
    {
        public static TValue GetValueOrDefault<TKey, TValue>
            (this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : default(TValue);
        }

        public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}