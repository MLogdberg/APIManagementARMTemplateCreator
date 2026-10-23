# Unique names for nested deployments in generated master templates

## Problem

`TemplatesGenerator` generates a `master.template.json` that deploys every sub-template as a nested
`Microsoft.Resources/deployments` resource. The deployment **name** is the fixed file name of the sub-template
(`template.GetShortPath()`), for example:

- `service.template.json`
- `users.template.json`, `groups.template.json`, `groupsUsers.template.json`, `subscriptions.template.json`
- `api-<name>.template.json`, `api-<name>.swagger.template.json`, `api-<name>.version-set.template.json`
- `product-<id>.template.json`

ARM deployment names are unique per resource group. All APIs and Products deploy into the same APIM resource group
(`dev-apim-westeu`, `ta-apim-westeu`, `prd-...`). When two releases run at the same time, they create nested deployments
with the same name (almost always `service.template.json`, and the users/groups/subscriptions templates). The second
release then fails with:

```
DeploymentActive: Unable to edit or replace deployment 'service.template.json': previous deployment from '...' is still active
```

or it overwrites the deployment history of the other release. This affects **API templates and Product templates**, because
both are generated through the same `GenerateDeployment` / `GenerateDeploymentDependsOn` code path.

The outer deployment name is already unique: the DeployWithMultipleArmFiles v2 task group uses
`master-<yyyyMMdd-HHmmss>-<random>`. The nested names must be made unique per run by deriving them from that name.

## Required change

File: `APIManagementTemplate/TemplatesGenerator.cs`

### 1. Add one helper that builds the nested deployment name expression

Add a private static helper that returns the ARM expression (without the surrounding `[` `]`) for a sub-template:

```csharp
private static string GetDeploymentNameExpression(string shortPath)
{
	// Max deployment name length is 64; uniqueString is 13 chars, so the readable part is limited to 50 chars.
	var readable = shortPath.Replace(".template.json", string.Empty);
	if (readable.Length > 50)
		readable = readable.Substring(0, 50);
	return $"concat('{readable}-', uniqueString(deployment().name, '{shortPath}'))";
}
```

Requirements:
- Result must be ≤ 64 characters and only contain characters allowed in deployment names (`[-\w._()]`).
- Must be deterministic **within one run** (so `dependsOn` resolves) and different **between runs**
  (because `deployment().name` of the master differs).
- Include `shortPath` in `uniqueString` so two templates whose readable part is truncated to the same 50 chars stay unique.
- Keep the readable part, so the deployment history in the portal remains understandable.

### 2. Use it in `GenerateDeployment`

```csharp
name = $"[{GetDeploymentNameExpression(template2.GetShortPath())}]",
```

### 3. Use it in `GenerateDeploymentDependsOn` (both places)

```csharp
dependsOn.Add($"[resourceId('Microsoft.Resources/deployments', {GetDeploymentNameExpression(match.GetShortPath())})]");
```

and for the swagger template:

```csharp
dependsOn.Add($"[resourceId('Microsoft.Resources/deployments', {GetDeploymentNameExpression(template.FileName.Replace(".swagger.template.json", ".template.json"))})]");
```

Check that the swagger case uses the same value as the `GetShortPath()` of the referenced API template (directory prefix);
if `GetShortPath()` includes the directory, use the referenced `GeneratedTemplate.GetShortPath()` instead of the raw file name.

### 4. Search for other hard-coded references

Search the solution for every other place that builds a `Microsoft.Resources/deployments` name or `resourceId(...)`
to a nested deployment (e.g. API master templates per directory, `*.master.template.json`, version sets, `reference(...)`/
`outputs` that use the deployment name) and apply the same helper. Nothing may refer to the old literal file name as
a deployment name anymore.

## Tests

Project: `APIManagementTemplate.Test` (MSTest). Update and add tests:

- Update `AssertMasterTemplateDeployment` and the tests using it (`service.template.json`,
  `product-unlimited.template.json`, ...): expect `name` to equal
  `[concat('<readable>-', uniqueString(deployment().name, '<shortPath>'))]`.
- Update tests that assert `dependsOn` values to the new `resourceId(...)` expression.
- New test: every `Microsoft.Resources/deployments` name in every generated master template is an expression that contains
  `deployment().name` (no literal names left).
- New test: every `dependsOn` entry that targets `Microsoft.Resources/deployments` matches the `name` of a deployment in the
  same master template.
- New test: a very long API name produces a readable part of max 50 characters.
- Cover both an API master template and a Product template.

## Validation

1. Build the solution and run all tests.
2. Regenerate the templates for one API and one Product and run
   `az deployment group validate` (or `what-if`) against `dev-apim-westeu`.
3. Start two releases on DEV at the same time that both include `service.template.json`; both must succeed and the
   deployment history must show two distinct `service-<hash>` deployments.

## Out of scope / notes

- Do not change the outer deployment name; the release task group already makes it unique.
- Old nested deployments with literal names stay in the deployment history; they do not need cleaning up
  (the resource group has an 800-deployment history limit that ARM trims automatically).
- After this change, all API and Product templates in the IntegratieEnOntwikkeling repo must be regenerated to profit
  from the fix.
