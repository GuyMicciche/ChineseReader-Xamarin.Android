using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System;

namespace ChineseReader.Android
{
    public class Dictionaries
    {
        private Activity mActivity;

        public Dictionaries(Activity activity)
        {
            mActivity = activity;
            currentDictionary = defaultDictionary();
        }

        internal DictInfo currentDictionary;

        public class DictInfo
        {
            public readonly string id;
            public readonly string name;
            public readonly string packageName;
            public readonly string className;
            public readonly string action;
            public readonly int internalDict;
            public string dataKey = SearchManager.Query;

            public DictInfo(string id, string name, string packageName, string className, string action, int internalDict)
            {
                this.id = id;
                this.name = name;
                this.packageName = packageName;
                this.className = className;
                this.action = action;
                this.internalDict = internalDict;
            }
            public virtual DictInfo setDataKey(string key)
            {
                this.dataKey = key;
                return this;
            }
        }

        internal static readonly DictInfo[] dicts = new DictInfo[]
        {
            new DictInfo("F", "Fora Dictionary", "com.ngc.fora", "com.ngc.fora.ForaDictionary", Intent.ActionSearch, 0),
            new DictInfo("CD", "ColorDict / GoldenDict", "com.socialnmobile.colordict", "com.socialnmobile.colordict.activity.Main", "colordict.intent.action.SEARCH", 1),
            new DictInfo("GD", "GoldenDict", "mobi.goldendict.android.free", "mobi.goldendict.android.free.activity.Main", "goldendict.intent.action.SEARCH", 1),
            new DictInfo("GD", "GoldenDict", "mobi.goldendict.android", "mobi.goldendict.android.activity.Main", "goldendict.intent.action.SEARCH", 1),
            new DictInfo("AD", "Aard Dictionary", "aarddict.android", "aarddict.android.Article", Intent.ActionSearch, 0),
            new DictInfo("ADL", "Aard Dictionary Lookup", "aarddict.android", "aarddict.android.Lookup", Intent.ActionSearch, 0),
            new DictInfo("A2", "Aard 2 Dictionary", "itkach.aard2", "aard2.lookup", Intent.ActionSearch, 3),
            new DictInfo("D", "Dictan Dictionary", "info.softex.dictan", null, Intent.ActionSearch, 2),
            new DictInfo("FD", "Free Dictionary . org", "org.freedictionary", "org.freedictionary.MainActivity", "android.intent.action.VIEW", 0),
            new DictInfo("AL", "ABBYY Lingvo", "com.abbyy.mobile.lingvo.market", null, "com.abbyy.mobile.lingvo.intent.action.TRANSLATE", 0).setDataKey("com.abbyy.mobile.lingvo.intent.extra.TEXT"),
            new DictInfo("LQL", "Lingo Quiz Lite", "mnm.lite.lingoquiz", "mnm.lite.lingoquiz.ExchangeActivity", "lingoquiz.intent.action.ADD_WORD", 0).setDataKey("EXTRA_WORD"),
            new DictInfo("LQ", "Lingo Quiz", "mnm.lingoquiz", "mnm.lingoquiz.ExchangeActivity", "lingoquiz.intent.action.ADD_WORD", 0).setDataKey("EXTRA_WORD"),
            new DictInfo("LD", "LEO Dictionary", "org.leo.android.dict", "org.leo.android.dict.LeoDict", "android.intent.action.SEARCH", 0).setDataKey("query"),
            new DictInfo("PD", "Popup Dictionary", "com.barisatamer.popupdictionary", "com.barisatamer.popupdictionary.MainActivity", "android.intent.action.VIEW", 0),
            new DictInfo("P", "Pleco", "com.pleco.chinesesystem", "com.pleco.chinesesystem.PlecoDictLauncherActivity", Intent.ActionSearch, 0),
            new DictInfo("HP", "Hanping Pro", "com.embermitre.hanping.app.pro", "com.embermitre.dictroid.ui.RedirectActivity", Intent.ActionSearch, 0),
            new DictInfo("HP", "Hanping", "com.embermitre.hanping.app.lite", "com.embermitre.dictroid.ui.RedirectActivity", Intent.ActionSearch, 0)
        };

        public const string DEFAULT_DICTIONARY_ID = "com.ngc.fora";

        internal static DictInfo findById(string id)
        {
            foreach (DictInfo d in dicts)
            {
                if (d.id.Equals(id))
                {
                    return d;
                }
            }
            return null;
        }

        internal static DictInfo defaultDictionary()
        {
            return findById(DEFAULT_DICTIONARY_ID);
        }


        public static DictInfo[] DictList
        {
            get
            {
                return dicts;
            }
        }

        public virtual string Dict
        {
            set
            {
                DictInfo d = findById(value);
                if (d != null)
                {
                    currentDictionary = d;
                }
            }
        }

        public static bool IsPackageInstalled(PackageManager pm, string packageName)
        {
            try
            {
                pm.GetPackageInfo(packageName, 0); //PackageManager.GET_ACTIVITIES);
                return true;
            }
            catch (PackageManager.NameNotFoundException e)
            {
                System.Console.WriteLine("Dictionaries IsPackageInstalled ERROR => " + e.Message);

                return false;
            }
        }

        private const int DICTAN_ARTICLE_REQUEST_CODE = 100;
        private const string DICTAN_ARTICLE_WORD = "article.word";
        private const string DICTAN_ERROR_MESSAGE = "error.message";
        private const int FLAG_ACTIVITY_CLEAR_TASK = 0x00008000;

        public class DictionaryException : Exception
        {
            public DictionaryException(string msg) : base(msg)
            {

            }
        }

        public static void FindInDictionary(Activity mActivity, DictInfo currentDictionary, string s)
        {
            switch (currentDictionary.internalDict)
            {
                case 0:
                    Intent intent = new Intent(currentDictionary.action);
                    if (currentDictionary.className != null || Build.VERSION.SdkInt == BuildVersionCodes.Cupcake)
                    {
                        intent.SetComponent(new ComponentName(currentDictionary.packageName, currentDictionary.className));
                    }
                    else
                    {
                        intent.SetPackage(currentDictionary.packageName);
                    }
                    intent.AddFlags(Build.VERSION.SdkInt >= BuildVersionCodes.EclairMr1 ? ActivityFlags.ClearTask : ActivityFlags.NewTask);
                    if (s != null)
                    {
                        intent.PutExtra(currentDictionary.dataKey, s);
                        intent.PutExtra(Intent.ExtraText, s);
                        intent.SetType("text/plain");
                    }
                    try
                    {
                        mActivity.StartActivity(intent);
                    }
                    catch (ActivityNotFoundException e)
                    {
                        System.Console.WriteLine("Dictionaries FindInDictionary ERROR => " + e.Message);

                        throw new DictionaryException("Dictionary \"" + currentDictionary.name + "\" is not installed");
                    }
                    break;
                case 1:
                    string SEARCH_ACTION = currentDictionary.action;
                    string EXTRA_QUERY = "EXTRA_QUERY";
                    string EXTRA_FULLSCREEN = "EXTRA_FULLSCREEN";

                    Intent intent1 = new Intent(SEARCH_ACTION);
                    if (s != null)
                    {
                        intent1.PutExtra(EXTRA_QUERY, s); //Search Query
                    }
                    intent1.PutExtra(EXTRA_FULLSCREEN, true);
                    try
                    {
                        mActivity.StartActivity(intent1);
                    }
                    catch (ActivityNotFoundException)
                    {
                        throw new DictionaryException("Dictionary \"" + currentDictionary.name + "\" is not installed");
                    }
                    break;
                case 2:
                    // Dictan support
                    Intent intent2 = new Intent("android.intent.action.VIEW");
                    // Add custom category to run the Dictan external dispatcher
                    intent2.AddCategory("info.softex.dictan.EXTERNAL_DISPATCHER");

                    // Don't include the dispatcher in activity  
                    // because it doesn't have any content view.	      
                    intent2.SetFlags(ActivityFlags.NoHistory);

                    intent2.PutExtra(DICTAN_ARTICLE_WORD, s);

                    try
                    {
                        mActivity.StartActivityForResult(intent2, DICTAN_ARTICLE_REQUEST_CODE);
                    }
                    catch (ActivityNotFoundException)
                    {
                        throw new DictionaryException("Dictionary \"" + currentDictionary.name + "\" is not installed");
                    }
                    break;
                case 3:
                    Intent intent3 = new Intent("aard2.lookup");
                    intent3.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                    intent3.PutExtra(SearchManager.Query, s);
                    try
                    {
                        mActivity.StartActivity(intent3);
                    }
                    catch (ActivityNotFoundException)
                    {
                        throw new DictionaryException("Dictionary \"" + currentDictionary.name + "\" is not installed");
                    }
                    break;
            }
        }

        public void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            if (requestCode == DICTAN_ARTICLE_REQUEST_CODE)
            {
                switch (resultCode)
                {
                    // The article has been shown, the intent is never expected null
                    case Result.Ok:
                        break;

                    // Error occured
                    case Result.Canceled:
                        string errMessage = "Unknown Error.";
                        if (intent != null)
                        {
                            errMessage = "The Requested Word: " + intent.GetStringExtra(DICTAN_ARTICLE_WORD) + ". Error: " + intent.GetStringExtra(DICTAN_ERROR_MESSAGE);
                        }
                        throw new DictionaryException(errMessage);

                    // Must never occur
                    default:
                        throw new DictionaryException("Unknown Result Code: " + resultCode);
                }
            }
        }
    }
}