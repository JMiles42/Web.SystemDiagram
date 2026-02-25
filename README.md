# DiagramSample

Sample ASP.NET Core (.NET 8) application that loads a YAML configuration and renders a systems diagram using Cytoscape.js.

Quick start

1. Ensure you have .NET 8 SDK installed.
2. (Important) Download Cytoscape.js and place the single-file build at `src/DiagramSample/wwwroot/lib/cytoscape/cytoscape.min.js` (replace the placeholder). See https://js.cytoscape.org/
3. From workspace root run:

```pwsh
cd src/DiagramSample
dotnet restore
dotnet run
```

4. Open `http://localhost:5000` (or the URL printed by `dotnet run`).

YAML schema overview

- `platforms[]`, `applications[]`, `modules[]` each contain nodes with:
  - `id` (string)
  - `type` (platform|application|module)
  - `displayName` (string, can be empty)
  - `applications` (platform -> app ids)
  - `modules` (application -> module ids)

- `links[]` entries:
  - `id`, `from`, `to`, `displayName`, optional `kind`, optional `via: [nodeId...]`.
  - `via` is expanded into segmented edges: `from -> via[0] -> ... -> to`. Segments keep `originalLinkId`.

- `businessProcesses[]`:
  - `id`, `displayName`, `steps[]` (each step `{ node: "id" }` or `{ link: "id" }`), optional `color`, `hiddenByDefault`.

Notes & limitations

- The repo includes a placeholder `cytoscape.min.js`; replace it with a real build for full rendering.
- YAML is loaded from `Config/diagram.yaml` on every request; errors in YAML will be returned as 500 with an error message.
- Display name hiding rule: nodes/edges with empty `displayName` do not show labels.
- Link routing: `via` generates multiple segments. Only the first segment will show the displayName label.
