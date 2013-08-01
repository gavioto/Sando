﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sando.Core.Logging;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Sando.Indexer.Searching.Criteria;
using Sando.Translation;
using Sando.Recommender;
using FocusTestVC;
using Sando.UI.View.Search;
using Sando.UI.Actions;
using System.Windows.Media;
using Sando.Core.Logging.Events;
using Sando.Indexer.Searching.Metrics;
using System.Windows.Threading;

namespace Sando.UI.View
{
    public partial class SearchViewControl : ISearchResultListener
    {
        public SearchViewControl()
        {
            DataContext = this; //so we can show results
            InitializeComponent();

            _searchManager = new SearchManager(this);
            SearchResults = new ObservableCollection<CodeSearchResult>();
            SearchCriteria = new SimpleSearchCriteria();
            InitAccessLevels();
            InitProgramElements();
            ((INotifyCollectionChanged)searchResultListbox.Items).CollectionChanged += SelectFirstResult;
            ((INotifyCollectionChanged)searchResultListbox.Items).CollectionChanged += ScrollToTop;


            SearchStatus = "Enter search terms - only complete words or partial words followed by a '*' are accepted as input.";

            _recommender = new QueryRecommender();

			_gatheredSearchFeedback = true;
			_gatheredResultFeedback = true;
			_savedClickedResult = null;
			_inactivityStopwatch = new Stopwatch();
        }

        public ObservableCollection<AccessWrapper> AccessLevels
        {
            get { return (ObservableCollection<AccessWrapper>) GetValue(AccessLevelsProperty); }
            set { SetValue(AccessLevelsProperty, value); }
        }


        public ObservableCollection<ProgramElementWrapper> ProgramElements
        {
            get { return (ObservableCollection<ProgramElementWrapper>) GetValue(ProgramElementsProperty); }
            set { SetValue(ProgramElementsProperty, value); }
        }

        public ObservableCollection<CodeSearchResult> SearchResults
        {
            get { return (ObservableCollection<CodeSearchResult>) GetValue(SearchResultsProperty); }
            set { SetValue(SearchResultsProperty, value); }
        }

        public string SearchStatus
        {
            get { return (string) GetValue(SearchStatusProperty); }
            private set { SetValue(SearchStatusProperty, value); }
        }

        public SimpleSearchCriteria SearchCriteria
        {
            get { return (SimpleSearchCriteria) GetValue(SearchCriteriaProperty); }
            set { SetValue(SearchCriteriaProperty, value); }
        }

        public string SearchLabel
        {
            get { return Translator.GetTranslation(TranslationCode.SearchLabel); }
        }

        public string ExpandCollapseResultsLabel
        {
            get { return Translator.GetTranslation(TranslationCode.ExpandResultsLabel); }
        }

        public string ComboBoxItemCurrentDocument
        {
            get { return Translator.GetTranslation(TranslationCode.ComboBoxItemCurrentDocument); }
        }

        public string ComboBoxItemEntireSolution
        {
            get { return Translator.GetTranslation(TranslationCode.ComboBoxItemEntireSolution); }
        }

        private void searchBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (searchBox != null)
            {
                var textBox = searchBox.Template.FindName("Text", searchBox) as TextBox;
                if (textBox != null)
                {
                    TextBoxFocusHelper.RegisterFocus(textBox);
                    textBox.KeyDown += HandleTextBoxKeyDown;
                }

                var listBox = searchBox.Template.FindName("Selector", searchBox) as ListBox;
                if (listBox != null)
                {
                    listBox.SelectionChanged += searchBoxListBox_SelectionChanged;
                }
            }
        }

        private void HandleTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                searchResultListbox.Focus();
                e.Handled = true;
            }
        }

        private void InitProgramElements()
        {
            ProgramElements = new ObservableCollection<ProgramElementWrapper>
                {
                    new ProgramElementWrapper(true, ProgramElementType.Class),
                    new ProgramElementWrapper(false, ProgramElementType.Comment),
                    new ProgramElementWrapper(true, ProgramElementType.Custom),
                    new ProgramElementWrapper(true, ProgramElementType.Enum),
                    new ProgramElementWrapper(true, ProgramElementType.Field),
                    new ProgramElementWrapper(true, ProgramElementType.Method),
                    new ProgramElementWrapper(true, ProgramElementType.MethodPrototype),
                    new ProgramElementWrapper(true, ProgramElementType.Property),
                    new ProgramElementWrapper(true, ProgramElementType.Struct),
                    new ProgramElementWrapper(true, ProgramElementType.XmlElement)
                    //new ProgramElementWrapper(true, ProgramElementType.TextLine)
                };
        }

        private void InitAccessLevels()
        {
            AccessLevels = new ObservableCollection<AccessWrapper>
                {
                    new AccessWrapper(true, AccessLevel.Private),
                    new AccessWrapper(true, AccessLevel.Protected),
                    new AccessWrapper(true, AccessLevel.Internal),
                    new AccessWrapper(true, AccessLevel.Public)
                };
        }

        private void SelectFirstResult(object sender, NotifyCollectionChangedEventArgs e)
        {
            //searchResultListbox.SelectedIndex = 0;
            //searchResultListbox_SelectionChanged(searchResultListbox,null);
            searchResultListbox.SelectedIndex = -1;
            searchResultListbox.Focus();
        }

        private void ScrollToTop(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(searchResultListbox.Items.Count > 0)
                searchResultListbox.ScrollIntoView(searchResultListbox.Items[0]);
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private void SearchButtonClick(object sender, RoutedEventArgs e)
        {
			BeginSearch(searchBox.Text);
        }

        private void OnKeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
				var text = sender as AutoCompleteBox;
                if (text != null)
                {
                    BeginSearch(text.Text);
                }
            }
        }

        private void BeginSearch(string searchString)
        {
            var selectedAccessLevels = AccessLevels.Where(a => a.Checked).Select(a => a.Access).ToList();
            if (selectedAccessLevels.Any())
            {
                SearchCriteria.SearchByAccessLevel = true;
                SearchCriteria.AccessLevels = new SortedSet<AccessLevel>(selectedAccessLevels);
            }
            else
            {
                SearchCriteria.SearchByAccessLevel = false;
                SearchCriteria.AccessLevels.Clear();
            }

            var selectedProgramElementTypes =
                ProgramElements.Where(e => e.Checked).Select(e => e.ProgramElement).ToList();
            if (selectedProgramElementTypes.Any())
            {
                SearchCriteria.SearchByProgramElementType = true;
                SearchCriteria.ProgramElementTypes = new SortedSet<ProgramElementType>(selectedProgramElementTypes);
            }
            else
            {
                SearchCriteria.SearchByProgramElementType = false;
                SearchCriteria.ProgramElementTypes.Clear();
            }

            SearchAsync(searchString, SearchCriteria);
        }

		void inactivityMonitorWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			ResetInactivityStopwatch();
			while(!_gatheredSearchFeedback)
			{
				if(_inactivityStopwatch.ElapsedMilliseconds > (1000 * 5 * 1))
				{
					var uiDispatcher = (Dispatcher)e.Argument;

					if(!_gatheredResultFeedback && _savedClickedResult != null)
					{
						uiDispatcher.BeginInvoke(new Action(() => ShowResultExplicitFeedbackPopup(_savedClickedResult)));
						_gatheredResultFeedback = true;
					}

					uiDispatcher.BeginInvoke(new Action(() => ShowSearchExplicitFeedbackPopup(QueryMetrics.SavedQuery)));
					_gatheredSearchFeedback = true;
				}
				//Thread.Sleep(1000);
			}
		}

        private void SearchAsync(String text, SimpleSearchCriteria searchCriteria)
        {
            var sandoWorker = new BackgroundWorker();
            sandoWorker.DoWork += sandoWorker_DoWork;
            var workerSearchParams = new WorkerSearchParameters { Query = text, Criteria = searchCriteria };
            sandoWorker.RunWorkerAsync(workerSearchParams);
        }

        private class WorkerSearchParameters
        {
            public SimpleSearchCriteria Criteria { get; set; }
            public String Query { get; set; }
        }
      
        void sandoWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var searchParams = (WorkerSearchParameters) e.Argument;
            _searchManager.Search(searchParams.Query, searchParams.Criteria);
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileWithSelectedResult(sender);
        }

        private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OpenFileWithSelectedResult(sender);
            }
        }

        private void OpenFileWithSelectedResult(object sender)
        {
            try
            {
                var result = sender as ListBoxItem;
                if (result != null)
                {
                    var searchResult = result.Content as CodeSearchResult;
                    FileOpener.OpenItem(searchResult, searchBox.Text);

                    var matchDescription = QueryMetrics.DescribeQueryProgramElementMatch(searchResult.ProgramElement, searchBox.Text);
                    LogEvents.OpeningCodeSearchResult(searchResult, SearchResults.IndexOf(searchResult) + 1, matchDescription);

					//FSM role = open in editor
					if(!_gatheredResultFeedback && _savedClickedResult != null)
					{
						ShowResultExplicitFeedbackPopup(_savedClickedResult);
						_gatheredResultFeedback = true;
					}
					ResetInactivityStopwatch();
					_gatheredResultFeedback = false;
					_savedClickedResult = searchResult;
                }
            }
            catch (ArgumentException aex)
            {
                LogEvents.UIGenericError(this, aex);
                MessageBox.Show(FileNotFoundPopupMessage, FileNotFoundPopupTitle, MessageBoxButton.OK);
            }
        }

        public void Update(IQueryable<CodeSearchResult> results)
        {
            if (Thread.CurrentThread == Dispatcher.Thread)
            {
                UpdateResults(results);
            }
            else
            {
                Dispatcher.Invoke((Action) (() => UpdateResults(results)));
            }

			//FSM role = show results
			if(_gatheredSearchFeedback && results.Count() > 0)
			{
				_gatheredSearchFeedback = false;
				var inactivityMonitorWorker = new BackgroundWorker();
				inactivityMonitorWorker.DoWork += inactivityMonitorWorker_DoWork;
				inactivityMonitorWorker.RunWorkerAsync(Dispatcher);
			}
        }

        private void UpdateResults(IEnumerable<CodeSearchResult> results)
        {
            SearchResults.Clear();
            foreach (var codeSearchResult in results)
            {
                SearchResults.Add(codeSearchResult);
            }
        }

        public void UpdateMessage(string message)
        {
            if (Thread.CurrentThread == Dispatcher.Thread)
            {
                SearchStatus = message;
            }
            else
            {
                Dispatcher.Invoke((Action)(() => SearchStatus = message));
            }
        }

        private void UpdateExpansionState(ListView view)
        {
            if (view != null)
            {
                var selectedIndex = view.SelectedIndex;

                if (IsExpandAllChecked())
                {
                    for (var currentIndex = 0; currentIndex < view.Items.Count; ++currentIndex)
                    {
                        var currentItem = view.ItemContainerGenerator.ContainerFromIndex(currentIndex) as ListViewItem;
                        if (currentItem != null)
                            currentItem.Height = 89;
                    }
                }
                else
                {
                    for (var currentIndex = 0; currentIndex < view.Items.Count; ++currentIndex)
                    {
                        var currentItem = view.ItemContainerGenerator.ContainerFromIndex(currentIndex) as ListViewItem;
                        if (currentItem != null)
                            currentItem.Height = currentIndex == selectedIndex ? 89 : 24;
                    }
                }
            }
        }

        private bool IsExpandAllChecked()
        {
            if (expandButton == null)
                return false;
            var check = expandButton.IsChecked;
            return check.HasValue && check == true;
        }

		//FSM role = recommend state
        private void searchBox_Populating(object sender, PopulatingEventArgs e)
        {
			if(!_gatheredResultFeedback && _savedClickedResult != null)
			{
				ShowResultExplicitFeedbackPopup(_savedClickedResult);
				_gatheredResultFeedback = true;
			}
			if(!_gatheredSearchFeedback && QueryMetrics.SavedQuery != String.Empty)
			{
				ShowSearchExplicitFeedbackPopup(QueryMetrics.SavedQuery);
				_gatheredSearchFeedback = true;
			}

            var recommendationWorker = new BackgroundWorker();
            recommendationWorker.DoWork += recommendationWorker_DoWork;
            e.Cancel = true;
            recommendationWorker.RunWorkerAsync(searchBox.Text);
        }

        private void recommendationWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var queryString = (string) e.Argument;

            var result = _recommender.GenerateRecommendations(queryString);
            if (Thread.CurrentThread == Dispatcher.Thread)
            {
                UpdateRecommendations(result, queryString);
            }
            else
            {
                Dispatcher.Invoke((Action) (() => UpdateRecommendations(result, queryString)));
            }
        }

        private void searchResultListbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listview = sender as ListView;
            LogEvents.SelectingCodeSearchResult(this, listview.SelectedIndex + 1);
            UpdateExpansionState(searchResultListbox);

			//FSM role = expand snippet
			if(!_gatheredResultFeedback && _savedClickedResult != null)
			{
				ShowResultExplicitFeedbackPopup(_savedClickedResult);
				_gatheredResultFeedback = true;
			}
			ResetInactivityStopwatch();
        }

        private void Toggled(object sender, RoutedEventArgs e)
        {
            UpdateExpansionState(searchResultListbox);
        }

        private void UpdateRecommendations(IEnumerable<string> recommendations, string query)
        {
            if (query == searchBox.Text)
            {
                searchBox.ItemsSource = recommendations;
                searchBox.PopulateComplete();
            }
            else
            {
                Debug.WriteLine("Query \"{0}\" doesn't match current text \"{1}\"; no update.", query, searchBox.Text);
            }
        }

        public void FocusOnText()
        {
            var textBox = searchBox.Template.FindName("Text", searchBox) as TextBox;
            if (textBox != null)
                textBox.Focus();
        }

        public void ShowProgressBar(bool visible)
        {
            if (Thread.CurrentThread == Dispatcher.Thread)
            {
                ProgBar.Visibility = (visible) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            else
            {
                Dispatcher.Invoke((Action)(() => ProgBar.Visibility = (visible) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed));
            }
        }

        private void searchBoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                listBox.ScrollIntoView(listBox.SelectedItem);
                LogEvents.SelectingRecommendationItem(this, listBox.SelectedIndex + 1);
            }
        }

        private void Toggled_Popup(object sender, RoutedEventArgs e)
        {
            if(!SelectionPopup.IsOpen)
                SelectionPopup.IsOpen = true;
        }


        public static readonly DependencyProperty AccessLevelsProperty =
            DependencyProperty.Register("AccessLevels", typeof (ObservableCollection<AccessWrapper>), typeof (SearchViewControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty ProgramElementsProperty =
            DependencyProperty.Register("ProgramElements", typeof(ObservableCollection<ProgramElementWrapper>), typeof(SearchViewControl), new UIPropertyMetadata(null));


        public static readonly DependencyProperty SearchResultsProperty =
            DependencyProperty.Register("SearchResults", typeof(ObservableCollection<CodeSearchResult>), typeof(SearchViewControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SearchStringProperty =
            DependencyProperty.Register("SearchString", typeof(string), typeof(SearchViewControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SearchStatusProperty =
            DependencyProperty.Register("SearchStatus", typeof(string), typeof(SearchViewControl), new UIPropertyMetadata(null));


        public static readonly DependencyProperty SearchCriteriaProperty =
            DependencyProperty.Register("SearchCriteria", typeof(SimpleSearchCriteria), typeof(SearchViewControl), new UIPropertyMetadata(null));

        private const string FileNotFoundPopupMessage = "This file cannot be opened. It may have been deleted, moved, or renamed since your last search.";
        private const string FileNotFoundPopupTitle = "File opening error";

        private readonly SearchManager _searchManager;
        private readonly QueryRecommender _recommender;

        private void Remove_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = searchResultListbox.SelectedItems[0];
                if (result != null)
                {
                    FileRemover.Remove((result as CodeSearchResult).ProgramElement.FullFilePath, RemoverCompleted);
                }
            }
            catch (ArgumentException aex)
            {
                LogEvents.UIGenericError(this, aex);
            }
        }

        private void RemoverCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SearchButtonClick(null, null);
        }

        private Color Good = (Color)ColorConverter.ConvertFromString("#E9FFDE");
        private Color OK = (Color)ColorConverter.ConvertFromString("#FFFFE6");
        private Color Bad = (Color)ColorConverter.ConvertFromString("#FFF0F0");

        private void RespondToLoad(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = sender as Border;
                var gradientBrush = item.Background as System.Windows.Media.LinearGradientBrush;
                Color myColor = Colors.White;
                var result = item.DataContext as CodeSearchResult;
                if (result != null)
                {
                    double score = result.Score;
                    if (score >= 0.6)
                        myColor = Good;
                    else if (score >= 0.4)
                        myColor = OK;
                    else if (score < 0.4)
                        myColor = Bad;
                    if (score > .99)
                    {
                        foreach (var stop in gradientBrush.GradientStops)
                            stop.Color = myColor;
                    }
                    else
                    {
                        gradientBrush.GradientStops.First().Color = myColor;
                        gradientBrush.GradientStops.ElementAt(1).Color = myColor;
                    }
                }

            }
            catch (Exception problem)
            {
                //ignore for now, as this is not a crucial feature
            }
        }

		private void ShowResultExplicitFeedbackPopup(CodeSearchResult result)
		{
			//do this with probability of 0.33
			Random random = new Random();
            int rand = random.Next(0, 3);
			if(rand == 0)
			{
				ResultExplicitFeedback resultFeedback = new ResultExplicitFeedback(result);
				resultFeedback.ShowDialog();
			}
		}

		private void ShowSearchExplicitFeedbackPopup(string previousQuery)
		{
			SearchExplicitFeedback searchFeedback = new SearchExplicitFeedback(previousQuery);
			searchFeedback.ShowDialog();
		}

		private void ResetInactivityStopwatch()
		{
			if(_inactivityStopwatch.IsRunning)
			{
				_inactivityStopwatch.Reset();
			}
			_inactivityStopwatch.Start();
		}

		private bool _gatheredSearchFeedback;
		private bool _gatheredResultFeedback;
		private CodeSearchResult _savedClickedResult;
		private Stopwatch _inactivityStopwatch;
    }
}