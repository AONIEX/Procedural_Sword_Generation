using System;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute : Attribute
{
    public string DisplayName;
    public string Section;
    public int Order;
    public string SubSection;
    public bool IsAdvanced;

    public DisplayNameAttribute(
       string displayName,
       string section = "General",
       int order = 0,
       string subSection = "Basic",
       bool isAdvanced = false)

    {
        DisplayName = displayName;
        Section = section;
        Order = order;
        SubSection = subSection;
        IsAdvanced = isAdvanced;
    }


}


[AttributeUsage(AttributeTargets.Field)]
public class HideInUIAttribute : Attribute
{
    // Simple marker attribute - no properties needed
}