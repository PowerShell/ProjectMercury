using System.Reflection;

namespace AIShell.Abstraction;

public interface IRenderElement<T>
{
    string Name { get; }
    string Value(T obj);
}

public sealed class PropertyElement<T> : IRenderElement<T>
{
    private readonly string _propertyName;
    private readonly PropertyInfo _propertyInfo;

    public PropertyElement(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        _propertyName = propertyName;
        _propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (_propertyInfo is null || !_propertyInfo.CanRead)
        {
            throw new ArgumentException($"'{propertyName}' is not a public instance property or it's write-only.", nameof(propertyName));
        }
    }

    public PropertyElement(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        Type type = typeof(T);
        if (type != property.ReflectedType)
        {
            throw new ArgumentException($"The passed-in property is not retrieved from the target type '{type.FullName}'.", nameof(property));
        }

        if (!property.CanRead)
        {
            throw new ArgumentException($"The property '{property.Name}' is write-only.", nameof(property));
        }

        _propertyName = property.Name;
        _propertyInfo = property;
    }

    public string Name => _propertyName;
    public string Value(T source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _propertyInfo.GetValue(source)?.ToString();
    }
}

public sealed class KeyValueElement<T> : IRenderElement<T>
    where T : IDictionary<string, string>
{
    private readonly string _key;

    public KeyValueElement(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _key = key;
    }

    public string Name => _key;
    public string Value(T source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(_key, out string value) ? value : null;
    }
}

public sealed class CustomElement<T> : IRenderElement<T>
{
    private readonly string _label;
    private readonly Func<T, string> _valueFunc;

    public CustomElement(string label, Func<T, string> valueFunc)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(valueFunc);

        _label = label;
        _valueFunc = valueFunc;
    }

    public string Name => _label;
    public string Value(T source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _valueFunc(source);
    }
}
