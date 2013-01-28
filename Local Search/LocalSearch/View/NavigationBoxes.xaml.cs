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
            FirstProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            SecondProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            ThirdProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            FourthProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            FifthProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            SixthProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            SeventhProgramElements = new ObservableCollection<ProgramElementWithRelation>();
            InitializeComponent();
        }

        public static readonly DependencyProperty FirstProgramElementsProperty =
                DependencyProperty.Register("FirstProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SecondProgramElementsProperty =
                DependencyProperty.Register("SecondProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty ThirdProgramElementsProperty =
                DependencyProperty.Register("ThirdProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty FourthProgramElementsProperty =
                DependencyProperty.Register("FourthProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty FifthProgramElementsProperty =
                DependencyProperty.Register("FifthProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SixthProgramElementsProperty =
                DependencyProperty.Register("SixthProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SeventhProgramElementsProperty =
                DependencyProperty.Register("SeventhProgramElements", typeof(ObservableCollection<ProgramElementWithRelation>), typeof(NavigationBoxes), new UIPropertyMetadata(null));


        public ObservableCollection<ProgramElementWithRelation> FirstProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(FirstProgramElementsProperty);
            }
            set
            {
                SetValue(FirstProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> SecondProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(SecondProgramElementsProperty);
            }
            set
            {
                SetValue(SecondProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> ThirdProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(ThirdProgramElementsProperty);
            }
            set
            {
                SetValue(ThirdProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> FourthProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(FourthProgramElementsProperty);
            }
            set
            {
                SetValue(FourthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> FifthProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(FifthProgramElementsProperty);
            }
            set
            {
                SetValue(FifthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> SixthProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(SixthProgramElementsProperty);
            }
            set
            {
                SetValue(SixthProgramElementsProperty, value);
            }
        }

        public ObservableCollection<ProgramElementWithRelation> SeventhProgramElements
        {
            get
            {
                return (ObservableCollection<ProgramElementWithRelation>)GetValue(SeventhProgramElementsProperty);
            }
            set
            {
                SetValue(SeventhProgramElementsProperty, value);
            }
        }

        private void ClearGetAndShow(System.Windows.Controls.ListView currentNavigationBox, ObservableCollection<ProgramElementWithRelation> relatedInfo)
        {
            relatedInfo.Clear(); //may triger relatedInfo NavigationBox selection change
            if (currentNavigationBox.SelectedItem != null)
            {
                var relatedmembers = InformationSource.GetRelatedInfo(currentNavigationBox.SelectedItem as ProgramElementWithRelation);
                foreach (var member in relatedmembers)
                {
                    relatedInfo.Add(member);
                }
            }
        }

        private void FirstProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(FirstProgramElementsList, SecondProgramElements);            
        }

        private void SecondProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(SecondProgramElementsList, ThirdProgramElements);
        }

        private void ThirdProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(ThirdProgramElementsList, FourthProgramElements);
        }

        private void FourthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(FourthProgramElementsList, FifthProgramElements);
        }

        private void FifthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(FifthProgramElementsList, SixthProgramElements);
        }

        private void SixthProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearGetAndShow(SixthProgramElementsList, SeventhProgramElements);
        }

        private void SeventhProgramElements_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //todo
        }

        public GraphBuilder InformationSource = null;

    }
}