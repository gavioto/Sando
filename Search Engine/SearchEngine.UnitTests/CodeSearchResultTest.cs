﻿using NUnit.Framework;
using Sando.ExtensionContracts.ResultsReordererContracts;

namespace Sando.SearchEngine.UnitTests
{
    [TestFixture]
    public class CodeSearchResultTest
    {
        [TestCase]
        public void FixSnipTabTest()
        {
            var stuff = "	public void yo()\n		sasdfsadf\n		asdfasdf\n";
            string fixSnip = CodeSearchResult.SourceToSnippet(stuff, CodeSearchResult.DefaultSnippetSize);
            Assert.IsTrue(fixSnip.Equals("public void yo()\n\tsasdfsadf\n\tasdfasdf\n"));
        }

        [TestCase]
        public void FixSnipSpacesTest()
        {
            var stuff = "      public void yo()\n            sasdfsadf\n            asdfasdf\n";
			string fixSnip = CodeSearchResult.SourceToSnippet(stuff, CodeSearchResult.DefaultSnippetSize);
            Assert.IsTrue(fixSnip.Equals("public void yo()\n      sasdfsadf\n      asdfasdf\n"));
        }
    }
}
