namespace SharedLibrary;

public record struct RunConfig() {
	public string VintageStoryPath { get; set; } = string.Empty;

	public string VintageStoryDataPath { get; set; } = string.Empty;

	public string AssemblyPath { get; set; } = "Vintagestory.dll";

	public ExecutableTypeEnum ExecutableType { get; set; }

	public bool UseAnsiLogger { get; set; } = true;
}