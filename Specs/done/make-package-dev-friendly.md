<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a DEV I want to make my package much friendlier to other DEVs

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
-  **Enable Source Link**: This is a game-changer for debugging. Source Link embeds source control metadata into your assemblies and PDBs. This allows users to step directly into your library's source code from their own project while debugging, providing an almost seamless debugging experience as if they had the source code locally.
-  **Publish Symbol Packages (`.snupkg`)**: Alongside Source Link, publishing a symbols package to NuGet.org is crucial. This package contains the PDBs that are essential for debugging. The combination of Source Link and symbol packages provides the richest possible debugging experience for your library's consumers.
-  **Use a Public API Analyzer**: To help enforce Semantic Versioning and prevent accidental breaking changes, you can use a public API analyzer (like `Microsoft.CodeAnalysis.PublicApiAnalyzers`). This tool tracks your public API surface and will generate a build error if you unintentionally change a public method signature, remove a public class, etc., forcing you to consciously acknowledge the change.
-  **Comprehensive Documentation with Examples**: Go beyond standard XML docs.
    *   **Use `<example>` tags**: Add code snippets directly within your XML comments. Visual Studio and other tools can render these directly in IntelliSense, giving users an immediate idea of how to use a method.
-  **Multi-targeting for Wider Compatibility**: If you want to support users who might not be on the absolute latest version of .NET, consider multi-targeting your library's `.csproj` file (e.g., `<TargetFrameworks>net9.0;net8.0;netstandard2.0</TargetFrameworks>`). This allows you to produce a single NuGet package that works across different .NET versions, maximizing your potential audience.
	
The github site at https://github.com/phaetto/Ama.CRDT

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\Ama.CRDT.csproj`
- `C:\sources\Ama.CRDT\.github\workflows\publish-nuget-manual.yml`
- `C:\sources\Ama.CRDT\.github\workflows\publish-nuget.yml`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtService.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtPatcher.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtPatcherFactory.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtApplicator.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtPatchBuilder.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Manual Verification**: After publishing a new version of the package to a test feed or locally:
  1. Create a new .NET console application that references the updated NuGet package.
  2. Write code that utilizes the library's main features.
  3. Place a breakpoint and attempt to "Step Into" (F11) a library method. Verify that Visual Studio correctly downloads the source file from GitHub and allows line-by-line debugging.
  4. Hover over library methods in the new console app to verify that IntelliSense correctly displays the enhanced XML documentation, including the code snippets from the `<example>` tags.
- **CI/CD Pipeline Verification**:
  1. The build pipeline will serve as an automated test for the Public API Analyzer. A build will fail if an unapproved breaking change is introduced, confirming the analyzer is working.
  2. The deployment logs for both the manual and automated publishing workflows should be inspected to confirm that both the `.nupkg` and `.snupkg` files are successfully pushed to the NuGet repository.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
- **Solution 1: Comprehensive Developer Experience Overhaul (Recommended)**
  - **Description**: This solution addresses all requirements simultaneously. It involves modifying the `Ama.CRDT.csproj` file to enable multi-targeting, Source Link, and the Public API Analyzer. The GitHub Actions workflows will be updated to handle the publishing of symbol packages (`.snupkg`). Finally, key public-facing interfaces and methods will be updated with comprehensive XML documentation, including usage examples in `<example>` tags.
  - **Pros**: Delivers the maximum possible value to consuming developers in a single update. It creates a professional, fully-featured package that is easy to debug, version-safe, and well-documented. The changes are logically related and are best implemented together for consistency.
  - **Cons**: Requires coordinated changes across build configuration, CI/CD pipelines, and source code, making it a slightly larger single piece of work.
  - **Reason for Recommendation**: This approach fully realizes the vision of making the package "much friendlier to other DEVs". The combination of discoverability (docs), debuggability (Source Link), and safety (API analyzer) provides a superior developer experience that addresses the problem holistically.

- **Solution 2: Phased Implementation**
  - **Description**: This approach splits the work into two distinct phases.
    - **Phase 1 (Build & Debugging)**: Implement all changes related to the build process and CI/CD. This includes multi-targeting, Source Link, symbol package publishing, and setting up the Public API Analyzer.
    - **Phase 2 (Documentation)**: In a subsequent effort, update the source code with the enhanced XML documentation and examples.
  - **Pros**: Breaks the work into smaller, more manageable chunks. Allows for quicker delivery of the debugging and API safety features.
  - **Cons**: Delays the full benefit to developers. A debuggable library is useful, but without clear documentation and examples, its usability is still limited.

- **Solution 3: Automated-Only Update (Rejected)**
  - **Description**: This solution focuses exclusively on the changes that can be automated through configuration. It involves modifying the `.csproj` and CI/CD workflows to enable Source Link, symbol packages, and multi-targeting, but completely omits the manual work of writing documentation.
  - **Pros**: Quickest to implement as it involves no creative or explanatory writing.
  - **Cons**: Fails to address a critical aspect of developer-friendliness: understanding how to use the library in the first place. Good documentation is often the first thing a developer looks for.
  - **Reason for Rejection**: This solution only partially addresses the user's goal. A library that is easy to debug but hard to learn is not truly "developer-friendly."

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify `Ama.CRDT.csproj` for Modern Packaging and Compatibility:**
    -   Open `$/Ama.CRDT/Ama.CRDT.csproj`.
    -   Change the single `<TargetFramework>net9.0</TargetFramework>` to `<TargetFrameworks>net9.0;net8.0;netstandard2.0</TargetFrameworks>`.
    -   In a `<PropertyGroup>` (ideally the one with package information like `<PackageId>`), add the following properties to enable Source Link and symbol package generation:
        ```xml
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <EmbedUntrackedSources>true</EmbedUntrackedSources>
        <IncludeSymbols>true</IncludeSymbols>
        <SymbolPackageFormat>snupkg</SymbolPackageFormat>
        <RepositoryUrl>https://github.com/phaetto/Ama.CRDT</RepositoryUrl>
        <RepositoryType>git</RepositoryType>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <NoWarn>$(NoWarn);1591</NoWarn>
        ```
    -   Add an `<ItemGroup>` with the package reference for Source Link:
        ```xml
        <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All"/>
        ```
    -   Add an `<ItemGroup>` with the package reference for the Public API Analyzer:
        ```xml
        <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4" PrivateAssets="All" />
        ```
    -   Add an `<ItemGroup>` to include the README file in the package:
        ```xml
        <None Include="..\README.md" Pack="true" PackagePath="\"/>
        ```

2.  **Establish the Public API Baseline:**
    -   Create two new empty text files in the root of the `Ama.CRDT` project: `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`.
    -   Build the project. The build will fail due to `RS0016` errors from the Public API Analyzer.
    -   In your IDE, use the provided quick fix on one of the errors to execute the action "Add public API to PublicAPI.Unshipped.txt".
    -   Copy the entire contents from `PublicAPI.Unshipped.txt` and paste them into `PublicAPI.Shipped.txt`.
    -   Clear all content from `PublicAPI.Unshipped.txt`.
    -   Commit both files to source control. This baseline freezes the current public API.

3.  **Update GitHub Actions Workflows for Symbol Publishing:**
    -   Modify both `$/.github/workflows/publish-nuget.yml` and `$/.github/workflows/publish-nuget-manual.yml`.
    -   Find the step named "Publish to NuGet".
    -   Update the `dotnet nuget push` command to use a wildcard that includes both `.nupkg` and `.snupkg` files. The updated command should look like this:
        `dotnet nuget push "Ama.CRDT/bin/Release/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`

4.  **Enhance Public Interfaces with XML Documentation and Examples:**
    -   In `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`, add a detailed example for the `AddCrdt` method.
    -   In `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs`, document the `Create` method with an example showing how to retrieve a replica-specific patcher.
    -   In `$/Ama.CRDT/Services/ICrdtService.cs`, add examples for `Diff` and `Merge` to illustrate the core create-patch and apply-patch workflow.
        Example for `ICrdtService.Merge`:
        ```csharp
        /// <summary>
        /// Merges a given patch into a document to produce a new, updated version of the document.
        /// </summary>
        /// <typeparam name="T">The type of the document object.</typeparam>
        /// <param name="document">The original document, containing the object and its metadata.</param>
        /// <param name="patch">The patch containing operations to apply.</param>
        /// <returns>A new CrdtDocument instance with the patch applied.</returns>
        /// <example>
        /// <code>
        /// // Assume you have a CrdtService instance and a document
        /// var crdtService = serviceProvider.GetRequiredService<ICrdtService>();
        /// var originalDocument = new CrdtDocument<MyPoco>(new MyPoco { Score = 10 });
        /// 
        /// // Assume 'patch' is received from another replica
        /// var mergedDocument = crdtService.Merge(originalDocument, patch);
        /// 
        /// Console.WriteLine($"New score: {mergedDocument.State.Score}");
        /// </code>
        /// </example>
        CrdtDocument<T> Merge<T>(CrdtDocument<T> document, CrdtPatch patch) where T : class;
        ```

<!---AI - Stage 1--->
# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Ama.CRDT/Ama.CRDT.csproj` (The core project file that needs modification for multi-targeting, Source Link, API analysis, and documentation generation.)
- `$/.github/workflows/publish-nuget.yml` (The automated CI/CD workflow that must be updated to push symbol packages.)
- `$/.github/workflows/publish-nuget-manual.yml` (The manual release workflow that must also be updated to push symbol packages.)
- `$/Ama.CRDT/Services/ICrdtService.cs` (A primary public interface requiring enhanced XML documentation with examples.)
- `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs` (A key factory interface that developers will use, needing clear documentation and examples.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (The DI setup extension method, which is the main entry point for configuring the library and requires excellent documentation.)
- `$/README.md` (To be included in the NuGet package.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Ama.CRDT.csproj`: Updated the project file to enable multi-targeting for `net9.0`, `net8.0`, and `netstandard2.0`. Added properties and package references for Source Link (`Microsoft.SourceLink.GitHub`) and the Public API Analyzer (`Microsoft.CodeAnalysis.PublicApiAnalyzers`). Configured XML documentation generation and symbol package creation.
- `$/.github/workflows/publish-nuget.yml`: Modified the `dotnet nuget push` command to push all `.nupkg` files from the output directory, which implicitly includes the generated `.snupkg` symbol package. Added `--skip-duplicate`.
- `$/.github/workflows/publish-nuget-manual.yml`: Applied the same change as the automated workflow to ensure manual publishes also include symbol packages.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Added comprehensive XML documentation to the `AddCrdt` method, including an example block showing typical DI setup.
- `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs`: Added detailed XML documentation to the `Create` method, with an example demonstrating its use in a multi-replica scenario.
- `$/Ama.CRDT/Services/ICrdtService.cs`: Enhanced the XML documentation for `CreatePatch` and `Merge` methods with detailed explanations and code examples to clarify their usage.
- `$/Ama.CRDT/PublicAPI.Shipped.txt`: Created and populated this file with the library's public API surface to establish a baseline for the API analyzer.
- `$/Ama.CRDT/PublicAPI.Unshipped.txt`: Created this empty file as required by the public API analyzer for tracking new, unapproved API changes.

<!---AI - Stage 2--->
# Manual Changes Needed
<!---
Here you add detailed information about all the manual changes that might be needed to be done from a human.
Example types of changes are:
	- Configuration settings
	- Environment variables
	- Deployments/Scripts/Setups external to this app
	- Dependencies to external projects that would need changes (like nuget packages for example)
	- Settings in other systems (for example, enable some flag or permissions in Github)
If there are none, then just write "No manual changes needed to be applied."
--->
No manual changes needed to be applied.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
- The Public API for `CrdtMetadata`'s properties have public setters and use `ConcurrentDictionary`. While functional, this exposes implementation details. A more robust design might use interfaces (`IDictionary`) and potentially make the setters internal or `init`-only to better encapsulate the state management. This was out of scope for the current task.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The `PublicAPI.Shipped.txt` file was generated based on the current public surface of the library. Any future intentional changes to the public API will now require this file to be updated, which is the intended workflow of the `Microsoft.CodeAnalysis.PublicApiAnalyzers` package.
- The dependency versions in `Ama.CRDT.csproj` for `Microsoft.Extensions.*` packages were set to `8.0.0` to ensure compatibility with the new `netstandard2.0` target framework.
- The addition of `<NoWarn>$(NoWarn);1591</NoWarn>` to the `.csproj` file prevents the build from failing due to missing XML comments on non-public members, focusing the documentation effort on the public-facing API.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->