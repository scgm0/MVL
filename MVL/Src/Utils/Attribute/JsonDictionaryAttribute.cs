using System;

namespace MVL.Utils.Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IconDictionaryAttribute(string jsonPath) : System.Attribute {
	public string IconJsonPath { get; } = jsonPath;
}