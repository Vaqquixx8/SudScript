using System.Reflection;
using System.Runtime.Loader;

namespace SudScript;

public static class ExternalLibraryLoader
{
	public static void LoadDirectory(string directory)
	{
		if (!Directory.Exists(directory))
		{
			return;
		}

		foreach (string filePath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
		{
			LoadAssembly(filePath);
		}
	}

	static void LoadAssembly(string filePath)
	{
		string libraryDirectory = Path.GetDirectoryName(filePath)!;

		var loadContext = new ExternalLibraryLoadContext(libraryDirectory);

		Assembly assembly =loadContext.LoadFromAssemblyPath(Path.GetFullPath(filePath));

		foreach (Type type in assembly.GetExportedTypes())
		{
			if (type.IsAbstract || type.IsInterface)
			{
				continue;
			}

			if (!typeof(ILibrary).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type)
				is not ILibrary library)
			{
				continue;
			}

			Libraries.Register(library);
		}
	}

	sealed class ExternalLibraryLoadContext : AssemblyLoadContext
	{
		readonly string directory;

		public ExternalLibraryLoadContext(string _directory) : base(isCollectible: false)
		{
			directory = _directory;
		}

		protected override Assembly? Load(AssemblyName assemblyName)
		{
			string dependencyPath = Path.Combine(directory, assemblyName.Name + ".dll");

			if (File.Exists(dependencyPath))
			{
				return LoadFromAssemblyPath(dependencyPath);
			}

			return null;
		}
	}
}
