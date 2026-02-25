# Agent Task: Sample ASP.NET app that self-hosts Cytoscape.js and renders a systems diagram from YAML

## Goal
Create a **sample ASP.NET Core** application that:

1. **Self-hosts Cytoscape.js** (served from `wwwroot`, no CDN usage).
2. Loads a **YAML configuration file** from disk at runtime (server-side).
3. Converts YAML into a **graph model** and renders it in the browser using Cytoscape.js.
4. Supports these domain concepts:
   - **Platform contains applications**
   - **Application contains modules**
   - **Applications and modules can link to other applications, modules, or platforms**
   - Links can have **labels**
   - Links can optionally be **routed through** other nodes (e.g., proxy)
   - **Business processes** define a flow through platforms/apps/modules/links used to complete a job
5. Supports display rules:
   - Every object/link has `displayName`
   - If `displayName` is empty or null, the label is hidden; otherwise it is shown.
6. UI supports:
   - User can **toggle visibility of business processes** (show/hide selected processes).
   - Diagram updates dynamically without full page reload.
7. Provide a working sample YAML file and a couple of sample platforms/apps/modules/links/processes.

## Tech constraints / preferences
- Use **ASP.NET Core** (choose .NET 8 minimal hosting).
- Use minimal APIs
- Use standard C# naming standards
- UI can be plain HTML + vanilla JS (no React required).
- YAML parsing should be done in .NET using **YamlDotNet**.
- Cytoscape.js must be included as a local static asset under `wwwroot/lib/cytoscape/` (or similar).
- No external services required.

---

## Repository structure (expected)
Create something similar to:

- `src/DiagramSample/DiagramSample.csproj`
- `src/DiagramSample/Program.cs`
- `src/DiagramSample/Controllers/DiagramController.cs` (or minimal endpoints in Program.cs)
- `src/DiagramSample/Services/DiagramConfigLoader.cs`
- `src/DiagramSample/Models/DiagramConfig.cs` (+ related model types)
- `src/DiagramSample/Config/diagram.yaml` (sample input)
- `src/DiagramSample/wwwroot/index.html`
- `src/DiagramSample/wwwroot/js/app.js`
- `src/DiagramSample/wwwroot/css/site.css`
- `src/DiagramSample/wwwroot/lib/cytoscape/cytoscape.min.js` (committed vendor asset)

If you prefer Razor Pages instead of static `index.html`, that’s fine; but keep it simple.

---

## YAML schema requirements
Define and document a YAML schema that can express:

### Nodes
- `platforms[]`
- `applications[]`
- `modules[]`

Each node must have:
- `id` (unique, stable string)
- `type` (`platform|application|module`)
- `displayName` (string, can be empty)
- containment:
  - platform has `applications: [appId...]`
  - application has `modules: [moduleId...]`
- optional metadata:
  - `tags: []`
  - `owner: string`
  - etc (optional)

### Links
- `links[]` with:
  - `id`
  - `from` (node id)
  - `to` (node id)
  - `displayName` (string; hide if empty)
  - optional `kind` (e.g., `http`, `event`, `db`, `internal`, `proxy`)
  - optional `via` route list: `via: [nodeId1, nodeId2, ...]`
    - Interpreting `via`:
      - The UI should render this as segmented edges:
        - `from -> via[0] -> ... -> via[n] -> to`
      - These generated edges should be associated back to the original link id (for styling + visibility).

### Business Processes
- `businessProcesses[]` with:
  - `id`
  - `displayName` (string; if empty, omit from UI list and diagram labeling)
  - `steps[]` representing a path through the system:
    - Each step references either:
      - a node id: `{ node: "appA" }`
      - or a link id: `{ link: "link1" }`
    - Steps should allow a simple ordered flow; the UI should draw an overlay path.
  - `usesLinks: [linkId...]` (optional convenience)
  - `color` (optional, used to style the process overlay)
  - `hiddenByDefault: bool` (optional)

### Example YAML
Include a sample `diagram.yaml` that demonstrates:
- 2 platforms
- 3 applications
- 4 modules
- at least 4 links
- at least one link routed `via` a proxy application
- 2 business processes, each with distinct overlay styling

---

## Backend requirements (ASP.NET Core)
### Endpoints
Implement endpoints like:

- `GET /api/diagram/config`
  - returns the parsed YAML as JSON (or a computed graph model)
- `GET /api/diagram/graph`
  - returns Cytoscape-ready elements:
    - `nodes: [...]`
    - `edges: [...]`
  - Include enough metadata in `data` for styling:
    - node: `id`, `type`, `displayName`, `parent` (if using compound nodes), etc.
    - edge: `id`, `source`, `target`, `displayName`, `kind`, `originalLinkId`, `processIds` etc.
- `GET /api/diagram/processes`
  - returns list of business processes for UI toggles

### Config load
- Load YAML from `Config/diagram.yaml`.
- Provide basic error handling:
  - if YAML invalid, return 500 with a helpful error message.
- (Optional) Add file watcher / reload for dev:
  - If YAML changes, next request reloads it (simple caching allowed but must not require restart in development).

---

## Frontend requirements (Cytoscape.js)
### Rendering
- Fetch `/api/diagram/graph` and render with Cytoscape.
- Use Cytoscape styles:
  - platforms/apps/modules look different (shape/color/border)
  - edges styled by `kind` and/or if routed via proxy
  - labels: show only when `displayName` is non-empty
- Use a layout algorithm that produces a readable initial layout (e.g., `breadthfirst` or `cose`).

### Containment visualization
Implement containment in one of these acceptable ways:
- **Compound nodes**: platform node is parent of applications, application parent of modules (preferred if feasible).
- Or visually group with background rectangles (less preferred).
If using compound nodes, ensure nodes include `data.parent` accordingly.

### Business process overlay + toggling
- Provide a sidebar UI listing business processes (checkboxes).
- User can show/hide any process.
- When a process is enabled:
  - highlight nodes/edges in that process path (e.g., thicker edges, glow, color).
  - Optionally add directed “process edges” overlay. Either approach is acceptable:
    1) Re-style existing edges referenced by process, and highlight nodes; OR
    2) Add separate overlay edges in Cytoscape with a class like `.process-edge.process-{id}`.
- When hidden:
  - remove overlay styling/elements for that process.
- Ensure processes with empty `displayName` do not show in the sidebar list.

---

## Business rules (important)
1. **Display label hiding rule**:
   - If `displayName` is `""` or null:
     - node label is not shown
     - edge label is not shown
2. **Link routing rule**:
   - If a link has `via: [...]`, render it as multiple edges.
   - These edges should keep the original link’s `displayName` behavior (either show label on the first segment only, or show no labels; document the choice).
3. **Validation**
   - Verify referenced IDs exist:
     - containment IDs exist
     - links `from/to/via` exist
     - business process steps reference valid node/link IDs
   - On validation failure, return an error that names the missing IDs.

---

## Deliverables
- Working ASP.NET Core app that can be run with `dotnet run` and navigated to a local URL.
- Sample YAML file in repo.
- Cytoscape included locally.
- Clear README with:
  - how to run
  - YAML schema overview
  - how to add new platforms/apps/modules/links/processes
  - known limitations

---

## Acceptance criteria checklist
- [ ] No CDN usage; Cytoscape served locally.
- [ ] YAML loaded by server, not hard-coded in JS.
- [ ] Platforms/apps/modules rendered with distinct styles.
- [ ] Containment is visually represented.
- [ ] Links can connect any of platform/app/module to any other.
- [ ] Routed links via proxy are rendered as segmented edges.
- [ ] Labels hidden when `displayName` is empty.
- [ ] Business processes listed in UI and can be toggled to show/hide.
- [ ] At least 2 sample business processes, one hidden by default.
- [ ] README documents schema and how to extend.

---

## Notes / optional enhancements (do if easy)
- Add search/filter box to focus on a node by id/displayName.
- Add legend for node/edge kinds.
- Add `?process=` query parameter to preselect processes.
