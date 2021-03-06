﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Sando.Core.Tools;

namespace Sando.Core.UnitTests.Tools
{
    [TestFixture]
    class BigMatrixTest
    {
        public IWordCoOccurrenceMatrix matrix;

        public BigMatrixTest()
        {
            this.matrix = new SparseCoOccurrenceMatrix();
        }

        [SetUp]
        public void ReadData()
        {
            matrix.Initialize(@"TestFiles\");
        }

        private void AssertWordPairExist(string word1, string word2)
        {
            Assert.IsTrue(matrix.GetCoOccurrenceCount(word1, word2) > 0);
        }

        private void AssertWordPairNonExist(string word1, string word2)
        {
            Assert.IsTrue(matrix.GetCoOccurrenceCount(word1, word2) == 0);
        }

        [Test]
        public void SameLocalDictionaryWordPairAlwaysExist()
        {
            AssertWordPairExist("sando", "sando");
            AssertWordPairExist("abb", "abb");
            AssertWordPairExist("test", "test");
        }

        [Test]
        public void SameNonLocalDictionaryWordNeverExist()
        {
            AssertWordPairNonExist("animal", "animal");
            AssertWordPairNonExist("bush", "bush");
            AssertWordPairNonExist("pinkcolor", "pinkcolor");
        }

        [Test]
        public void DifferentWordPairsThatExist()
        {
            AssertWordPairExist("method", "name");
            AssertWordPairExist("assert", "true");
            AssertWordPairExist("search", "result");
            AssertWordPairExist("assert", "null");
            AssertWordPairExist("sando", "search");
            AssertWordPairExist("directory", "update");
            AssertWordPairExist("configuration", "results");
        }

        [Test]
        public void DifferentWordPairsThatDoesNotExist()
        {
            AssertWordPairNonExist("confidence", "apple");
            AssertWordPairNonExist("confidence", "lackof");
            AssertWordPairNonExist("confidence", "configuration");
            AssertWordPairNonExist("configuration", "nomad");   
        }

        [Test]
        public void AssertCanGetWordsAndCount()
        {
            var dic = matrix.GetAllWordsAndCount();
            Assert.IsNotNull(dic);
            Assert.IsTrue(dic.Any());
        }
    }
}
