using UnityEngine;

namespace oti.Utilities
{
    public static class OTIUtilities
    {
        public static string[] _Alphabetic = new string[26] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

        /// <summary>
        /// Assembles a continuous alphabetic unique string from index i: A (i = 0), ...., AB (i = 27), ....., BC (i = 54), ... AAB etc
        /// </summary>
        public static string _AlphabetAssembler(int i)
        {
            string s = "";
            int wh = -1;

            for (int j = 0; j <= i; j += _Alphabetic.Length)
            {
                wh += 1;
            }

            for (int j = 0; j <= wh; j++)
            {
                if (j > 0)
                {
                    j = wh + 1;
                    s += _Alphabetic[i % _Alphabetic.Length];
                }
                else
                {
                    s += wh >= 1 ? _Alphabetic[wh - 1] : _Alphabetic[i % _Alphabetic.Length];
                }
            }

            return s;
        }
    }
}