﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sando.Core.Tools;
using Sando.DependencyInjection;

namespace Sando.Core.QueryRefomers
{
    internal class TypoCorrectionReformer : AbstractWordReformer
    {
        
        
        internal TypoCorrectionReformer(DictionaryBasedSplitter localDictionary) 
            : base(localDictionary)
        {
        }

        private string GetReformMessage(string originalWord, string newWord)
        {
            return "Correct \"" + originalWord + "\" to \"" + newWord + "\"";
        }

        protected override IEnumerable<ReformedWord> GetReformedTargetInternal(string target)
        {
            var list = localDictionary.FindSimilarWords(target).ToList();
            if (list.Any())
            {
                IEnumerable<ReformedWord> correctedList = list.Select(w => new ReformedWord
                      (TermChangeCategory.MISSPELLING, target, w,
                        GetReformMessage(target, w))).ToList();
                if (correctedList.Count() >= QuerySuggestionConfigurations.SIMILAR_WORDS_MAX_COUNT)
                    correctedList = correctedList.ToList().GetRange(0,
                        QuerySuggestionConfigurations.SIMILAR_WORDS_MAX_COUNT);
                return correctedList;
            }
            return Enumerable.Empty<ReformedWord>();
        }
    }
}