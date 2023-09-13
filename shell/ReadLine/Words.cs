/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private enum FindTokenMode
        {
            CurrentOrNext,
            Next,
            Previous,
        }

        private bool InWord(int index, string wordDelimiters)
        {
            char c = _buffer[index];
            return InWord(c, wordDelimiters);
        }

        private bool InWord(char c, string wordDelimiters)
        {
            return !char.IsWhiteSpace(c) && wordDelimiters.IndexOf(c) < 0;
        }

        /// <summary>
        /// Find the end of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int FindForwardWordPoint(string wordDelimiters)
        {
            int i = _current;
            if (i == _buffer.Length)
            {
                return i;
            }

            if (!InWord(i, wordDelimiters))
            {
                // Scan to end of current non-word region
                while (i < _buffer.Length)
                {
                    if (InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i += 1;
                }
            }
            while (i < _buffer.Length)
            {
                if (!InWord(i, wordDelimiters))
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Find the start of the next word.
        /// </summary>
        private int FindNextWordPoint(string wordDelimiters)
        {
            int i = _singleton._current;
            if (i == _singleton._buffer.Length)
            {
                return i;
            }

            if (InWord(i, wordDelimiters))
            {
                // Scan to end of current word region
                while (i < _singleton._buffer.Length)
                {
                    if (!InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i += 1;
                }
            }

            while (i < _singleton._buffer.Length)
            {
                if (InWord(i, wordDelimiters))
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Find the beginning of the previous word.
        /// </summary>
        private int FindBackwardWordPoint(string wordDelimiters)
        {
            int i = _current - 1;
            if (i < 0)
            {
                return 0;
            }

            if (!InWord(i, wordDelimiters))
            {
                // Scan backwards until we are at the end of the previous word.
                while (i > 0)
                {
                    if (InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i -= 1;
                }
            }
            while (i > 0)
            {
                if (!InWord(i, wordDelimiters))
                {
                    i += 1;
                    break;
                }
                i -= 1;
            }
            return i;
        }


    }
}
