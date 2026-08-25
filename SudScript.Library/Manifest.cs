namespace SudScript;

public class Manifest
{
	public string? Project;
	public string? Entry;
	public string? Modules;
	public string? Libraries;

	public static Manifest Load(string path)
	{
		Manifest manifest = new Manifest();

		foreach(string line in File.ReadLines(path))
		{
			string trimmed = line.Trim();

			if(string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
			{
				continue;
			}
			int index = trimmed.IndexOf('=');
			if(index == -1)
			{
				continue;
			}

			string key = trimmed[..index].Trim();
			string value = trimmed[(index+1)..].Trim().Trim('"');

			switch (key)
			{
				case "project":
					manifest.Project = value;
					break;
				case "entry":
					manifest.Entry = value;
					break;
				case "modules":
					manifest.Modules = value;
					break;
				case "libraries":
					manifest.Libraries = value;
					break;
			}
		}
		return manifest;
	}
}
