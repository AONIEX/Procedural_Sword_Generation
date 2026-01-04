using System;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute : Attribute
{
    public string DisplayName;
    public string Section;
    public int Order;

    public DisplayNameAttribute(
       string displayName,
       string section = "General",
       int order = 0)
    {
        DisplayName = displayName;
        Section = section;
        Order = order;
    }


}


[AttributeUsage(AttributeTargets.Field)]
public class HideInUIAttribute : Attribute
{
    // Simple marker attribute - no properties needed
}