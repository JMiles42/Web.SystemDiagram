async function loadGraph(){
  const res = await fetch('/api/diagram/graph');
  if(!res.ok){
    const err = await res.json();
    document.getElementById('cy').innerText = 'Error loading graph: ' + (err.error||res.statusText);
    return;
  }
  const data = await res.json();

  const elements = [];
  // ensure node data.icon becomes a usable URL (if present)
  data.nodes.forEach(n => {
    if (n.data && n.data.icon) {
      const iconVal = String(n.data.icon);
      if (!iconVal.startsWith('/') && !iconVal.startsWith('http')) {
        n.data.icon = '/icons/' + iconVal.toLowerCase() + '.svg';
      }
    }
    elements.push(n);
  });
  data.edges.forEach(e => elements.push(e));

  const cy = cytoscape({
    container: document.getElementById('cy'),
    elements: elements,
    style: [
      // node labels: smaller, above the node, wrapped if long
      { selector: 'node', style: { 'label': 'data(displayName)', 'text-valign': 'top', 'text-halign': 'center', 'text-margin-y': -10, 'font-size': 10, 'text-wrap': 'wrap', 'text-max-width': 80, 'background-color': '#fff', 'border-color':'#333', 'border-width':1 } },
      { selector: 'node[type="platform"]', style: { 'shape':'roundrectangle', 'background-color':'#f7f3d9' } },
      { selector: 'node[type="application"]', style: { 'shape':'roundrectangle', 'background-color':'#dfefff' } },
      { selector: 'node[type="module"]', style: { 'shape':'ellipse', 'background-color':'#e8f7e8' } },
      { selector: ':parent', style: { 'background-opacity': 0.05, 'padding': 10 } },

      // icon rule: use node.data.icon (URL) when present
      { selector: 'node[icon]', style: { 'background-image': 'data(icon)', 'background-fit':'contain', 'background-width':'60%', 'background-height':'60%', 'background-clip':'node' } },

      { selector: 'edge', style: { 'width':2, 'line-color':'#888', 'target-arrow-shape':'triangle', 'target-arrow-color':'#888', 'curve-style':'bezier', 'label':'data(displayName)', 'font-size':10 } },
      { selector: 'edge[kind="http"]', style: { 'line-style':'solid', 'line-color':'#2c3e50', 'target-arrow-color':'#2c3e50' } },
      { selector: 'edge[kind="proxy"]', style: { 'line-style':'dashed', 'line-color':'#9b59b6', 'target-arrow-color':'#9b59b6' } },
      { selector: 'edge[kind="event"]', style: { 'line-style':'dotted', 'line-color':'#27ae60', 'target-arrow-color':'#27ae60' } },

      // process highlighting (edges and nodes) — uses element data 'processColor'
      { selector: '.process-highlight', style: { 'line-color':'data(processColor)', 'target-arrow-color':'data(processColor)', 'width':4 } },
      { selector: '.process-node', style: { 'border-color':'data(processColor)', 'border-width':4 } },
    ],
    layout: { name: 'cose', idealEdgeLength:80, nodeOverlap:20 }
  });

  window._cy = cy;
}

async function loadProcesses(){
  const res = await fetch('/api/diagram/processes');
  const container = document.getElementById('processList');
  container.innerHTML = '';

  if(!res.ok){
    const text = await res.text().catch(()=>res.statusText);
    const err = document.createElement('div');
    err.className = 'process-error';
    err.innerText = 'Error loading processes: ' + (text || res.statusText || res.status);
    container.appendChild(err);
    return;
  }

  const payload = await res.json();

  function createProcessItem(p) {
    const div = document.createElement('div');
    div.className = 'process-item';
    const cb = document.createElement('input'); cb.type = 'checkbox'; cb.id = 'proc_'+p.id; cb.checked = !p.hiddenByDefault;
    // attach the process object to the checkbox for reliable access in handlers
    cb._proc = p;
    const label = document.createElement('label'); label.htmlFor = cb.id; label.innerText = p.displayName;
    const sw = document.createElement('span'); sw.className = 'legend-swatch'; sw.style.background = p.color || '#f39c12';
    div.appendChild(cb); div.appendChild(sw); div.appendChild(label);

    cb.addEventListener('change', (ev)=> {
      const target = ev.currentTarget;
      toggleProcess(target._proc, target.checked);
    });
    // apply default
    if(cb.checked) toggleProcess(p, true);
    return div;
  }

  function renderProcessList(parentEl, procs){
    procs.forEach(p => parentEl.appendChild(createProcessItem(p)));
  }

  let renderedAny = false;

  // groups (expected shape)
  if (Array.isArray(payload.groups) && payload.groups.length) {
    payload.groups.forEach(g => {
      const details = document.createElement('details');
      details.open = !g.hiddenByDefault;
      const summary = document.createElement('summary');
      summary.innerText = g.displayName || g.id || 'Group';
      details.appendChild(summary);

      const inner = document.createElement('div');
      inner.className = 'group-processes';
      if (Array.isArray(g.processes) && g.processes.length) {
        renderProcessList(inner, g.processes);
        renderedAny = true;
      } else {
        const em = document.createElement('div');
        em.className = 'empty-group';
        em.innerText = 'No processes in this group';
        inner.appendChild(em);
      }
      details.appendChild(inner);
      container.appendChild(details);
    });
  }

  // back-compat or stray ungrouped processes (server returns payload.processes)
  if (Array.isArray(payload.processes) && payload.processes.length) {
    const header = document.createElement('h4');
    header.innerText = 'Other Processes';
    container.appendChild(header);
    const list = document.createElement('div');
    renderProcessList(list, payload.processes);
    container.appendChild(list);
    renderedAny = true;
  }

  // Back-compat: server might return a flat array directly
  if (!renderedAny && Array.isArray(payload) && payload.length) {
    const header = document.createElement('h4');
    header.innerText = 'Processes';
    container.appendChild(header);
    renderProcessList(container, payload);
    renderedAny = true;
  }

  if (!renderedAny) {
    const msg = document.createElement('div');
    msg.className = 'no-processes';
    msg.innerHTML = 'No business process groups found. Define <code>businessProcessGroups</code> in <code>Config/diagram.yaml</code>.';
    container.appendChild(msg);
  }
}

function highlightProcess(p){
  const cy = window._cy;
  if(!cy) return;
  const color = p.color || '#e74c3c';

  // highlight nodes and edges mentioned in steps
  if(p.steps){
    p.steps.forEach(s => {
      if(s.node){
        const n = cy.getElementById(s.node);
        if(n && n.length){
          n.addClass('process-node');
          n.data('processColor', color);
        } else {
          console.warn(`Process ${p.id} references missing node '${s.node}'`);
        }
      }
      if(s.link){
        // find edges with originalLinkId == link
        const edges = cy.edges().filter(e => e.data('originalLinkId') === s.link);
        if(edges.length === 0){
          console.warn(`Process ${p.id} references link '${s.link}' but no matching edges found`);
        }
        edges.forEach(e => { e.addClass('process-highlight'); e.data('processColor', color); });
      }
    });
  }
}

function unhighlightProcess(p){
  const cy = window._cy; if(!cy) return;
  if(p.steps){
    p.steps.forEach(s => {
      if(s.node){
        const n = cy.getElementById(s.node);
        if(n && n.length){
          n.removeClass('process-node');
          n.data('processColor', null);
        }
      }
      if(s.link){
        const edges = cy.edges().filter(e => e.data('originalLinkId') === s.link);
        edges.forEach(e => { e.removeClass('process-highlight'); e.data('processColor', null); });
      }
    });
  }
}

function toggleProcess(p, on){
  if(on) highlightProcess(p); else unhighlightProcess(p);
}

async function init(){
  await loadGraph();
  await loadProcesses();
}

init();
