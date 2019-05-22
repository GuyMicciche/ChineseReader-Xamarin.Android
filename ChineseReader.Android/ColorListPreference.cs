using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Collections.Generic;
using System.Globalization;

namespace ChineseReader.Android
{
    public class ColorListPreference : ListPreference
    {
        private Context mContext;

        public ColorListPreference(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            mContext = context;
        }

        protected override void OnPrepareDialogBuilder(AlertDialog.Builder builder)
        {
            if (builder == null)
            {
                throw new System.NullReferenceException("Builder is null");
            }

            string[] entries = GetEntries();
            string[] entryValues = GetEntryValues();

            if (entries == null || entryValues == null || entries.Length != entryValues.Length)
            {
                throw new System.InvalidOperationException("Invalid entries array or entryValues array");
            }

            ArrayAdapter<string> adapter = new ListPreferenceAdapter(mContext, global::Android.Resource.Layout.SelectDialogSingleChoice, entries);
            builder.SetAdapter(adapter, this);

            base.OnPrepareDialogBuilder(builder);
        }        
    }

    public class ListPreferenceAdapter : ArrayAdapter<string>
    {
        private Context context;
        private string[] entries;

        public ListPreferenceAdapter(Context context, int textViewResourceId, string[] entries) : base(context, textViewResourceId, entries)
        {
            this.context = context;
            this.entries = entries;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = base.GetView(position, convertView, parent);

            CheckedTextView checkedTextView = (CheckedTextView)view.FindViewById(global::Android.Resource.Id.Text1);
            checkedTextView.SetTextColor(new Color((int)long.Parse(entries[position].ToString(), NumberStyles.HexNumber)));

            return view;
        }
    }
}