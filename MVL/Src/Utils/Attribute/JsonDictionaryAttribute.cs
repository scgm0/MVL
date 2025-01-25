using System;

namespace MVL.Utils.Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonDictionaryAttribute(string jsonPath) : System.Attribute {
	public string JsonPath { get; } = jsonPath;
}