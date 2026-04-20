using System;

namespace Yotepad
{
    public class SearchService
    {
        public string LastSearchTerm { get; private set; } = string.Empty;
        public bool LastMatchCase { get; private set; } = false;
        public bool LastMatchWholeWord { get; private set; } = false;
        public bool LastSearchDown { get; private set; } = true;

        public void UpdateSearchState(string term, bool matchCase, bool matchWholeWord, bool searchDown)
        {
            LastSearchTerm = term;
            LastMatchCase = matchCase;
            LastMatchWholeWord = matchWholeWord;
            LastSearchDown = searchDown;
        }

        public int Find(string fullText, string searchTerm, int startIndex, bool matchCase, bool matchWholeWord, bool searchDown)
        {
            if (string.IsNullOrEmpty(fullText) || string.IsNullOrEmpty(searchTerm)) return -1;

            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (searchDown)
            {
                int index = startIndex < fullText.Length 
                    ? FindNext(fullText, searchTerm, startIndex, comparison, matchWholeWord) 
                    : -1;

                // Wrap to top
                if (index == -1)
                    index = FindNext(fullText, searchTerm, 0, comparison, matchWholeWord);

                return index;
            }
            else
            {
                int index = startIndex > 0 
                    ? FindPrevious(fullText, searchTerm, startIndex - 1, comparison, matchWholeWord) 
                    : -1;

                // Wrap to bottom
                if (index == -1)
                    index = FindPrevious(fullText, searchTerm, fullText.Length - 1, comparison, matchWholeWord);

                return index;
            }
        }

        private int FindNext(string fullText, string searchTerm, int fromIndex, StringComparison comparison, bool matchWholeWord)
        {
            int pos = fromIndex;
            while (pos <= fullText.Length - searchTerm.Length)
            {
                int found = fullText.IndexOf(searchTerm, pos, comparison);
                if (found == -1) return -1;
                if (!matchWholeWord || IsWholeWordMatch(fullText, found, searchTerm.Length))
                    return found;
                pos = found + 1;
            }
            return -1;
        }

        private int FindPrevious(string fullText, string searchTerm, int fromIndex, StringComparison comparison, bool matchWholeWord)
        {
            int pos = fromIndex;
            while (pos >= 0)
            {
                int found = fullText.LastIndexOf(searchTerm, pos, pos + 1, comparison);
                if (found == -1) return -1;
                if (!matchWholeWord || IsWholeWordMatch(fullText, found, searchTerm.Length))
                    return found;
                pos = found - 1;
            }
            return -1;
        }

        private bool IsWholeWordMatch(string fullText, int index, int length)
        {
            // Check character before — must be start of text or non-word character
            if (index > 0 && IsWordChar(fullText[index - 1]))
                return false;

            // Check character after — must be end of text or non-word character
            int after = index + length;
            if (after < fullText.Length && IsWordChar(fullText[after]))
                return false;

            return true;
        }

        private bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        public string ReplaceAll(string fullText, string searchTerm, string replaceTerm, bool matchCase, bool matchWholeWord)
        {
            if (string.IsNullOrEmpty(fullText) || string.IsNullOrEmpty(searchTerm)) return fullText;

            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (!matchWholeWord)
                return fullText.Replace(searchTerm, replaceTerm ?? string.Empty, comparison);

            // Whole word replace — walk the string manually
            var result = new System.Text.StringBuilder();
            int pos = 0;
            while (pos < fullText.Length)
            {
                int found = fullText.IndexOf(searchTerm, pos, comparison);
                if (found == -1)
                {
                    result.Append(fullText, pos, fullText.Length - pos);
                    break;
                }

                result.Append(fullText, pos, found - pos);

                if (IsWholeWordMatch(fullText, found, searchTerm.Length))
                {
                    result.Append(replaceTerm ?? string.Empty);
                    pos = found + searchTerm.Length;
                }
                else
                {
                    result.Append(fullText[found]);
                    pos = found + 1;
                }
            }
            return result.ToString();
        }
    }
}