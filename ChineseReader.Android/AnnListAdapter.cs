using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChineseReader.Android
{
    public class ViewHolder : RecyclerView.ViewHolder
    {
        public LineView mLineView;
        public ViewHolder(LineView v) : base(v)
        {
            mLineView = v;
        }
    }

    public class AnnListAdapter : RecyclerView.Adapter
    {
        private readonly int VIEW_ITEM = 1;
        private readonly int VIEW_HEADER = 0;
        private readonly int VIEW_FOOTER = 2;

        private AnnotationActivity context;
        private WordPopup wPopup;
        private IList<List<object>> mLines;

        public bool ShowHeader = false;
        public bool ShowFooter = false;

        

        public AnnListAdapter(AnnotationActivity context, IList<List<object>> items, RecyclerView recyclerView, WordPopup popup)
        {
            this.context = context;
            wPopup = popup;
            mLines = items;

            LinearLayoutManager linearLayoutManager = (LinearLayoutManager)recyclerView.GetLayoutManager();
        }

        public override int GetItemViewType(int position)
        {
            if (position == 0)
            {
                return VIEW_HEADER;
            }
            else if (position == ItemCount - 1)
            {
                return VIEW_FOOTER;
            }
            else
            {
                return VIEW_ITEM;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            RecyclerView.ViewHolder vh = null;

            if (viewType == VIEW_ITEM)
            {
                LineView v = new LineView(this.context);

                vh = new LineViewHolder(this, v);
            }
            else if (viewType == VIEW_HEADER)
            {
                View v = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ListHeader, parent, false);

                vh = new ProgressViewHolder(this, v);
            }
            else if (viewType == VIEW_FOOTER)
            {
                View v = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ListFooter, parent, false);

                vh = new ProgressViewHolder(this, v);
            }
            return vh;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is LineViewHolder)
            {
                List<object> line = mLines[position - 1];

                LineView curView = ((LineViewHolder)holder).mLineView;
                curView.line = line;
                curView.lines = mLines;
                curView.hlIndex = context.hlIndex;
                curView.bookmarks.Clear();

                if (context.mFoundBookmarks.Count > 0)
                {
                    for (int i = 0; i < line.Count; i++)
                    {
                        Bookmark itsBookmark = Bookmark.SearchAnnotated(position - 1, i, context.mFoundBookmarks);
                        if (itsBookmark != null)
                        {
                            curView.bookmarks.Add(itsBookmark);
                        }
                        else
                        {
                            curView.bookmarks.Add(null);
                        }
                    }
                }

                if (curView.top == null)
                {
                    curView.top = new List<string>();
                }
                else
                {
                    curView.top.Clear();
                }

                if (curView.bottom == null)
                {
                    curView.bottom = new List<string>();
                }
                else
                {
                    curView.bottom.Clear();
                }

                if (curView.tones == null)
                {
                    curView.tones = new List<int>();
                }
                else
                {
                    curView.tones.Clear();
                }

                int count = line.Count;
                for (int i = 0; i < count; i++)
                {
                    var word = line[i];

                    if (word is string)
                    {
                        curView.bottom.Add((string)word);
                        curView.top.Add("");
                        curView.tones.Add(0);
                    }
                    else
                    {
                        int entry = (int)word;
                        string key = Dict.GetCh(entry);
                        curView.bottom.Add(key);
                        if (AnnotationActivity.sharedPrefs.GetString("pref_pinyinType", "marks").Equals("none"))
                        {
                            curView.top.Add("");
                        }
                        else
                        {
                            curView.top.Add(Dict.PinyinToTones(Dict.GetPinyin(entry)));
                        }

                        if (AnnotationActivity.sharedPrefs.GetString("pref_toneColors", "none").Equals("none"))
                        {
                            curView.tones.Add(0);
                        }
                        else
                        {
                            int tones = int.Parse(Regex.Replace(Dict.GetPinyin(entry), @"[\d]", ""));
                            int reverseTones = 0;
                            while (tones != 0)
                            {
                                reverseTones = reverseTones * 10 + tones % 10;
                                tones = tones / 10;
                            }
                            curView.tones.Add(reverseTones);
                        }
                    }
                }

                if (count == 0 || line[count - 1] is string && ((string)line[count - 1]).Length == 0 || context.endPos >= context.textLen && position - 1 == ItemCount - 3)
                {
                    curView.lastLine = true;
                }
                else
                {
                    curView.lastLine = false;
                }

                curView.UpdateVars();

            }
            else
            {
                if (position == 0 && ShowHeader || position == mLines.Count + 1 && ShowFooter)
                {
                    ((ProgressViewHolder)holder).progressBar.Visibility = ViewStates.Visible;
                }
                else
                {
                    ((ProgressViewHolder)holder).progressBar.Visibility = ViewStates.Gone;
                }
            }

        }

        public override int ItemCount
        {
            get
            {
                return mLines.Count + 2;
            }
        }

        public class LineViewHolder : RecyclerView.ViewHolder, View.IOnClickListener
        {
            private AnnListAdapter adapter;

            public LineView mLineView;

            public LineViewHolder(AnnListAdapter adapter, View v) : base(v)
            {
                this.adapter = adapter;
                mLineView = (LineView)v;
                mLineView.SetOnClickListener(this);
            }

            public void OnClick(View view)
            {
                LineView lv = (LineView)view;
                int[] touchedData = new int[2];
                lv.GetTouchedWord(touchedData);
                int lineNum = adapter.context.lines.IndexOf(lv.line);

                if (touchedData[0] == -1 || lv.line[(touchedData[0])] is string || adapter.wPopup.Showing && adapter.context.hlIndex.Y == lineNum && adapter.context.hlIndex.X == touchedData[0])
                {
                    adapter.wPopup.dismiss();
                }
                else
                {
                    adapter.wPopup.show(view, lv.line, touchedData[0], touchedData[1], true);
                    adapter.context.hlIndex.Y = lineNum;
                    adapter.context.hlIndex.X = touchedData[0];
                    lv.Invalidate();
                }
            }
        }

        public class ProgressViewHolder : RecyclerView.ViewHolder, View.IOnClickListener
        {
            private AnnListAdapter adapter;

            public ProgressBar progressBar;

            public ProgressViewHolder(AnnListAdapter adapter, View v) : base(v)
            {
                this.adapter = adapter;
                progressBar = (ProgressBar)v.FindViewById(Resource.Id.progressBar);
                v.SetOnClickListener(this);
            }

            public void OnClick(View view)
            {
                adapter.wPopup.dismiss();
            }
        }
    }
}
