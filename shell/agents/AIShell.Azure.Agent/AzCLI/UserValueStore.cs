using System.Text;

namespace AIShell.Azure.CLI;

internal class UserValueStore
{
    const string PseudoValuePrefix = "__pseudo_";
    const string PseudoValueSuffix = "_v__";
    private int _counter;
    private readonly Dictionary<string, string> _pseudoToRealValueMap;
    private readonly Dictionary<string, string> _realToPseudoValueMap;

    internal UserValueStore()
    {
        _counter = 0;
        _pseudoToRealValueMap = [];
        _realToPseudoValueMap = [];
    }

    private static bool DigitsOnly(string text, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private string GetUniquePseudoValue()
    {
        _counter++;
        return $"{PseudoValuePrefix}{_counter}{PseudoValueSuffix}";
    }

    internal string SaveUserInputValue(string userValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(userValue);

        // We want to have 1:1 mapping between a real value and a pseudo value, so if the user inputs the same
        // value for 2 different parameters, the server handler will see the same value for those 2 parameters.
        if (_realToPseudoValueMap.TryGetValue(userValue, out string pseudoValue))
        {
            return pseudoValue;
        }

        pseudoValue = GetUniquePseudoValue();
        _pseudoToRealValueMap.Add(pseudoValue, userValue);
        _realToPseudoValueMap.Add(userValue, pseudoValue);
        return pseudoValue;
    }

    internal void Clear()
    {
        _counter = 0;
        _pseudoToRealValueMap.Clear();
        _realToPseudoValueMap.Clear();
    }

    internal string ReplacePseudoValues(string script)
    {
        List<(int Start, int End)> ranges = null;

        int index = script.IndexOf(PseudoValuePrefix);
        while (index > -1)
        {
            int start = index + PseudoValuePrefix.Length;
            int i2 = script.IndexOf(PseudoValueSuffix, start);
            if (i2 > -1 && DigitsOnly(script, start, i2))
            {
                start = i2 + PseudoValueSuffix.Length;
                ranges ??= [];
                ranges.Add((index, start));
            }

            index = script.IndexOf(PseudoValuePrefix, start);
        }

        if (ranges is null)
        {
            return script;
        }

        int begin = 0;
        StringBuilder result = new(capacity: script.Length);
        foreach (var (start, end) in ranges)
        {
            string pseudoValue = script[start..end];

            if (_pseudoToRealValueMap.TryGetValue(pseudoValue, out string realValue))
            {
                // We found the corresponding real value.
                result.Append(script, begin, start - begin).Append(realValue);
            }
            else
            {
                // No corresponding real value.
                result.Append(script, begin, end - begin);
            }

            begin = end;
        }

        // Append the rest of the original script.
        if (begin != script.Length)
        {
            result.Append(script, begin, script.Length - begin);
        }

        return result.ToString();
    }

    internal static void FilterOutPseudoValues(ResponseData data)
    {
        List<PlaceholderItem> phList = data.PlaceholderSet;
        if (phList is null || phList.Count is 0)
        {
            return;
        }

        List<int> indices = null;
        int minLength = PseudoValuePrefix.Length + PseudoValueSuffix.Length;

        for (int i = 0; i < phList.Count; i++)
        {
            string name = phList[i].Name;
            if (name.StartsWith('$'))
            {
                continue;
            }

            // Check if the name is actually a pseudo value.
            if (name.Length > minLength
                && name.StartsWith(PseudoValuePrefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(PseudoValueSuffix, StringComparison.OrdinalIgnoreCase)
                && DigitsOnly(name, PseudoValuePrefix.Length, name.Length - PseudoValueSuffix.Length))
            {
                indices ??= [];
                indices.Add(i);
            }
        }

        // Remove pseudo values from the placeholder set if there is any.
        if (indices is not null)
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                phList.RemoveAt(indices[i]);
            }
        }
    }
}
