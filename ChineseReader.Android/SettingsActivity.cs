using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using System;

namespace ChineseReader.Android
{
    [Activity]
    public class SettingsActivity : PreferenceActivity, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        private bool changed = false;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AddPreferencesFromResource(Resource.Xml.preferences);
            PreferenceManager.SetDefaultValues(this, Resource.Xml.preferences, false);
            for (int i = 0; i < PreferenceScreen.PreferenceCount; i++)
            {
                InitSummary(PreferenceScreen.GetPreference(i));
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            // Set up a listener whenever a key changes
            PreferenceScreen.SharedPreferences.RegisterOnSharedPreferenceChangeListener(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            // Unregister the listener whenever a key changes
            PreferenceScreen.SharedPreferences.UnregisterOnSharedPreferenceChangeListener(this);
        }

        void ISharedPreferencesOnSharedPreferenceChangeListener.OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
        {
            changed = true;
            UpdatePrefSummary(FindPreference(key));
        }

        private void InitSummary(Preference p)
        {
            if (p is PreferenceCategory)
            {
                PreferenceCategory pCat = (PreferenceCategory)p;
                for (int i = 0; i < pCat.PreferenceCount; i++)
                {
                    InitSummary(pCat.GetPreference(i));
                }
            }
            else
            {
                UpdatePrefSummary(p);
            }
        }

        private void UpdatePrefSummary(Preference p)
        {
            if (p is ListPreference)
            {
                ListPreference listPref = (ListPreference)p;
                p.Summary = listPref.Entry;
            }
            if (p is EditTextPreference)
            {
                EditTextPreference editTextPref = (EditTextPreference)p;
                p.Summary = editTextPref.Text;
            }
            if (p is SeekBarPreference)
            {
                SeekBarPreference seekBarPref = (SeekBarPreference)p;
                p.Summary = Convert.ToString(seekBarPref.Progress) + "%";
            }
        }
        public override void OnBackPressed()
        {
            Intent resultIntent = new Intent();
            SetResult((Result)AnnotationActivity.RESULT_SETTINGS_CHANGED, resultIntent);
            Finish();
        }
    }
}