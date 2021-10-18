// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Text
{
    internal static partial class EncodingTable
    {
        private static readonly Dictionary<string, int> s_nameToCodePageCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, string> s_codePageToWebNameCache = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> s_codePageToEnglishNameCache = new Dictionary<int, string>();
        private static readonly ReaderWriterLockSlim s_cacheLock = new ReaderWriterLockSlim();

        internal static int GetCodePageFromName(string name)
        {
            if (name == null)
                return 0;

            int codePage;

            s_cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (s_nameToCodePageCache.TryGetValue(name, out codePage))
                {
                    return codePage;
                }
                else
                {
                    // Okay, we didn't find it in the hash table, try looking it up in the unmanaged data.
                    codePage = InternalGetCodePageFromName(name);
                    if (codePage == 0)
                        return 0;

                    s_cacheLock.EnterWriteLock();
                    try
                    {
                        int cachedCodePage;
                        if (s_nameToCodePageCache.TryGetValue(name, out cachedCodePage))
                        {
                            return cachedCodePage;
                        }
                        s_nameToCodePageCache.Add(name, codePage);
                    }
                    finally
                    {
                        s_cacheLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                s_cacheLock.ExitUpgradeableReadLock();
            }

            return codePage;
        }

        private static int InternalGetCodePageFromName(string name)
        {
            int left = 0;
            int right = EncodingNameIndices.Length - 2;
            int index;
            int result;

            Debug.Assert(EncodingNameIndices.Length == CodePagesByName.Length + 1);
            Debug.Assert(EncodingNameIndices[^1] == s_encodingNames.Length);

            name = name.ToLowerInvariant();

            //Binary search the array until we have only a couple of elements left and then
            //just walk those elements.
            while ((right - left) > 3)
            {
                index = ((right - left) / 2) + left;

                Debug.Assert(index < EncodingNameIndices.Length - 1);
                result = CompareOrdinal(name, s_encodingNames, EncodingNameIndices[index], EncodingNameIndices[index + 1] - EncodingNameIndices[index]);
                if (result == 0)
                {
                    //We found the item, return the associated codePage.
                    return (CodePagesByName[index]);
                }
                else if (result < 0)
                {
                    //The name that we're looking for is less than our current index.
                    right = index;
                }
                else
                {
                    //The name that we're looking for is greater than our current index
                    left = index;
                }
            }

            //Walk the remaining elements (it'll be 3 or fewer).
            for (; left <= right; left++)
            {
                Debug.Assert(left < EncodingNameIndices.Length - 1);
                if (CompareOrdinal(name, s_encodingNames, EncodingNameIndices[left], EncodingNameIndices[left + 1] - EncodingNameIndices[left]) == 0)
                {
                    return (CodePagesByName[left]);
                }
            }

            // The encoding name is not valid.
            return 0;
        }

        private static int CompareOrdinal(string s1, string s2, int index, int length)
        {
            int count = s1.Length;
            if (count > length)
                count = length;

            int i = 0;
            while (i < count && s1[i] == s2[index + i])
                i++;

            if (i < count)
                return (int)(s1[i] - s2[index + i]);

            return s1.Length - length;
        }

        internal static string? GetWebNameFromCodePage(int codePage)
        {
            return GetNameFromCodePage(codePage, s_webNames, WebNameIndices, s_codePageToWebNameCache);
        }

        internal static string? GetEnglishNameFromCodePage(int codePage)
        {
            return GetNameFromCodePage(codePage, s_englishNames, EnglishNameIndices, s_codePageToEnglishNameCache);
        }

        private static string? GetNameFromCodePage(int codePage, string names, ReadOnlySpan<int> indices, Dictionary<int, string> cache)
        {
            string? name;

            Debug.Assert(MappedCodePages.Length + 1 == indices.Length);
            Debug.Assert(indices[indices.Length - 1] == names.Length);

            //This is a linear search, but we probably won't be doing it very often.
            for (int i = 0; i < MappedCodePages.Length; i++)
            {
                if (MappedCodePages[i] == codePage)
                {
                    Debug.Assert(i < indices.Length - 1);

                    s_cacheLock.EnterUpgradeableReadLock();
                    try
                    {
                        if (cache.TryGetValue(codePage, out name))
                        {
                            return name;
                        }
                        else
                        {
                            name = names.Substring(indices[i], indices[i + 1] - indices[i]);

                            s_cacheLock.EnterWriteLock();
                            try
                            {
                                if (cache.TryGetValue(codePage, out string? cachedName))
                                {
                                    return cachedName;
                                }

                                cache.Add(codePage, name);
                            }
                            finally
                            {
                                s_cacheLock.ExitWriteLock();
                            }
                        }
                    }
                    finally
                    {
                        s_cacheLock.ExitUpgradeableReadLock();
                    }

                    return name;
                }
            }

            // Nope, we didn't find it.
            return null;
        }
    }
}
