﻿using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.Indexer.Searching.Criteria;
using Sando.UI.InterleavingExperiment;

namespace Sando.UI.UnitTests.PairedInterleaving
{
    [TestFixture]
    class PairedInterleavingTest
    {
        private List<CodeSearchResult> _sandoResults;
        private List<CodeSearchResult> _lexResults;
        private List<CodeSearchResult> _interleavedResults;
        private List<int> _clickIdx;

        [TestFixtureSetUp]
        public void SetupExperiment()
        {
            var a = new CodeSearchResult(new CommentElement("a", 1, "abcd.cs", "a", "a"), 1);
            var b = new CodeSearchResult(new CommentElement("b", 1, "abcd.cs", "b", "b"), 1);
            var c = new CodeSearchResult(new CommentElement("c", 1, "abcd.cs", "c", "c"), 1);
            var d = new CodeSearchResult(new CommentElement("d", 1, "abcd.cs", "d", "d"), 1);

            _sandoResults = new List<CodeSearchResult> {a, b, c};
        	_lexResults = new List<CodeSearchResult> {b, d, c};
            _interleavedResults = BalancedInterleaving.Interleave(_sandoResults, _lexResults); 
            _clickIdx = new List<int> {_interleavedResults.IndexOf(b), _interleavedResults.IndexOf(c)};
        }

        [Test]
        public void TestBalancedInterleaving()
        {
            List<CodeSearchResult> interleaving = BalancedInterleaving.Interleave(_sandoResults, _lexResults);
            Assert.AreEqual(interleaving.Count, 4);
            if (interleaving[0] == _sandoResults[0])
            {
                //b,d,c
                Assert.IsTrue(interleaving[1] == _lexResults[0]);
                Assert.IsTrue(interleaving[2] == _lexResults[1]);
                Assert.IsTrue(interleaving[3] == _sandoResults[2]);
            }
            else if (interleaving[0] == _lexResults[0])
            {
                //a,d,c
                Assert.IsTrue(interleaving[1] == _sandoResults[0]);
                Assert.IsTrue(interleaving[2] == _lexResults[1]);
                Assert.IsTrue(interleaving[3] == _sandoResults[2]);
            }
            else
            {
                Assert.Fail();
            }
        }

        [Test]
        public void TestDetermineWinner()
        {
            int scoreA, scoreB;
            BalancedInterleaving.DetermineWinner(_sandoResults, _lexResults, _interleavedResults, 
                                        _clickIdx, out scoreA, out scoreB);
            Assert.AreEqual(scoreA, 0);
            Assert.AreEqual(scoreB, 1);
        }

        [Test]
        public void TestLex()
        {
            var tuple = LexSearch.GetCriteria(@"C:\Users\USDASHE1\Documents\VsProjects\Sando-clone\Indexer\Indexer\IndexState\CppHeaderElementResolver.cs(20):			//first parse all the included header files. they are the same in all the unresolved elements");
            Assert.IsTrue(tuple.Item2==20);
            Assert.IsTrue((tuple.Item1 as SimpleSearchCriteria).Locations.First().Equals(@"C:\Users\USDASHE1\Documents\VsProjects\Sando-clone\Indexer\Indexer\IndexState\CppHeaderElementResolver.cs"));
        }

    }
}