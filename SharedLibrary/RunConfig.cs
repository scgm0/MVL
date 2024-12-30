namespace SharedLibrary;

public record struct RunConfig() {
	public required string VintageStoryPath { get; init; }

	public required string VintageStoryDataPath { get; init; }

	public string AssemblyPath { get; init; } = "Vintagestory.dll";

	public required ExecutableTypeEnum ExecutableType { get; init; }

	public bool UseAnsiLogger { get; init; } = true;
}