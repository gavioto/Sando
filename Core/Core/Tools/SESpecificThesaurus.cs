﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Sando.Core.QueryRefomers;

namespace Sando.Core.Tools
{
    public interface IThesaurus : IInitializable
    {
        IEnumerable<SynonymInfo> GetSynonyms(String word);
    }

    public class SynonymInfo
    {
        public string Synonym { get; private set; }
        public int SimilarityScore { get; private set; }

        public SynonymInfo(String Synonym, int SimilarityScore = 0)
        {
            this.Synonym = Synonym;
            this.SimilarityScore = SimilarityScore;
        }
    }

    public class ThesaurusHelper
    {
        public static IEnumerable<T> GetValuesOfKey<T>(List<KeyValuePair<String, T>> keyValuePairs,
          string word)
        {
            var target = new KeyValuePair<String, T>(word, default(T));
            var comparer = new KeyComparer<T>();
            int endIndex = keyValuePairs.BinarySearch(target, comparer);
            if (endIndex > -1 && endIndex < keyValuePairs.Count)
            {
                int startInex;
                for (startInex = endIndex; startInex > 0 && comparer.Compare(keyValuePairs.ElementAt(startInex - 1), 
                    target) == 0; startInex--);
                return keyValuePairs.GetRange(startInex, endIndex - startInex + 1).Select(p => p.Value);
            }
            return Enumerable.Empty<T>();
        }

        private class KeyComparer<T> : IComparer<KeyValuePair<string, T>>
        {
            public int Compare(KeyValuePair<string, T> x, KeyValuePair<string, T> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }
    }


    public class SeSpecificThesaurus : IThesaurus
    {
        private static readonly string filePath = GetFilePath();
        private List<ThesaurusEntry> orderedWordPairs;
        private List<ThesaurusEntry> switchedWordPairs;
        private readonly object locker = new object();
        private bool isInitialized;
        private static SeSpecificThesaurus instance;

        private SeSpecificThesaurus()
        {
            lock (locker)
            {
                this.isInitialized = false;
            }
        }
        public static IThesaurus GetInstance()
        {
            return instance ?? (instance = new SeSpecificThesaurus());
        }

        private static string GetFilePath()
        {
            var directory = PathManager.Instance.GetExtensionRoot();
            var file = @"Dictionaries\mined_related_unique.csv";
            return File.Exists(file) ? file : Path.Combine(directory, file);
        }


        private class ThesaurusEntry
        {
            public String FirstWord { private set; get; }
            public String SecondWord { private set; get; }
            public int Score { private set; get; }

            public ThesaurusEntry(String FirstWord, String SecondWord, int Score)
            {
                this.FirstWord = FirstWord;
                this.SecondWord = SecondWord;
                this.Score = Score;
            }

            public ThesaurusEntry(String line)
            {
                var parts = line.Split(new char[] {','});
                this.FirstWord = parts[0];
                this.SecondWord = parts[1];
                this.Score = int.Parse(parts[2]);
            }

            public ThesaurusEntry GetReversedEntry()
            {
                return new ThesaurusEntry(SecondWord, FirstWord, Score);
            }
        }

        public void Initialize(string notUsedDirectory)
        {
            lock (locker)
            {
                if (!isInitialized)
                {
                    var lines = File.ReadAllLines(filePath).Select(a => a.Split(';'));
                    IEnumerable<string> csv = (from line in lines select (from piece in line select piece).
                            First()).Skip(1);
                    var synonyms = csv.Select(s => new ThesaurusEntry(s)).ToList();
                    this.orderedWordPairs = synonyms.OrderBy(s => s.FirstWord).ToList();
                    this.switchedWordPairs = synonyms.Select(s => s.GetReversedEntry()).OrderBy(s => 
                        s.FirstWord).ToList();
                    this.isInitialized = true;
                }
            }
        }

        private String Preprocess(String word)
        {
            return word.Trim().ToLower();
        }
       
        private IEnumerable<ThesaurusEntry> GetEntriesByFirstWord(List<ThesaurusEntry> entries, String word)
        {
            return entries.CustomBinarySearch(new ThesaurusEntry(word, "", 0), new EntryKeyComparer());
        }

        private class EntryKeyComparer : IComparer<ThesaurusEntry>
        {
            public int Compare(ThesaurusEntry x, ThesaurusEntry y)
            {
                return x.FirstWord.CompareTo(y.FirstWord);
            }
        }

        public IEnumerable<SynonymInfo> GetSynonyms(String word)
        {
            lock (locker)
            {
                if (!String.IsNullOrEmpty(word))
                {
                    word = Preprocess(word);
                    return GetEntriesByFirstWord(orderedWordPairs, word)
                            .Union(GetEntriesByFirstWord(switchedWordPairs, word)).
                                Select(entry => new SynonymInfo(entry.SecondWord, 
                                    entry.Score));
                }
                return Enumerable.Empty<SynonymInfo>();
            }
        }
    }
}
