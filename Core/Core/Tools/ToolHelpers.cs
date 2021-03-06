﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sando.Core.QueryRefomers;

namespace Sando.Core.Tools
{
    public static class ToolHelpers
    {
        public static IEnumerable<T> RemoveRedundance<T>(this IEnumerable<T> list) where T : IEquatable<T>
        {
            return list.Distinct();
        }

        public static IEnumerable<ReformedWord> CreateNonChangedTerm(string word)
        {
            return new[] { new ReformedWord(TermChangeCategory.NOT_CHANGED, word, word, String.Empty) };
        }

        public static IEnumerable<T> TrimIfOverlyLong<T>(this IEnumerable<T> list, int maxCount)
        {
            var newList = list.ToList();
            return newList.Count() > maxCount ? newList.GetRange(0, maxCount) : newList;
        }

        public static IEnumerable<T> DistinctBy<T, M>(this IEnumerable<T> list,
            Func<T, M> selector) where M : IEquatable<M>
        {
            var set = new HashSet<T>(list, new GenericEqualityCompare<T,M>(selector));
            return set.AsEnumerable();
        }

        private class GenericEqualityCompare<T, M> : IEqualityComparer<T>
            where M : IEquatable<M>
        {
            private readonly Func<T, M> selector;

            public GenericEqualityCompare(Func<T, M> selector)
            {
                this.selector = selector;
            }

            public bool Equals(T x, T y)
            {
                var mx = selector.Invoke(x);
                var my = selector.Invoke(y);
                return mx.Equals(my);
            }

            public int GetHashCode(T obj)
            {
                return 0;
            }
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static IEqualityComparer<String> GetCaseInsensitiveEqualityComparer()
        {
            return new InternalStringComparer();
        }

        private class InternalStringComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return 0;
            }
        }

        public static bool IsIndexInRange<T>(this IEnumerable<T> list, int index)
        {
            var array = list.ToArray();
            return index >= 0 && index < array.Count();
        }

        public static IEnumerable<T> CustomBinarySearch<T>(this List<T> list, T target, IComparer<T> comparer)
        {
            var endIndex = list.BinarySearch(target, comparer);
            if (endIndex > -1 && endIndex < list.Count) 
            {
                int startIndex = endIndex;
                for (; startIndex >=0 && comparer.Compare(list.ElementAt(startIndex - 1), target) == 0; startIndex--);
                startIndex = startIndex < 0 ? 0 : startIndex;
                return list.GetRange(startIndex, endIndex - startIndex + 1);
            }
            return Enumerable.Empty<T>();
        }

        public static bool IsWordFlag(this string word)
        {
            return word.StartsWith("-");
        }

        public static bool IsWordQuoted(this string word)
        {
            return word.Trim().StartsWith("\"") && word.EndsWith("\"");
        }

        public static String ToLowerAndTrim(this string text)
        {
            return text.ToLower().Trim();
        }

        public static bool IsStemSameTo(this string word1, string word2)
        {
            return word1.GetStemmedQuery().Equals(word2.GetStemmedQuery());
        }

        public static List<T> AddImmutably<T>(this List<T> list, T element)
        {
            var newList = list.ToList();
            newList.Add(element);
            return newList;
        }

        public static String[] SplitToLines(this String text)
        {
            return text.Split(new string[] {Environment.NewLine}, StringSplitOptions.None);
        }
    }
}
