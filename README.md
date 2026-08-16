## Installation

SudScript CLI requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

Install SudScript globally using:

```bash
dotnet tool install --global sudscript**
```

## Usage
Create new Project
```bash
cd {project_directory}
sud new {project_name}
```

Run current Project
```bash
cd {project_directory}
sud run
```

Build current Project
(Creates a ./build folder and generates an imbedded C# project, and then builds that into a standalone application)
```bash
cd {project_directory}
sud Build
```

Uninstall SudScript
```bash
dotnet tool uninstall --global sudscript
```
