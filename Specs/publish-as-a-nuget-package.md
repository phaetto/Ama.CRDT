<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want to be able to publish the main CRDT project to nuget.org

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- We need a github action that will be able to publish the master branch with semantic versioning.
- The major and minor version should be set as a variable in git action.
- The patch version should be an incremental value derived from date and time.
- The code assistant needs to remind me when to increase the minor (or major) component by the changes. So you should add a clarification text in our coding standards for this solution.
- The first version that we will start would be 0.1
- Make sure that only our main project is packable and publishable.
- Run the build and unit tests before.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\CodingStandards.md`
- `C:\sources\Ama.CRDT\Ama.CRDT\Ama.CRDT.csproj`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
The testing for this feature will be manual. It involves the following steps:
1. Pushing a change to the `master` branch.
2. Observing the execution of the new GitHub Action.
3. Verifying that the build and test steps pass successfully.
4. Confirming that a new version of the package is published to nuget.org with the correctly formatted version number (e.g., `0.1.YYMMDDHHMM`).

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: GitHub Actions with .NET CLI (Recommended)
- **Description:** This approach involves creating a standard GitHub Actions workflow file. The workflow will use the native .NET CLI commands (`dotnet build`, `dotnet test`, `dotnet pack`, and `dotnet nuget push`) to perform all necessary steps. Versioning will be handled within the workflow by combining hardcoded `MAJOR` and `MINOR` versions with a dynamically generated `PATCH` version based on the current UTC date and time.
- **Pros:**
    - Directly meets all requirements using standard, well-documented tools.
    - Simple and maintainable workflow definition.
    - Provides clear separation of build, test, and publish steps.
- **Cons:**
    - The date/time-based patch number is not strictly sequential if multiple pushes occur in the same minute, but it is unique and time-ordered, satisfying the requirement.

### Solution 2: GitHub Actions with Nerdbank.GitVersioning (Rejected)
- **Description:** This solution would use the `Nerdbank.GitVersioning` tool to automatically determine the package version based on the Git history (tags and commit height). A `version.json` file would control the major/minor components.
- **Reason for Rejection:** This approach does not align with the specific requirement to have the Major and Minor versions as simple variables in the workflow file and the patch version based on a date/time stamp. It introduces unnecessary complexity for the requested versioning scheme.

### Solution 3: Manual Publishing Script (Rejected)
- **Description:** This would involve creating a local shell script that a developer runs to build, test, package, and publish the library.
- **Reason for Rejection:** This solution is not automated and is prone to human error. It completely violates the core requirement of creating an automated GitHub Action for publishing on pushes to the `master` branch.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify `Ama.CRDT.csproj`:**
    *   Update the project file to include properties required for NuGet packaging.
    *   Set `<IsPackable>true</IsPackable>` to allow the project to be packaged.
    *   Add metadata tags such as `<PackageId>`, `<Authors>`, `<Description>`, `<RepositoryUrl>`, and `<PackageLicenseExpression>`.

2.  **Create GitHub Action Workflow:**
    *   Create a new file at `$/.github/workflows/publish-nuget.yml`.
    *   The workflow will be triggered on pushes to the `master` branch.
    *   It will define two environment variables for the version: `MAJOR_VERSION: 0` and `MINOR_VERSION: 1`.
    *   It will consist of two sequential jobs:
        1.  **`build-and-test`:** Checks out the code, sets up the .NET environment, and runs `dotnet build` and `dotnet test` across the solution.
        2.  **`publish`:** This job will only run if `build-and-test` succeeds. It will:
            *   Generate a patch version using the format `YYMMDDHHMM` from the current UTC time.
            *   Run `dotnet pack` on the `Ama.CRDT` project, passing the full `Major.Minor.Patch` version.
            *   Run `dotnet nuget push` to publish the generated package to nuget.org, using a `NUGET_API_KEY` provided via GitHub secrets.

3.  **Update `CodingStandards.md`:**
    *   Add a new section titled "Versioning and Publishing".
    *   Document the `Major.Minor.Patch` strategy, explaining that `Major.Minor` is controlled via variables in the GitHub Action and `Patch` is automatically generated.
    *   Add a directive for the AI assistant: "The assistant must remind the developer to consider incrementing the `MAJOR_VERSION` or `MINOR_VERSION` variables in `/.github/workflows/publish-nuget.yml` when implementing significant new features, breaking changes, or after a series of cumulative smaller changes."

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
- `$/Ama.CRDT/Ama.CRDT.csproj` (To add NuGet packaging properties)
- `$/CodingStandards.md` (To add the new versioning and publishing guidelines)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Ama.CRDT.csproj`: Added a new `<PropertyGroup>` with all the necessary metadata for NuGet packaging, including `<IsPackable>`, `<PackageId>`, `<Version>`, `<Authors>`, `<Description>`, `<RepositoryUrl>`, `<PackageLicenseExpression>`, and `<PackageTags>`. This makes the main project packable.
- `$/.github/workflows/publish-nuget.yml`: Created a new GitHub Actions workflow file. This workflow triggers on pushes to `master`, builds and tests the solution, and then publishes the `Ama.CRDT` project to NuGet. It uses environment variables for the major/minor version and generates a date-time stamp for the patch version, as required.
- `$/CodingStandards.md`: Appended a new "Versioning and Publishing" section that documents the versioning strategy and includes a specific directive for the AI assistant to remind developers about version increments.
- `$/FilesDescription.md`: Added an entry for the new `publish-nuget.yml` file and updated descriptions for `Ama.CRDT.csproj` and `CodingStandards.md` to reflect the changes.

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
- **GitHub Repository Secret:** A secret named `NUGET_API_KEY` must be created in the GitHub repository's settings (`Settings > Secrets and variables > Actions`). The value of this secret should be a valid API key from nuget.org with permissions to push packages.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
No technical debt was identified or introduced.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The versioning strategy `Major.Minor.DateTime` ensures that every push to `master` results in a unique, time-ordered package version.
- The workflow is split into two jobs (`build-and-test` and `publish`) to ensure that publishing only occurs after all builds and tests have passed successfully.
- Other projects in the solution (Benchmarks, ShowCase, UnitTests) are not packable by default, so explicitly setting `<IsPackable>true</IsPackable>` only in `Ama.CRDT.csproj` is sufficient to meet the requirement that only the main project is published.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->