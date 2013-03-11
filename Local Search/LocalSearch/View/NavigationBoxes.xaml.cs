﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using Sando.ExtensionContracts.Services;
using System.ComponentModel.Composition;
using Sando.DependencyInjection;


namespace LocalSearch.View
{
    /// <summary>
    /// Interaction logic for NavigationBoxes.xaml
    /// </summary>
    public partial class NavigationBoxes : UserControl
    {

        public NavigationBoxes()
        {
            this.DataContext = this;
            FirstProgramElements = new ObservableCollection<CodeSearchResult>();
            SecondProgramElements = new ObservableCollection<CodeNavigationResult>();
            ThirdProgramElements = new ObservableCollection<CodeNavigationResult>();
            FourthProgramElements = new ObservableCollection<CodeNavigationResult>();
            FifthProgramElements = new ObservableCollection<CodeNavigationResult>();
            SixthProgramElements = new ObservableCollection<CodeNavigationResult>();
            SeventhProgramElements = new ObservableCollection<CodeNavigationResult>();
            SelectedElements = new List<CodeSearchResult>();
            InitializeComponent();            
        }

        public NavigationBoxes(ushort heuristic)
        {
            this.DataContext = this;
            FirstProgramElements = new ObservableCollection<CodeSearchResult>();
            SecondProgramElements = new ObservableCollection<CodeNavigationResult>();
            ThirdProgramElements = new ObservableCollection<CodeNavigationResult>();
            FourthProgramElements = new ObservableCollection<CodeNavigationResult>();
            FifthProgramElements = new ObservableCollection<CodeNavigationResult>();
            SixthProgramElements = new ObservableCollection<CodeNavigationResult>();
            SeventhProgramElements = new ObservableCollection<CodeNavigationResult>();
            SelectedElements = new List<CodeSearchResult>();
            rankingHeuristic = heuristic;
            InitializeComponent();
        }


        public void Search(string s)
        {
            try
            {
                InitDte2();
                var searchManager = ServiceLocator.Resolve<ISearchService>();
                InformationSource.query = s; //set context
                var results = searchManager.Search(s);
                FirstProgramElements.Clear();
                foreach (var result in results)
                {   
                    int number = Convert.ToInt32(result.ProgramElement.DefinitionLineNumber);
                    InformationSource.InitialSearchResults.Add(Tuple.Create(result, number)); //set context
                    FirstProgramElements.Add(new CodeNavigationResult(result.ProgramElement, result.Score, InformationSource.GetXElementFromLineNum(number)));
                }
            }
            catch (NullReferenceException e)
            {
                //only in unit tests?
            }
        }

        private ushort rankingHeuristic;

        public static readonly DependencyProperty FirstProgramElementsProperty =
                DependencyProperty.Register("FirstProgramElements", typeof(ObservableCollection<CodeSearchResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SecondProgramElementsProperty =
                DependencyProperty.Register("SecondProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty ThirdProgramElementsProperty =
                DependencyProperty.Register("ThirdProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty FourthProgramElementsProperty =
                DependencyProperty.Register("FourthProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty FifthProgramElementsProperty =
                DependencyProperty.Register("FifthProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SixthProgramElementsProperty =
                DependencyProperty.Register("SixthProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SeventhProgramElementsProperty =
                DependencyProperty.Register("SeventhProgramElements", typeof(ObservableCollection<CodeNavigationResult>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty RelationSequenceProperty =
             DependencyProperty.Register("RelationSequence", typeof(string), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SearchStringProperty =
            DependencyProperty.Register("SearchString", typeof(string), typeof(NavigationBoxes), new UIPropertyMetadata(null));


        public ObservableCollection<CodeSearchResult> FirstProgramElements
        {
            get
            {
                return (ObservableCollection<CodeSearchResult>)GetValue(FirstProgramElementsProperty);
            }
            set
            {
                SetValue(FirstProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> SecondProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(SecondProgramElementsProperty);
            }
            set
            {
                SetValue(SecondProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> ThirdProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(ThirdProgramElementsProperty);
            }
            set
            {
                SetValue(ThirdProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> FourthProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(FourthProgramElementsProperty);
            }
            set
            {
                SetValue(FourthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> FifthProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(FifthProgramElementsProperty);
            }
            set
            {
                SetValue(FifthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> SixthProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(SixthProgramElementsProperty);
            }
            set
            {
                SetValue(SixthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<CodeNavigationResult> SeventhProgramElements
        {
            get
            {
                return (ObservableCollection<CodeNavigationResult>)GetValue(SeventhProgramElementsProperty);
            }
            set
            {
                SetValue(SeventhProgramElementsProperty, value);
            }
        }

        public List<CodeSearchResult> SelectedElements { get; set; }

        public string RelationSequence
        {
            get
            {
                return (string)GetValue(RelationSequenceProperty);
            }
            private set
            {
                SetValue(RelationSequenceProperty, value);
            }
        }

        public string SearchString
        {
            get
            {
                return (string)GetValue(SearchStringProperty);
            }
            private set
            {
                SetValue(SearchStringProperty, value);
            }
        }

        private void ClearGetAndShow(System.Windows.Controls.ListView currentNavigationBox, 
            ObservableCollection<CodeNavigationResult> relatedInfo, int currentPos)
        {

            ClearSelectedElements(currentPos);

            relatedInfo.Clear(); //may triger relatedInfo NavigationBox selection change

            if (currentNavigationBox.SelectedItem != null)
            {
                var relation = currentNavigationBox.SelectedItem as CodeNavigationResult;
                if (relation!=null)
                {   
                    foreach (var linenumber in relation.RelationLineNumber)
                    {
                        if (linenumber > 0)
                        {
                            NavigationBoxes.SelectLine(linenumber);
                        }
                    }
                }
                var selected = currentNavigationBox.SelectedItem as CodeSearchResult;
                SelectedElements.Add(selected);

                RelationSequence = ShowSequenceOfSelects();

                //var relatedmembers = InformationSource.GetRecommendations(selected);
                var relatedmembers = InformationSource.GetBasicRecommendations(selected);
                InformationSource.RankRelatedInfo(ref relatedmembers, rankingHeuristic);
                foreach (var member in relatedmembers)
                {
                    relatedInfo.Add(member);
                }
                                
                
            }
        }

        private void ClearSelectedElements(int currentPos)
        {
            int SelectedNum = SelectedElements.Count;
            if (SelectedNum > currentPos)
            {
                SelectedElements.RemoveRange(currentPos, SelectedNum - currentPos);

                //for (int i = currentPos; i <= (SelectedNum - currentPos); i++)
                //{
                //    CodeNavigationResult programelementInPath = InformationSource.CurrentPath[i] as CodeNavigationResult;
                //    if (programelementInPath != null)
                //        RestoreHistoryScores(ref programelementInPath, currentPos);
                //}

                InformationSource.CurrentPath.RemoveRange(currentPos, SelectedNum - currentPos); // set context
            }
        }

        private void RestoreHistoryScores(ref CodeNavigationResult programelement, 
            int currentPos)
        {
            int lenofhistory = programelement.scorehistory.Count();
            if (lenofhistory > currentPos)
            {
                programelement.Score = programelement.scorehistory[currentPos];
                programelement.scorehistory.RemoveRange(currentPos, lenofhistory - currentPos - 1);
            }
        }

        private String ShowSequenceOfSelects()
        {
            String strbuilder = "";
            InformationSource.CurrentPath.Clear();

            int count = SelectedElements.Count;

            if (count == 0)
                return strbuilder;
            
            CodeSearchResult firstElement = SelectedElements[0];
            String NameOfElement = firstElement.ProgramElement.Name;
            String TypeOfElement = firstElement.ProgramElementType.ToString();
            strbuilder += TypeOfElement + " \"" + NameOfElement + "\" ";

            if (firstElement as CodeNavigationResult != null) //set context
                InformationSource.CurrentPath.Add(firstElement as CodeNavigationResult);

            int i = 1;
            while (i < count)
            {
                CodeSearchResult Element = SelectedElements[i];
                String Name = Element.Name;
                String Type = Element.ProgramElementType.ToString();

                var type = typeof(ProgramElementRelation);

                if (Element as CodeNavigationResult != null)
                {
                    InformationSource.CurrentPath.Add(Element as CodeNavigationResult); //set context

                    var memInfo = type.GetMember(((Element as CodeNavigationResult)).ProgramElementRelation.ToString());
                    var attributes = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute),
                        false);
                    var description = ((DescriptionAttribute)attributes[0]).Description;
                    strbuilder += " --" + description + "--> " + Type + " \"" + Name + "\" ";
                }
                else
                {
                    strbuilder += Name;
                }
                    
                i++;
            }

            return strbuilder.ToString();
        }

        private void FirstProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {            
            ClearGetAndShow(FirstProgramElementsList, SecondProgramElements,0);
        }

        private void SecondProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(SecondProgramElementsList, ThirdProgramElements,1);
        }

        private void ThirdProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(ThirdProgramElementsList, FourthProgramElements,2);
        }

        private void FourthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(FourthProgramElementsList, FifthProgramElements,3);
        }

        private void FifthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(FifthProgramElementsList, SixthProgramElements,4);
        }

        private void SixthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(SixthProgramElementsList, SeventhProgramElements,5);
        }

        private void SeventhProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {            
            RelationSequence = ShowSequenceOfSelects();
        }

        public Context InformationSource = null;
        private static DTE2 dte;

        public void Connect(int connectionId, object target)
        {
            throw new NotImplementedException();
        }

        private static void InitDte2()
        {
            if (dte == null)
            {
                dte = Package.GetGlobalService(typeof(DTE)) as DTE2;                
            }
        }

        public static void SelectLine(int lineNumber)
        {
            InitDte2();
            try
            {
                var selection = (EnvDTE.TextSelection)dte.ActiveDocument.Selection;                
                selection.GotoLine(lineNumber);
                selection.LineDown(false,70);
                selection.GotoLine(lineNumber);
                selection.SelectLine();                
            }
            catch (Exception)
            {
                //ignore, we don't want this feature ever causing a crash
            }

        }

        private void TextBox_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                var terms = searchBox.Text;
                Search(terms);
            }
        }

        private void TextBox_TextChanged_1(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void searchBox_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void FirstProgramElementsList_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(FirstProgramElementsList, SecondProgramElements, 0);
        }

        private void SecondProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(SecondProgramElementsList, ThirdProgramElements, 1);
        }

        private void ThirdProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(ThirdProgramElementsList, FourthProgramElements, 2);
        }

        private void FourthProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(FourthProgramElementsList, FifthProgramElements, 3);
        }

        private void FifthProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(FifthProgramElementsList, SixthProgramElements, 4);
        }

        private void SixthProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClearGetAndShow(SixthProgramElementsList, SeventhProgramElements, 5);
        }

        private void SeventhProgramElements_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RelationSequence = ShowSequenceOfSelects();
        }
    }
}
