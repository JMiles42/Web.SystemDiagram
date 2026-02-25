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
    ],
    layout: { name: 'cose', idealEdgeLength:80, nodeOverlap:20 }
  });

  window._cy = cy;
}

async function loadProcesses(){
  const res = await fetch('/api/diagram/processes');
  if(!res.ok) return;
  const procs = await res.json();
  const container = document.getElementById('processList');
  container.innerHTML = '';
  procs.forEach(p => {
    const div = document.createElement('div');
    div.className = 'process-item';
    const cb = document.createElement('input'); cb.type = 'checkbox'; cb.id = 'proc_'+p.id; cb.checked = !p.hiddenByDefault;
    const label = document.createElement('label'); label.htmlFor = cb.id; label.innerText = p.displayName;
    const sw = document.createElement('span'); sw.className = 'legend-swatch'; sw.style.background = p.color || '#f39c12';
    div.appendChild(cb); div.appendChild(sw); div.appendChild(label);
    container.appendChild(div);

    cb.addEventListener('change', ()=> toggleProcess(p, cb.checked));
    // apply default
    if(cb.checked) toggleProcess(p, true);
  });
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
        if(n) n.addClass('process-node'); n.data('processColor', color);
      }
      if(s.link){
        // find edges with originalLinkId == link
        const edges = cy.edges().filter(e => e.data('originalLinkId') === s.link);
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
        if(n) n.removeClass('process-node'); n.data('processColor', null);
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
