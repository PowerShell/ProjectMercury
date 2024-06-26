﻿/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        // Yank/Kill state
        private List<string> _killRing;
        private int _killIndex;
        private int _killCommandCount;
        private int _yankCommandCount;
        private int _yankStartPoint;

        private int _visualSelectionCommandCount;

        /// <summary>
        /// Mark the current location of the cursor for use in a subsequent editing command.
        /// </summary>
        public static void SetMark(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._mark = _singleton._current;
        }

        /// <summary>
        /// The cursor is placed at the location of the mark and the mark is moved
        /// to the location of the cursor.
        /// </summary>
        public static void ExchangePointAndMark(ConsoleKeyInfo? key = null, object arg = null)
        {
            var tmp = _singleton._mark;
            _singleton._mark = _singleton._current;
            _singleton.MoveCursor(Math.Min(tmp, _singleton._buffer.Length));
        }

        /// <summary>
        /// The contents of the kill ring are cleared.
        /// </summary>
        public static void ClearKillRing()
        {
            _singleton._killRing?.Clear();
            _singleton._killIndex = -1;    // So first add indexes 0.
        }

        private void Kill(int start, int length, bool prepend)
        {
            if (length <= 0)
            {
                // if we're already in the middle of some kills,
                // change _killCommandCount so it isn't zeroed out.
                // If, OTOH, _killCommandCount was 0 to begin with,
                // we won't append to something we're not supposed to.
                if (_killCommandCount > 0)
                    _killCommandCount++;
                return;
            }
            var killText = _buffer.ToString(start, length);
            SaveEditItem(EditItemDelete.Create(killText, start));
            _buffer.Remove(start, length);
            _current = start;
            Render();
            if (_killCommandCount > 0)
            {
                if (prepend)
                {
                    _killRing[_killIndex] = killText + _killRing[_killIndex];
                }
                else
                {
                    _killRing[_killIndex] += killText;
                }
            }
            else
            {
                if (_killRing.Count < Options.MaximumKillRingCount)
                {
                    _killRing.Add(killText);
                    _killIndex = _killRing.Count - 1;
                }
                else
                {
                    _killIndex += 1;
                    if (_killIndex == _killRing.Count)
                    {
                        _killIndex = 0;
                    }
                    _killRing[_killIndex] = killText;
                }
            }
            _killCommandCount += 1;
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the input.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void KillLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Kill(_singleton._current, _singleton._buffer.Length - _singleton._current, false);
        }

        /// <summary>
        /// Clear the input from the start of the input to the cursor.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void BackwardKillInput(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Kill(0, _singleton._current, true);
        }

        /// <summary>
        /// Clear the input from the start of the current logical line to the cursor.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void BackwardKillLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var start = GetBeginningOfLinePos(_singleton._current);
            _singleton.Kill(start, _singleton._current, true);
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the current word.  If the cursor
        /// is between words, the input is cleared from the cursor to the end of the next word.
        /// The cleared text is placed in the kill ring.
        /// </summary>
        public static void KillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindForwardWordPoint(_singleton.Options.WordDelimiters);
            _singleton.Kill(_singleton._current, i - _singleton._current, false);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void BackwardKillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindBackwardWordPoint(_singleton.Options.WordDelimiters);
            _singleton.Kill(i, _singleton._current - i, true);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void UnixWordRubout(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindBackwardWordPoint("");
            _singleton.Kill(i, _singleton._current - i, true);
        }

        /// <summary>
        /// Kill the text between the cursor and the mark.
        /// </summary>
        public static void KillRegion(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.GetRegion(out var start, out var length);
            _singleton.Kill(start, length, true);
        }

        private void YankImpl()
        {
            if (_killRing.Count == 0)
                return;

            // Starting a yank session, yank the last thing killed and
            // remember where we started.
            _mark = _yankStartPoint = _current;
            Insert(_killRing[_killIndex]);

            _yankCommandCount += 1;
        }

        /// <summary>
        /// Add the most recently killed text to the input.
        /// </summary>
        public static void Yank(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.YankImpl();
        }

        private void YankPopImpl()
        {
            if (_yankCommandCount == 0)
                return;

            _killIndex -= 1;
            if (_killIndex < 0)
            {
                _killIndex = _killRing.Count - 1;
            }
            var yankText = _killRing[_killIndex];
            Replace(_yankStartPoint, _current - _yankStartPoint, yankText);
            _yankCommandCount += 1;
        }

        /// <summary>
        /// If the previous operation was Yank or YankPop, replace the previously yanked
        /// text with the next killed text from the kill ring.
        /// </summary>
        public static void YankPop(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.YankPopImpl();
        }

        private void VisualSelectionCommon(Action action, bool forceSetMark = false)
        {
            if (_singleton._visualSelectionCommandCount == 0 || forceSetMark)
            {
                SetMark();
            }
            _singleton._visualSelectionCommandCount += 1;
            action();
            _singleton.RenderWithPredictionQueryPaused();
        }

        /// <summary>
        /// Adjust the current selection to include the previous character.
        /// </summary>
        public static void SelectBackwardChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BackwardChar(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next character.
        /// </summary>
        public static void SelectForwardChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ForwardChar(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the previous word.
        /// </summary>
        public static void SelectBackwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BackwardWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word.
        /// </summary>
        public static void SelectNextWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => NextWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word using ForwardWord.
        /// </summary>
        public static void SelectForwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ForwardWord(key, arg));
        }

        /// <summary>
        /// Select the entire line.
        /// </summary>
        public static void SelectAll(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._visualSelectionCommandCount += 1;
            _singleton._mark = 0;
            _singleton._current = _singleton._buffer.Length;
            _singleton.RenderWithPredictionQueryPaused();
        }

        /// <summary>
        /// Adjust the current selection to include from the cursor to the end of the line.
        /// </summary>
        public static void SelectLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => EndOfLine(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include from the cursor to the start of the line.
        /// </summary>
        public static void SelectBackwardsLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BeginningOfLine(key, arg));
        }

        /// <summary>
        /// Paste text from the system clipboard.
        /// </summary>
        public static void Paste(ConsoleKeyInfo? key = null, object arg = null)
        {
            string textToPaste = Clipboard.GetText();

            if (textToPaste != null)
            {
                textToPaste = textToPaste.Replace("\r", "");
                textToPaste = textToPaste.Replace("\t", "    ");
                if (_singleton._visualSelectionCommandCount > 0)
                {
                    _singleton.GetRegion(out var start, out var length);
                    Replace(start, length, textToPaste);
                }
                else
                {
                    Insert(textToPaste);
                }
            }
        }

        /// <summary>
        /// Copy selected region to the system clipboard.  If no region is selected, copy the whole line.
        /// </summary>
        public static void Copy(ConsoleKeyInfo? key = null, object arg = null)
        {
            string textToSet;
            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                textToSet = _singleton._buffer.ToString(start, length);
            }
            else
            {
                textToSet = _singleton._buffer.ToString();
            }
            if (!string.IsNullOrEmpty(textToSet))
            {
                Clipboard.SetText(textToSet);
            }
        }

        /// <summary>
        /// If text is selected, copy to the clipboard, otherwise cancel the line.
        /// </summary>
        public static void CopyOrCancelLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                Copy(key, arg);
            }
            else
            {
                CancelLine(key, arg);
            }
        }

        /// <summary>
        /// Delete selected region placing deleted text in the system clipboard.
        /// </summary>
        public static void Cut(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                Clipboard.SetText(_singleton._buffer.ToString(start, length));
                Delete(start, length);
            }
        }
    }
}
