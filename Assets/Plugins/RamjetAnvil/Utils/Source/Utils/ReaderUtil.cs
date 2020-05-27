using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RamjetAnvil.Unity.Utility
{
    public static class ReaderUtil
    {
        /// <summary>
        /// Falls back to the second reader if creating the first one gives a file not found exception.
        /// </summary>
        /// <returns>Either of the two readers</returns>
        public static TextReader FallbackReader(params Func<TextReader>[] readers)
        {
            foreach (var reader in readers)
            {
                try
                {
                    return reader();
                }
                catch (FileNotFoundException)
                {
                }    
            }
            throw new Exception("No suitable reader found.");
        }
    }
}
