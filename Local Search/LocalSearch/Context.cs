﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.ExtensionContracts.ResultsReordererContracts;
using ABB.SrcML;
using System.Xml;
using System.Xml.Linq;

namespace Sando.LocalSearch
{
    public class Context 
    {
        private GraphBuilder graph;
        private string filePath;
        private CodeSearchResult _currentElement;
        private bool _isInitialized = false;

        public String query { set;  get; }

        //TODO: int -- line number on which the query or its extension is satisfied 
        //       (only the highest score result?) 
        public List<Tuple<CodeSearchResult, int>> InitialSearchResults { set; get; }

        public List<CodeSearchResult> CurrentPath
        {
            set; //when the user makes a new selection in the UI, set it
            get;
        }
 
        public List<List<CodeNavigationResult>> droppedPaths
        {
            set; //when the user drop a path (by returning to an ealier state), set it
            get;
        }
        
        public Context()
        {
            CurrentPath = new List<CodeSearchResult>();
            droppedPaths = new List<List<CodeNavigationResult>>();
            InitialSearchResults = new List<Tuple<CodeSearchResult,int>>();
        }

        public Context(String searchQuery)            
        {
            query = searchQuery;
            CurrentPath = new List<CodeSearchResult>();
            droppedPaths = new List<List<CodeNavigationResult>>();
            InitialSearchResults = new List<Tuple<CodeSearchResult, int>>();
        }

        public void Intialize(string srcPath, string srcMLPath = null)
        {
            graph = new GraphBuilder(srcPath, srcMLPath);
            graph.Initialize();
            filePath = srcPath;
            _isInitialized = true;
        }

        public void RankRelatedInfo(ref List<CodeNavigationResult> RelatedProgramElements, 
            UInt16 heuristic = 4)
        {
            if (RelatedProgramElements.Count() == 0)
                return;
            
            //restore scores
            foreach (var programelement in RelatedProgramElements)
                programelement.Score = 1.0;

            //score setting
            switch (heuristic)
            {
                case 1: //basic (default) heuristic
                    {
                        BasicHeuristic(ref RelatedProgramElements);
                        UseLocationHeuristic(ref RelatedProgramElements);
                        break;
                    }
                case 2: //comprehensive heuristics
                    {
                        CodeSearchResult lastSelectedProgramElement = CurrentPath[CurrentPath.Count() - 1];
                        BasicHeuristic(ref RelatedProgramElements);
                        TopologyHeuristic(lastSelectedProgramElement, ref RelatedProgramElements, 1);
                        EditDistanceHeuristicInPath(ref RelatedProgramElements, 1, 1);
                        UseLocationHeuristic(ref RelatedProgramElements);
                        break;
                    }
                case 3: //partially selected heuristics 
                    {
                        CodeSearchResult lastSelectedProgramElement = CurrentPath[CurrentPath.Count() - 1];
                        EditDistanceHeuristicInPath(ref RelatedProgramElements, 2, 1);
                        ShowBeforeHeuristic(ref RelatedProgramElements);
                        //UseLocationHeuristic(ref RelatedProgramElements);
                        break;
                    }
                case 4: //no heuristics
                    {   
                        UseLocationHeuristic(ref RelatedProgramElements);
                        break;
                    }
                case 5: //single heuristic
                    {
                        ShowBeforeHeuristic(ref RelatedProgramElements);
                        break;
                    }
                case 6: //single heuristic
                    {
                        AmongInitialSearchResultsHeuristic(ref RelatedProgramElements, 2, 1);
                        break;
                    }
                case 7: //single heuristic
                    {
                        EditDistanceHeuristicInPath(ref RelatedProgramElements, 2, 1);
                        break;
                    }
                case 8: //single heuristic
                    {
                        CodeSearchResult lastSelectedProgramElement = CurrentPath[CurrentPath.Count() - 1];
                        TopologyHeuristic(lastSelectedProgramElement, ref RelatedProgramElements, 1);
                        break;
                    }
                default:
                    break;
            }

            //backup current scores in current path
            //for (int i = 0; i < RelatedProgramElements.Count() - 1; i++)
            //{
            //    CodeNavigationResult temp = RelatedProgramElements[i];
            //    temp.scorehistory.Add(temp.Score);
            //}

            //bubble ranking
            for (int i = 0; i < RelatedProgramElements.Count() - 1; i++)
                for (int j = i + 1; j < RelatedProgramElements.Count(); j++)
                {
                    if (RelatedProgramElements[j].Score > RelatedProgramElements[i].Score)
                    {
                        CodeNavigationResult temp = RelatedProgramElements[j];
                        RelatedProgramElements[j] = RelatedProgramElements[i];
                        RelatedProgramElements[i] = temp;
                    }
                }
        }

        public void RankRelatedInfoWithWeights(ref List<CodeNavigationResult> RelatedProgramElements,
            bool decayOn, double ShowBeforeWeight,
            int searchResultsLookahead, double AmongSearchResWeight, 
            double TopologyWeight, 
            int editDistanceLookback, double EditDistanceWeight)
        {
            if (RelatedProgramElements.Count() == 0)
                return;

            //restore scores
            foreach (var programelement in RelatedProgramElements)
                programelement.Score = 1.0;

            //score setting
            CodeSearchResult lastSelectedProgramElement = CurrentPath[CurrentPath.Count() - 1];
            ShowBeforeHeuristic(ref RelatedProgramElements, ShowBeforeWeight, decayOn);
            AmongInitialSearchResultsHeuristic(ref RelatedProgramElements, searchResultsLookahead, AmongSearchResWeight);
            TopologyHeuristic(lastSelectedProgramElement, ref RelatedProgramElements, TopologyWeight);
            EditDistanceHeuristicInPath(ref RelatedProgramElements, editDistanceLookback, EditDistanceWeight);
            //UseLocationHeuristic(ref RelatedProgramElements);                        

            //bubble ranking
            for (int i = 0; i < RelatedProgramElements.Count() - 1; i++)
                for (int j = i + 1; j < RelatedProgramElements.Count(); j++)
                {
                    if (RelatedProgramElements[j].Score > RelatedProgramElements[i].Score)
                    {
                        CodeNavigationResult temp = RelatedProgramElements[j];
                        RelatedProgramElements[j] = RelatedProgramElements[i];
                        RelatedProgramElements[i] = temp;
                    }
                }
        }

        #region ranking heuristics
                 
        private void BasicHeuristic(ref List<CodeNavigationResult> RelatedProgramElements)
        {
            ShowBeforeHeuristic(ref RelatedProgramElements);

            AmongInitialSearchResultsHeuristic(ref RelatedProgramElements, 1, 1);
        }

        private void ShowBeforeHeuristic(ref List<CodeNavigationResult> RelatedProgramElements, double weight = 1, bool decay = false)
        {
            int pathLen = CurrentPath.Count();
            if (pathLen == 0)
                return;

            foreach (var relatedProgramElement in RelatedProgramElements)
            {
                int location = pathLen -1;

                //what has shown before is set lower score
                if (ExistingInCurrentPath(CurrentPath, relatedProgramElement, out location))
                {
                    if (!decay)
                        relatedProgramElement.Score = relatedProgramElement.Score - 1;
                    else
                    {
                        int temp = 2 ^ (pathLen - location - 1);
                        double decayfactor = Convert.ToDouble(1 / temp);
                        relatedProgramElement.Score = relatedProgramElement.Score - (1 * decayfactor) * weight;
                    }
                }
                
            }
        }

        private void AmongInitialSearchResultsHeuristic(ref List<CodeNavigationResult> RelatedProgramElements, int steps, double baseweight)
        {
            if (InitialSearchResults.Count() == 0)
                return;

            List<double> weights = new List<double>();
            weights.Add(1);
            for (int i = 1; i <= steps; i++)
            {
                int temp = 2 ^ i;
                weights.Add(1 / Convert.ToDouble(temp));
            }
            //NormalizeScoreBySum(ref weights);

            List<double> listOfAbsScores = new List<double>();
            for (int i = 0; i < RelatedProgramElements.Count; i++)
                listOfAbsScores.Add(0);

            Dictionary<CodeNavigationResult, List<CodeNavigationResult>> relatedElementsInSteps
                = new Dictionary<CodeNavigationResult, List<CodeNavigationResult>>();

            foreach (var relatedProgramElement in RelatedProgramElements)
            {
                List<CodeNavigationResult> relatedElements = new List<CodeNavigationResult>();
                relatedElements.Add(relatedProgramElement); // add itself
                relatedElementsInSteps[relatedProgramElement] = relatedElements;
            }

            //AmongInitialSearchResultsByStep(ref RelatedProgramElements, relatedElementsInSteps, weights[0]*baseweight);
            AmongInitialSearchResultsByStep(ref RelatedProgramElements, relatedElementsInSteps, weights[0], ref listOfAbsScores);

            for (int i = 1; i <= steps; i++)
            {
                foreach (var relatedProgramElement in RelatedProgramElements)
                {
                    List<CodeNavigationResult> relatedElements = null;
                    relatedElementsInSteps.TryGetValue(relatedProgramElement, out relatedElements);
                    if (relatedElements == null) continue;
                    int NumOfElements = relatedElements.Count();
                    for (int j = 0; j < NumOfElements; j++)
                    {
                        CodeNavigationResult origRelatedElement = relatedElements[0];
                        List<CodeNavigationResult> newRelatedElements =
                            GetBasicRecommendations(origRelatedElement as CodeSearchResult);
                        relatedElementsInSteps[relatedProgramElement].AddRange(newRelatedElements.ToArray());
                        relatedElementsInSteps[relatedProgramElement].RemoveAt(0);
                    }
                }

                //AmongInitialSearchResultsByStep(ref RelatedProgramElements, relatedElementsInSteps, weights[i]*baseweight);
                AmongInitialSearchResultsByStep(ref RelatedProgramElements, relatedElementsInSteps, weights[i], ref listOfAbsScores);
            }

            // Normalize Final Score by Max
            NormalizeScoreByMax(ref listOfAbsScores);
            int cnt = 0;
            foreach (var element in RelatedProgramElements)
            {
                element.Score += baseweight * listOfAbsScores[cnt];
                cnt++;
            }
        }

        private void AmongInitialSearchResultsByStep(ref List<CodeNavigationResult> RelatedProgramElements,
            Dictionary<CodeNavigationResult, List<CodeNavigationResult>> RelatedProgramElementsInSteps,
            double weight)
        {
            foreach (var relatedProgramElement in RelatedProgramElements)
            {
                //what is more closer related to search result is set higher score
                List<CodeNavigationResult> relatedElementsInSteps = null;
                RelatedProgramElementsInSteps.TryGetValue(relatedProgramElement, out relatedElementsInSteps);
                if (relatedElementsInSteps == null)
                    continue;
                foreach (var relatedElement in relatedElementsInSteps)
                {
                    double searchScore = ExistingInInitialSearchRes(InitialSearchResults, relatedElement);
                    if (searchScore > 0) //this is always true
                    {
                        //relatedProgramElement.Score = relatedProgramElement.Score + 0.1 * weight;
                        //if (searchScore > 1)
                        //    relatedProgramElement.Score += (searchScore - 1) * weight;
                        relatedProgramElement.Score += searchScore * weight;
                    }
                }
            }
        }

        private void AmongInitialSearchResultsByStep(ref List<CodeNavigationResult> RelatedProgramElements,
            Dictionary<CodeNavigationResult, List<CodeNavigationResult>> RelatedProgramElementsInSteps,
            double weight, ref List<double> rankingScores)
        {
            int cnt = 0;
            foreach (var relatedProgramElement in RelatedProgramElements)
            {
                //what is more closer related to search result is set higher score
                List<CodeNavigationResult> relatedElementsInSteps = null;
                RelatedProgramElementsInSteps.TryGetValue(relatedProgramElement, out relatedElementsInSteps);
                if (relatedElementsInSteps == null)
                {
                    cnt++;
                    continue;
                }

                foreach (var relatedElement in relatedElementsInSteps)
                {
                    double searchScore = ExistingInInitialSearchRes(InitialSearchResults, relatedElement);
                    if (searchScore > 0) 
                    {
                        try
                        {
                            rankingScores[cnt] += searchScore * weight;
                        }
                        catch(Exception e)
                        {
                           //do nothing
                        }
                    }                   
                }

                cnt++;
            }
        }

        private bool ExistingInCurrentPath(List<CodeSearchResult> source, CodeNavigationResult target, out int location)
        {
            int index = source.Count() -1;

            //if it's an declaration, treated differently
            if (target.ProgramElementRelation == ProgramElementRelation.Other)
            {
                //foreach (var programelement in source)
                for (location = index; location >= 0; location--)
                {
                    var programelement = source[location];
                    if (programelement.Name.Equals(target.Name)
                    && programelement.ProgramElementType.Equals(target.ProgramElementType)
                    && programelement.ProgramElement.DefinitionLineNumber.Equals(target.ProgramElement.DefinitionLineNumber)
                    && ((programelement as CodeNavigationResult == null) ||
                    ((programelement as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.Other))))
                        return true;                    
                }

                return false;
            }
            else
            {
                //foreach (var eachSelectedElement in source)
                for (location = index; location >= 0; location--)
                {
                    var eachSelectedElement = source[location];
                    if (eachSelectedElement.Name.Equals(target.Name)
                    && eachSelectedElement.ProgramElementType.Equals(target.ProgramElementType)
                    && eachSelectedElement.ProgramElement.DefinitionLineNumber.Equals(target.ProgramElement.DefinitionLineNumber))
                        return true;                    
                }

                return false;
            }  
            
        }

        private double ExistingInInitialSearchRes(List<Tuple<CodeSearchResult,int>> source, CodeNavigationResult target)
        {
            double res = 0;

            foreach (var searchresult in source)
            {
                var programelement = searchresult.Item1;
                if (programelement.Name.Equals(target.Name)
                  && programelement.ProgramElementType.Equals(target.ProgramElementType)
                  && programelement.ProgramElement.DefinitionLineNumber.Equals(target.ProgramElement.DefinitionLineNumber))
                    return programelement.Score;
            }


            return res;  
        }
        
        private void TopologyHeuristic(CodeSearchResult sourceProgramElement,
            ref List<CodeNavigationResult> RelatedProgramElements, double baseweight)
        {
            double numberOfCallers = 0;
            double numberOfCalls = 0;
            double numberOfUsers = 0;
            double numberOfUses = 0;
            double FixedNumerator = 1;
            CodeSearchResult sourceAsCodeSearchRes = sourceProgramElement as CodeSearchResult;

            List<double> listOfDegree = new List<double>();

            if (sourceProgramElement.ProgramElementType == ProgramElementType.Field)
            {
                numberOfUsers = graph.GetFieldUsers(sourceAsCodeSearchRes).Count();
                foreach (var relatedProgramElement in RelatedProgramElements)
                {
                    double degree = 0;

                    if (relatedProgramElement.ProgramElementRelation == ProgramElementRelation.Use)
                    {
                        numberOfUses = graph.GetFieldUses(relatedProgramElement as CodeSearchResult).Count();
                        degree = (FixedNumerator / numberOfUsers) * (FixedNumerator / numberOfUses);
                    }
                    
                    listOfDegree.Add(degree);
                }
            }

            if (sourceProgramElement.ProgramElementType == ProgramElementType.Method)
            {
                numberOfCallers = graph.GetCallers(sourceAsCodeSearchRes).Count();
                numberOfCalls = graph.GetCallees(sourceAsCodeSearchRes).Count();
                numberOfUses = graph.GetFieldUses(sourceAsCodeSearchRes).Count();
                foreach (var relatedProgramElement in RelatedProgramElements)
                {
                    double degree = 0;
                    ProgramElementRelation relation = relatedProgramElement.ProgramElementRelation;
                    if (relation == ProgramElementRelation.Call)
                    {
                        double NumOfCall = graph.GetCallees(relatedProgramElement as CodeSearchResult).Count();
                        degree = (FixedNumerator / numberOfCallers) * (FixedNumerator / NumOfCall);
                    }

                    if (relation == ProgramElementRelation.CallBy)
                    {
                        double NumOfCaller = graph.GetCallers(relatedProgramElement as CodeSearchResult).Count();
                        degree = (FixedNumerator / numberOfCalls) * (FixedNumerator / NumOfCaller);
                    }

                    if (relation == ProgramElementRelation.UseBy)
                    {
                        double NumOfUser = graph.GetFieldUsers(relatedProgramElement as CodeSearchResult).Count();
                        degree = (FixedNumerator / numberOfUses) * (FixedNumerator / NumOfUser);
                    }

                    //else declaration
                    listOfDegree.Add(degree);
                }
                    
            }

            NormalizeScoreByMax(ref listOfDegree);

            for (int i = 0; i < RelatedProgramElements.Count(); i++)
                RelatedProgramElements[i].Score += listOfDegree[i] * baseweight;            
        }


        private void EditDistanceHeuristicInPath(ref List<CodeNavigationResult> relatedProgramElements, 
            int steps, double baseweight)
        {
            if(CurrentPath.Count < steps)
                steps = CurrentPath.Count;
            
            List<double> listOfDegree = new List<double>();
            listOfDegree.Add(1);
            for (int i = 2; i <= steps; i++)
            {
                int temp = 2 ^ (i-1);
                listOfDegree.Add(1 / Convert.ToDouble(temp));
            }
           // NormalizeScoreBySum(ref listOfDegree);

            List<double> listOfAbsScores = new List<double>();
            for (int i = 0; i < relatedProgramElements.Count; i++)
                listOfAbsScores.Add(0);

            for (int i = 1; i <= steps; i++)
            {
                CodeSearchResult ProgramElementToCompare = CurrentPath[CurrentPath.Count - i];
                double absoluteweight = listOfDegree[i - 1] * baseweight;
                //EditDistanceHeuristic(ProgramElementToCompare, ref relatedProgramElements, absoluteweight);
                EditDistanceHeuristic(ProgramElementToCompare, ref relatedProgramElements, listOfDegree[i - 1], ref listOfAbsScores);
            }

            // Normalize Final Score by Max
            NormalizeScoreByMax(ref listOfAbsScores);
            int cnt = 0;
            foreach (var element in relatedProgramElements)
            {
                element.Score += baseweight * listOfAbsScores[cnt];
                cnt++;
            }
        }


        private void EditDistanceHeuristic(CodeSearchResult ProgramElementToCompare, 
            ref List<CodeNavigationResult> relatedProgramElements, double weight)
        {
            List<double> listOfDegree = new List<double>();

            foreach (var relatedelement in relatedProgramElements)
            {
                double degree = 0;

                //if (relatedelement.ProgramElementRelation != ProgramElementRelation.Other)
                //{
                //    double distance = LevenshteinDistance(relatedelement.Name, ProgramElementToCompare.Name);
                    
                //    if (distance == 0) //very little chance to hit
                //        degree = 2.1; //double.MaxValue;
                //    else
                //    {
                //        if (PartialWordMatch(relatedelement.Name, ProgramElementToCompare.Name))
                //            degree = 1;
                //        degree += 1 / distance;
                //    }
                //}

                //if (PartialWordMatch(relatedelement.Name, ProgramElementToCompare.Name))
                //    degree = 1;

                double distance = LevenshteinDistance(relatedelement.Name, ProgramElementToCompare.Name);
                if (distance == 0) //very little chance to hit
                    degree += 2; 
                else
                    degree += 1 / distance;

                listOfDegree.Add(degree);
            }

            NormalizeScoreByMax(ref listOfDegree);

            for (int i = 0; i < relatedProgramElements.Count(); i++)
                relatedProgramElements[i].Score += listOfDegree[i] * weight;
        }

        private void EditDistanceHeuristic(CodeSearchResult ProgramElementToCompare,
            ref List<CodeNavigationResult> relatedProgramElements, double weight, ref List<double> rankingScores)
        {
            int cnt = 0;
            foreach (var relatedelement in relatedProgramElements)
            {
                double degree = 0;

                if (PartialWordMatch(relatedelement.Name, ProgramElementToCompare.Name))
                    degree = 1;

                double distance = LevenshteinDistance(relatedelement.Name, ProgramElementToCompare.Name);
                if (distance == 0) //very little chance to hit
                    degree += 2;
                else
                    degree += 1 / distance;

                //listOfDegree.Add(degree);
                rankingScores[cnt] += degree * weight;
                cnt++;
            }

            //Assert: cnt == relatedProgramElements.Count()

        }

        private bool PartialWordMatch(string target, string source)
        {
            //source may be multiple words
            string[] sources = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var originalWord in sources)
            {
                Stemmer s = new Stemmer();
                s.add(originalWord.ToCharArray(), originalWord.Length);
                s.stem();
                string stemSource = new string(s.getResultBuffer());

                Stemmer t = new Stemmer();
                t.add(target.ToCharArray(), target.Length);
                t.stem();
                String stemTarget = new string(t.getResultBuffer());

                if (stemTarget.Contains(stemSource) || stemSource.Contains(stemTarget))
                    return true;
            }

            return false;
        }

        public int LevenshteinDistance(String s, String t)
        {
            int n = s.Length;
	        int m = t.Length;
	        int[,] d = new int[n + 1, m + 1];

	        if (n == 0)
	        {
	            return m;
	        }

	        if (m == 0)
	        {
	            return n;
	        }

	        for (int i = 0; i <= n; d[i, 0] = i++)
	        {
	        }

	        for (int j = 0; j <= m; d[0, j] = j++)
	        {
	        }

	        for (int i = 1; i <= n; i++)
	        {
	             for (int j = 1; j <= m; j++)
	            {
		            int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

		            d[i, j] = Math.Min(
		                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
		                d[i - 1, j - 1] + cost);
	            }
        	}
            
	        return d[n, m];
    
        }

        private void UseLocationHeuristic(ref List<CodeNavigationResult> relatedProgramElements)
        {
            foreach (var relatedProgramElement in relatedProgramElements)
            {
                int relationLine = relatedProgramElement.RelationLineNumber[0];
                relatedProgramElement.Score += 1 / Convert.ToDouble(relationLine) * 0.0001;
            }
        }
             
        private void NormalizeScoreByMax(ref List<double> scores)
        {
            double maxscore = scores.Max();
            if (maxscore == 0)
                return;

            List<double> normalizedscores = new List<double>();
            foreach (var score in scores)
                normalizedscores.Add(score / maxscore);
            scores = normalizedscores;
        }

        private void NormalizeScoreBySum(ref List<double> scores)
        {
            double sumscore = scores.Sum();
            if (sumscore == 0)
                return;

            List<double> normalizedscores = new List<double>();
            foreach (var score in scores)
                normalizedscores.Add(score / sumscore);
            scores = normalizedscores;
        }

        private void ContextHeuristic(ref List<CodeNavigationResult> relatedProgramElements)
        {
            for(int i=0; i<relatedProgramElements.Count; i++)
            {
                CodeNavigationResult relatedprogramelement = relatedProgramElements[i];
                double score = 0;
                if (relatedProgramElements[i].ProgramElementType == ProgramElementType.Field)
                    score = FieldContextHeuristic(relatedprogramelement);
                else
                    score = MethodContextHeuristic(relatedprogramelement);

                relatedProgramElements[i].Score += score;
            }
        }

        private double FieldContextHeuristic(CodeNavigationResult relatedFieldElement)
        {
            double res = 0;    
        
            //if the field Type is somehow matching the query
            string classname = (relatedFieldElement.ProgramElement as FieldElement).FieldType;
            //XElement fielddeclaration = graph.GetField(relatedFieldElement.ProgramElement as FieldElement); 
            //string classname = fielddeclaration.Element(SRC.Type).ToString();
            if (PartialWordMatch(classname, query))
                res += 1;

            //if the field use is like "field.X" and X somehow matches the query
            XElement relationcode = relatedFieldElement.RelationCode;

            return res;
        }

        private double MethodContextHeuristic(CodeNavigationResult relatedMethodElement)
        {
            double res = 0;
            XElement relationcode = relatedMethodElement.RelationCode;

            return res;
        }

        #endregion ranking heuristics

        #region public APIs
        public IEnumerable<CodeSearchResult> GetRecommendations()
        {
            var methods = graph.GetMethodsAsMethodElements();
            methods.AddRange(graph.GetFieldsAsFieldElements());

            return methods;
        }


        public List<CodeNavigationResult> GetBasicRecommendations(CodeSearchResult codeSearchResult)
        {
            ProgramElementType elementtype = codeSearchResult.ProgramElementType;
            List<CodeNavigationResult> recommendations = new List<CodeNavigationResult>();

            if (elementtype.Equals(ProgramElementType.Field))
                recommendations = GetFieldRelatedInfo(codeSearchResult);
            else // if(elementtype.Equals(ProgramElementType.Method))
                recommendations = GetMethodRelatedInfo(codeSearchResult);

            return recommendations;
        }
        
        public List<CodeNavigationResult> GetRecommendations(CodeSearchResult codeSearchResult)
        {            
            List<CodeNavigationResult> recommendations = GetBasicRecommendations(codeSearchResult);

            RankRelatedInfo(ref recommendations, 2);

            return recommendations;
        }

        public List<CodeNavigationResult> GetRecommendations(CodeSearchResult codeSearchResult,
            bool decay, double showBeforeW,
            int searchResLookahead, double AmongSearchResW, 
            double TopologyW,
            int editDistanceLookback, double EditDistanceW)
        {
            List<CodeNavigationResult> recommendations = GetBasicRecommendations(codeSearchResult);

            RankRelatedInfoWithWeights(ref recommendations, decay, showBeforeW, searchResLookahead, AmongSearchResW,
                TopologyW, editDistanceLookback, EditDistanceW);

            return recommendations;
        }

        private List<CodeNavigationResult> GetFieldRelatedInfo(CodeSearchResult codeSearchResult)
        {
            List<CodeNavigationResult> listFiledRelated
                = new List<CodeNavigationResult>();
            String fieldname = codeSearchResult.Name;

            //relation 0: get the decl of itself
            if ((codeSearchResult as CodeNavigationResult) == null //direct search result (first column)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.Use)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.Call)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.CallBy)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.UseBy))
            {
                //var fieldDeclaration = GetFieldDeclFromName(codeSearchResult.Element.Name);
                var fieldDeclaration = graph.GetField(codeSearchResult.ProgramElement as FieldElement);
                listFiledRelated.Add(XElementToProgramElementConverter.GetFieldElementWRelationFromDecl(fieldDeclaration, filePath));
            }

            //relation 1: get methods that use this field (if there are in the same class)
            //listFiledRelated.AddRange(GetMethodElementsUseField(fieldname));
            listFiledRelated.AddRange(graph.GetFieldUsers(codeSearchResult));

            //there may be other relations that will be considered in the future
            // todo

            return listFiledRelated;

        }

        private List<CodeNavigationResult> GetMethodRelatedInfo(CodeSearchResult codeSearchResult)
        {
            List<CodeNavigationResult> listMethodRelated
                = new List<CodeNavigationResult>();

            //relation 0: get the decl of itself
            if ((codeSearchResult as CodeNavigationResult) == null
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.Use)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.Call)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.CallBy)
                || (codeSearchResult as CodeNavigationResult).ProgramElementRelation.Equals(ProgramElementRelation.UseBy))
            {
                var methodDeclaration = graph.GetMethod(codeSearchResult.ProgramElement as MethodElement);
                listMethodRelated.Add(XElementToProgramElementConverter.GetMethodElementWRelationFromXElement(methodDeclaration,filePath));
            }

            String methodname = codeSearchResult.Name;
            int srcLineNumber = codeSearchResult.ProgramElement.DefinitionLineNumber;

            //var method = GetFullMethodFromName(methodname, srcLineNumber);
            //Contract.Requires((method != null), "Method "+ methodname + " does not belong to this local file.");

            //relation 1: get methods that are called by this method (callees)
            listMethodRelated.AddRange(graph.GetCallees(codeSearchResult));

            //relation 2: get fields that are used by this method
            //listMethodRelated.AddRange(GetFieldElementsUsedinMethod(method));
            listMethodRelated.AddRange(graph.GetFieldUses(codeSearchResult));

            //relation 3: get methods that call this method (callers)
            listMethodRelated.AddRange(graph.GetCallers(codeSearchResult));

            return listMethodRelated;
        }
        
        public XElement GetXElementFromLineNum(int number)
        {
            return graph.GetXElementFromLineNum(number);
        }

        public ProgramElementRelation GetRelation(CodeSearchResult searchRes1, CodeSearchResult searchRes2, ref List<int> UsedLineNumber)
        {
            return graph.GetRelation(searchRes1, searchRes2, ref UsedLineNumber);
        }
        
          public Context Copy()
        {
            return this;
        }

        
        #endregion public APIs

          public bool IsInitialized()
          {
              return _isInitialized;
          }
    }
}
