using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Extensions
{
    public static class StringExtensions
    {
        public static bool IsSet(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }
        
        public static bool IsEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }
    }
}
