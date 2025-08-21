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

The files should be structured like the following:
Conside solution root as $/
Services files should always got to $/<Project root>/Services folder.
Interfaces for the above services should also always got to $/<Project root>/Services folder.
Model files should always code to $/<Project root>/Models folder.
Extensions should always code to $/<Project root>/Extensions folder.
Plugins for semantic kernel should always code to $/<Project root>/Plugins folder.
Global constants files should always code to $/<Project root>/Constants.cs file.
Command files should always got to $/<Project root>/Services/Commands folder.
