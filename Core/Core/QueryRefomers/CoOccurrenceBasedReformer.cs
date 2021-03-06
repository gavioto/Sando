﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sando.Core.Tools;

namespace Sando.Core.QueryRefomers
{
    public class CoOccurrenceBasedReformer : AbstractContextSensitiveWordReformer
    {
        private List<string> otherWords;

        public CoOccurrenceBasedReformer(DictionaryBasedSplitter localDictionary) 
            : base(localDictionary)
        {
        }

        protected override IEnumerable<ReformedWord> GetReformedTargetInternal(string target)
        {
            if (otherWords.Any())
            {
                var commonWords = otherWords.Select(w => localDictionary.GetCoOccurredWordsAndCount(w))
                    .Aggregate(GetDictionaryIntersect).ToList().OrderBy(p => -p.Value).Select(p => p.Key);
                return commonWords.Select(w => new ReformedWord(TermChangeCategory.COOCCUR, target, 
                    w, GetMessage(target, w)));
            }
            return Enumerable.Empty<ReformedWord>();
        }

        protected override int GetMaximumReformCount()
        {
            return QuerySuggestionConfigurations.COOCCURRENCE_WORDS_MAX_COUNT;
        }


        private Dictionary<string, int> GetDictionaryIntersect(Dictionary<string, int> dict1, 
            Dictionary<string, int> dict2)
        {
            var dictionary = new Dictionary<string, int>();
            var list1 = dict1.ToList();
            foreach (var word in list1)
            {
                int v2;
                if (dict2.TryGetValue(word.Key, out v2))
                {
                    dictionary.Add(word.Key, word.Value + v2);
                }
            }
            return dictionary;
        }

        private string GetMessage(string original, string reformed)
        {
            return "Neighbor";
        }

        public override void SetContextWords(IEnumerable<string> words)
        {
            this.otherWords = words.Where(w => localDictionary.DoesWordExist
                (w, DictionaryOption.NoStemming) && !String.IsNullOrEmpty(w)).ToList();
        }
    }
}
