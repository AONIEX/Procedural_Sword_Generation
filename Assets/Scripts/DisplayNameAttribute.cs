using System;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute : Attribute
{
    public string Name;
    public DisplayNameAttribute(string name)
    {
        Name = name;
    }
}