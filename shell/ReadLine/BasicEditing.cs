/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Text;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Insert the key.
        /// </summary>
        public static void SelfInsert(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                return;
            }

            var keyChar = key.Value.KeyChar;
            if (keyChar == '\0')
                return;

            if (arg is int count)
            {
                if (count <= 0)
                    return;
            }
            else
            {
                count = 1;
            }

            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                Replace(start, length, new string(keyChar, count));
            }
            else if (count > 1)
            {
                Insert(new string(keyChar, count));
            }
            else
            {
                Insert(keyChar);
            }
        }

        /// <summary>
        /// Reverts all of the input to the current input.
        /// </summary>
        public static void RevertLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._prediction.RevertSuggestion())
            {
                return;
            }

            if (_singleton._statusIsErrorMessage)
            {
                // After an edit, clear the error message
                _singleton.ClearStatusMessage(render: false);
            }

            while (_singleton._undoEditIndex > 0)
            {
                _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                _singleton._undoEditIndex--;
            }
            _singleton.Render();
        }

        /// <summary>
        /// Cancel the current input, leaving the input on the screen,
        /// but returns back to the host so the prompt is evaluated again.
        /// </summary>
        public static void CancelLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.ClearStatusMessage(false);
            _singleton._current = _singleton._buffer.Length;

            using var _ = _singleton._prediction.DisableScoped();
            _singleton.ForceRender();

            _singleton._console.Write("\x1b[91m^C\x1b[0m");

            _singleton._buffer.Clear(); // Clear so we don't actually run the input
            _singleton._current = 0; // If Render is called, _current must be correct.
            _singleton._currentHistoryIndex = _singleton._history.Count;
            _singleton._inputAccepted = true;
        }

        /// <summary>
        /// Like KillLine - deletes text from the point to the end of the input,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void ForwardDeleteInput(ConsoleKeyInfo? key = null, object arg = null)
        {
            ForwardDeleteImpl(_singleton._buffer.Length, ForwardDeleteInput);
        }

        /// <summary>
        /// Deletes text from the point to the end of the current logical line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void ForwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            ForwardDeleteImpl(GetEndOfLogicalLinePos(_singleton._current) + 1, ForwardDeleteLine);
        }

        /// <summary>
        /// Deletes text from the cursor position to the specified end position
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        /// <param name="endPosition">0-based offset to one character past the end of the text.</param>
        private static void ForwardDeleteImpl(int endPosition, Action<ConsoleKeyInfo?, object> instigator)
        {
            var current = _singleton._current;
            var buffer = _singleton._buffer;

            if (buffer.Length > 0 && current < endPosition)
            {
                int length = endPosition - current;
                var str = buffer.ToString(current, length);

                _singleton.SaveEditItem(
                    EditItemDelete.Create(
                        str,
                        current,
                        instigator,
                        instigatorArg: null,
                        !InViEditMode()));

                buffer.Remove(current, length);
                _singleton.Render();
            }
        }

        /// <summary>
        /// Like BackwardKillInput - deletes text from the point to the start of the input,
        /// but does not put the deleted text in the kill ring.
        public static void BackwardDeleteInput(ConsoleKeyInfo? key = null, object arg = null)
        {
            BackwardDeleteSubstring(0, BackwardDeleteInput);
        }

        /// <summary>
        /// Like BackwardKillLine - deletes text from the point to the start of the logical line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void BackwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var position = GetBeginningOfLinePos(_singleton._current);
            BackwardDeleteSubstring(position, BackwardDeleteLine);
        }

        private static void BackwardDeleteSubstring(int position, Action<ConsoleKeyInfo?, object> instigator)
        {
            if (_singleton._current > position)
            {
                var count = _singleton._current - position;

                _singleton.RemoveTextToViRegister(position, count, instigator, arg: null, !InViEditMode());
                _singleton._current = position;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character before the cursor.
        /// </summary>
        public static void BackwardDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                Delete(start, length);
                return;
            }

            if (_singleton._buffer.Length > 0 && _singleton._current > 0)
            {
                int qty = arg as int? ?? 1;
                if (qty < 1) return; // Ignore useless counts
                qty = Math.Min(qty, _singleton._current);

                int startDeleteIndex = _singleton._current - qty;

                _singleton.RemoveTextToViRegister(startDeleteIndex, qty, BackwardDeleteChar, arg, !InViEditMode());
                _singleton._current = startDeleteIndex;
                _singleton.Render();
            }
        }

        private void DeleteCharImpl(int qty, bool orExit)
        {
            if (_visualSelectionCommandCount > 0)
            {
                GetRegion(out var start, out var length);
                Delete(start, length);
                return;
            }

            if (_buffer.Length > 0)
            {
                if (_current < _buffer.Length)
                {
                    qty = Math.Min(qty, _singleton._buffer.Length - _singleton._current);

                    RemoveTextToViRegister(_current, qty, DeleteChar, qty, !InViEditMode());
                    if (_current >= _buffer.Length)
                    {
                        _current = Math.Max(0, _buffer.Length + ViEndOfLineFactor);
                    }
                    Render();
                }
            }
            else if (orExit)
            {
                throw new ExitException();
            }
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        public static void DeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            if (qty < 1) return; // Ignore useless counts

            _singleton.DeleteCharImpl(qty, orExit: false);
        }

        /// <summary>
        /// Delete the character under the cursor, or if the line is empty, exit the process.
        /// </summary>
        public static void DeleteCharOrExit(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DeleteCharImpl(1, orExit: true);
        }

        /// <summary>
        /// A helper function to change the case of the current word.
        /// </summary>
        private static void UpdateWordCase(bool toUpper)
        {
            if (_singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            int endOfWord = _singleton.FindForwardWordPoint(_singleton.Options.WordDelimiters);
            int wordlen = endOfWord - _singleton._current;

            string word = _singleton._buffer.ToString(_singleton._current, wordlen);
            word = toUpper ? word.ToUpper() : word.ToLower();

            Replace(_singleton._current, wordlen, word);

            _singleton.MoveCursor(endOfWord);
            _singleton.Render();
        }

        /// <summary>
        /// Upcase the current word and move to the next one.
        /// </summary>
        public static void UpcaseWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            UpdateWordCase(toUpper: true);
        }

        /// <summary>
        /// Downcase the current word and move to the next one.
        /// </summary>
        public static void DowncaseWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            UpdateWordCase(toUpper: false);
        }

        /// <summary>
        /// Capitalize the current word and move to the next one.
        /// </summary>
        public static void CapitalizeWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            int endOfWord = _singleton.FindForwardWordPoint(_singleton.Options.WordDelimiters);
            int wordlen = endOfWord - _singleton._current;

            char[] word = _singleton._buffer.ToString(_singleton._current, wordlen).ToLower().ToCharArray();
            int firstLetterIdx = Array.FindIndex(word, static x => char.IsLetter(x));

            if (firstLetterIdx >= 0)
            {
                word[firstLetterIdx] = char.ToUpper(word[firstLetterIdx]);
                Replace(_singleton._current, wordlen, new string(word));
            }

            _singleton.MoveCursor(endOfWord);
            _singleton.Render();
        }

        private bool AcceptLineImpl(bool validate)
        {
            using var _ = _prediction.DisableScoped();

            // If text was pasted, for performance reasons we skip rendering for some time,
            // but if input is accepted, we won't have another chance to render.
            //
            // Also - if there was an emphasis, we want to clear that before accepting
            // and that requires rendering.
            bool renderNeeded = _emphasisStart >= 0 || _queuedKeys.Count > 0;

            _emphasisStart = -1;
            _emphasisLength = 0;

            if (renderNeeded)
            {
                ForceRender();
            }

            // Only run validation if we haven't before.  If we have and status line shows an error,
            // treat that as a -Force and accept the input so it is added to history, and PowerShell
            // can report an error as it normally does.
            if (validate && !_statusIsErrorMessage)
            {
                var insertionPoint = _current;
                var errorMessage = Validate(_buffer);
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    // If there are more keys, assume the user pasted with a right click and
                    // we should insert a newline even though validation failed.
                    if (_queuedKeys.Count > 0)
                    {
                        // Validation may have moved the cursor.  Because there are queued
                        // keys, we need to move the cursor back to the correct place, and
                        // ignore where validation put the cursor because the queued keys
                        // will be inserted in the wrong place.
                        SetCursorPosition(insertionPoint);
                        Insert('\n');
                    }
                    _statusLinePrompt = "";
                    _statusBuffer.Append(errorMessage);
                    _statusIsErrorMessage = true;
                    Render();
                    return false;
                }
            }

            if (_statusIsErrorMessage)
            {
                ClearStatusMessage(render: true);
            }

            // Make sure cursor is at the end before writing the line.
            if (_current != _buffer.Length)
            {
                // Let public API set cursor to end of line incase end of line is end of buffer.
                _current = _buffer.Length;
                SetCursorPosition(_current);
            }

            // Clear the prediction view if there is one.
            _prediction.ActiveView.Clear(cursorAtEol: true);

            _console.Write("\n");
            _inputAccepted = true;
            return true;
        }

        private string Validate(StringBuilder buffer)
        {
            return null;
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadLine waits for
        /// keys to edit the current input.
        /// </summary>
        public static void AcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.AcceptLineImpl(false);
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadLine waits for
        /// keys to edit the current input.
        /// </summary>
        public static void ValidateAndAcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.AcceptLineImpl(true);
        }

        /// <summary>
        /// Attempt to execute the current input.  If it can be executed (like AcceptLine),
        /// then recall the next item from history the next time ReadLine is called.
        /// </summary>
        public static void AcceptAndGetNext(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton.AcceptLineImpl(false))
            {
                if (_singleton._currentHistoryIndex < (_singleton._history.Count - 1))
                {
                    _singleton._getNextHistoryIndex = _singleton._currentHistoryIndex + 1;
                }
                else
                {
                    Ding();
                }
            }
        }

        /// <summary>
        /// The continuation prompt is displayed on the next line and PSReadLine waits for
        /// keys to edit the current input.  This is useful to enter multi-line input as
        /// a single command even when a single line is complete input by itself.
        /// </summary>
        public static void AddLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            Insert('\n');
        }

        /// <summary>
        /// A new empty line is created above the current line regardless of where the cursor
        /// is on the current line.  The cursor moves to the beginning of the new line.
        /// </summary>
        public static void InsertLineAbove(ConsoleKeyInfo? key = null, object arg = null)
        {
            // Move the current position to the beginning of the current line and only the current line.
            _singleton._current = GetBeginningOfLinePos(_singleton._current);
            Insert('\n');
            PreviousLine();
        }

        /// <summary>
        /// A new empty line is created below the current line regardless of where the cursor
        /// is on the current line.  The cursor moves to the beginning of the new line.
        /// </summary>
        public static void InsertLineBelow(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton._current;
            for (; i < _singleton._buffer.Length; i++)
            {
                if (_singleton._buffer[i] == '\n')
                {
                    break;
                }
            }

            _singleton._current = i;

            Insert('\n');
        }
    }
}
