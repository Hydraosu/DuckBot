﻿using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Resources;
using Discord;

namespace DuckBot
{
    public static class Utils
    {
        public static Task RunAsync<T>(Action<T> func, T arg)
        {
            return Task.Run(() => func(arg));
        }

        public static string StartCase(this string data)
        {
            return data == null ? null : char.ToUpper(data[0]) + data.Substring(1);
        }

        public static bool IsCultureAvailable(string langCode)
        {
            CultureInfo ci = null;
            try { ci = CultureInfo.GetCultureInfo(langCode); }
            catch (CultureNotFoundException) { return false; }
            ResourceManager rm = null;
            try
            {
                rm = new ResourceManager(typeof(Resources.Strings));
                using (ResourceSet rs = rm.GetResourceSet(ci, true, false))
                    if (rs != null) return true;
                return false;
            }
            finally { rm.ReleaseAllResources(); }
        }

        public static bool UserActive(this IPresence user) { return user != null && (user.Status == UserStatus.Online || user.Status == UserStatus.DoNotDisturb); }

        public static int Similarity(string data1, string data2)
        {
            if (string.IsNullOrEmpty(data1) || string.IsNullOrEmpty(data2)) return 0;
            data1 = data1.ToLower(); data2 = data2.ToLower();
            int pos1 = 0, pos2 = 0, ret = 0;
            while (pos1 < data1.Length && pos2 < data2.Length)
                if (data1[pos1] == data2[pos2])
                {
                    ++pos1; ++pos2; ++ret;
                }
                else if (data1.Length - pos1 > data2.Length - pos2) ++pos1;
                else ++pos2;
            return ret;
        }

        internal static string Extract(string input, string start, string end)
        {
            int ix = input.LastIndexOf(start);
            if (ix == -1) return null;
            ix += start.Length;
            int ie = input.IndexOf(end, ix);
            if (ie == -1) return null;
            return input.Substring(ix, ie - ix);
        }
    }
}
