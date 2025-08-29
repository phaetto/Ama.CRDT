# Coding standards

You are using the following coding standards always:
Code for .NET 9.
You should only use DI.
All field names should not be prefixed by underscore.
You should use interfaces for IEnumerable, IList, IDictionary when possible.
Add sealed in the classes that are not inherited.
Use readonly record struct for data structures.
Do not put comments in code. Only if there is something unique happening.
Interfaces should be in their own file.
Use xUnit, Moq and Shouldly when changing things in unt tests.
Put all the private methods towards the end of a file.
Always protect for null or empty inputs on a method.
Use IHttpClientFactory to manage HttpClient always.
Instead of ILogger you should use ConsoleExtensions.
The integration tests that make requests need to be by default skipped.
Put namespaces inside the main namespace and only use file-scoped namespaces for files.
Interfaces should always include deatiled XmlDoc comments.
Never use tuples, only construct DTOs to pass ar retrieve data.
Always introduce models with implementation of `IEquatable<>` and be explicit when the model using ISet, IEnumerable or other deep structures.

# Filesystem structure

The files should be structured like the following:
Conside solution root as `$/`
Services files should always got to `$/<Project root>/Services` folder.
Interfaces for the above services should also always got to `$/<Project root>/Services` folder.
Model files should always code to `$/<Project root>/Models` folder.
Extensions should always code to `$/<Project root>/Extensions` folder.
Plugins for semantic kernel should always code to `$/<Project root>/Plugins` folder.
Global constants files should always code to `$/<Project root>/Constants.cs` file.
Command files should always got to `$/<Project root>/Services/Commands` folder.

Never change $/Ama.CRDT/PublicAPI.Unshipped.txt and $/Ama.CRDT/PublicAPI.Shipped.txt as they need to be used manually.

 **Remember the escaping:** Please escape the blocks ``` in files correctly like for README.md file.

# Versioning and Publishing
The project uses semantic versioning in the format `Major.Minor.Patch`.

- **Major/Minor Version**: These are controlled by the `MAJOR_VERSION` and `MINOR_VERSION` environment variables in the `/.github/workflows/publish-nuget.yml` workflow file. They should be updated manually when new features or breaking changes are introduced.
- **Patch Version**: This is generated automatically by the CI/CD pipeline upon a push to the `master` branch. The format is `YYMMDDHHMM` (e.g., `2408151430`).

**Guidance for the AI Assistant:** The assistant must remind the developer to consider incrementing the `MAJOR_VERSION` or `MINOR_VERSION` variables in `/.github/workflows/publish-nuget.yml` when implementing significant new features, breaking changes, or after a series of cumulative smaller changes.