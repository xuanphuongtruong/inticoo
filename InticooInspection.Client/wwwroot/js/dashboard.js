    (function () {
        let BASE = '';
        let allItems = [], activeFilter = 'all';

        // Get ApiBaseUrl from Blazor config (appsettings.json)
        function dbGetBaseUrl() {
            const el = document.getElementById('blazor-api-base');
            if (el) {
                const url = (el.getAttribute('data-url') || '').trim().replace(/\/$/, '');
                if (url) return url;
            }
            // Fallback: localStorage if config not available
            return localStorage.getItem('ic_base') || '';
        }

        // Auto-init after DOM is ready
        function dbInit() {
            BASE = dbGetBaseUrl();
            if (BASE) {
                document.getElementById('cfg-banner').style.display = 'none';
                dbLoadAll();
            } else {
                // Show banner only if no config
                document.getElementById('cfg-banner').style.display = 'flex';
            }
        }

        window.dbSaveUrl = function () {
            const v = (document.getElementById('api-input').value || '').trim().replace(/\/$/, '');
            if (!v) { alert('Please enter URL'); return; }
            BASE = v;
            localStorage.setItem('ic_base', v);
            document.getElementById('cfg-banner').style.display = 'none';
            dbLoadAll();
        };

        // Wait for Blazor to finish rendering
        setTimeout(dbInit, 100);

        window.dbLoadAll = async function () {
            if (!BASE) return;
            dbSpin(true);
            document.getElementById('last-upd').textContent = 'Loading…';

            const [iR, cR, vR, pR, uR] = await Promise.allSettled([
                dbGet('/api/inspections?pageSize=500'),
                dbGet('/api/customers?pageSize=1'),
                dbGet('/api/vendors?pageSize=1'),
                dbGet('/api/products?pageSize=1'),
                dbGet('/api/users?pageSize=200'),
            ]);

            if (iR.status === 'fulfilled') {
                allItems = iR.value.items || [];
                const total  = iR.value.total || allItems.length;
                const onCnt  = allItems.filter(x => dbNorm(x.status) === 'ongoing').length;
                const cmpCnt = allItems.filter(x => dbNorm(x.status) === 'completed').length;
                const penCnt = allItems.filter(x => ['pending','new'].includes(dbNorm(x.status))).length;
                const canCnt = allItems.filter(x => ['cancelled','cancel'].includes(dbNorm(x.status))).length;
                const thisMo = dbThisMonth(allItems).length;
                dbSetKpi('k1', total, 'd1', total + ' jobs');
                dbSetKpi('k2', onCnt, 'd2', onCnt + ' active');
                dbRenderTable('all');
                dbRenderDonut(cmpCnt, onCnt, penCnt, canCnt, total);
                dbRenderBarChart(allItems);
                dbRenderActivity(allItems);
                dbRenderTypes(allItems);
                dbRenderInspectors(allItems);
                document.getElementById('mstats').innerHTML =
                    `<div class="db-ms"><div class="db-msv" style="color:var(--db-accent)">${cmpCnt}</div><div class="db-msl">Completed</div></div>
                     <div class="db-ms"><div class="db-msv" style="color:var(--db-blue)">${thisMo}</div><div class="db-msl">This month</div></div>
                     <div class="db-ms"><div class="db-msv" style="color:var(--db-amber)">${penCnt}</div><div class="db-msl">Pending</div></div>`;
            } else {
                dbErrTable('Failed to load from ' + BASE + '<br><small>' + iR.reason.message + '</small>');
            }
            if (cR.status === 'fulfilled') dbSetKpi('k3', cR.value.total || 0, 'd3', (cR.value.total||0) + ' clients');
            if (vR.status === 'fulfilled') { const t = vR.value.total||0; dbSetKpi('k4', t, 'd4', t+' vendors'); document.getElementById('ms1').textContent = t; }
            if (pR.status === 'fulfilled') dbSetKpi('k5', pR.value.total||0, 'd5', (pR.value.total||0)+' SKUs');
            if (uR.status === 'fulfilled') { const t = uR.value.total||(uR.value.items||[]).length; document.getElementById('ms2').textContent = t; }

            const now = new Date();
            document.getElementById('last-upd').textContent = 'Updated ' + now.toLocaleTimeString('en-US',{hour:'2-digit',minute:'2-digit'});
            dbSpin(false);
        };

        async function dbGet(path) {
            const r = await fetch(BASE + path, { headers: { Accept: 'application/json' } });
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        }

        function dbNorm(s) {
            if (!s) return 'new';
            const l = s.toLowerCase().replace(/[\s-]/g,'');
            if (l==='ongoing'||l==='inprogress') return 'ongoing';
            if (l==='completed') return 'completed';
            if (l==='cancelled'||l==='cancel') return 'cancelled';
            return 'pending';
        }
        const statusVI = { ongoing:'Ongoing', completed:'Completed', pending:'Pending', cancelled:'Cancelled', new:'New' };

        function dbRenderTable(filter) {
            activeFilter = filter;
            let rows = allItems;
            if (filter !== 'all') rows = rows.filter(r => dbNorm(r.status) === filter);
            const show = rows.slice(0, 10);
            document.getElementById('tbl-sub').textContent = show.length + ' / ' + rows.length + ' records';
            if (!show.length) { document.getElementById('tbody').innerHTML = `<tr><td colspan="5"><div class="db-errmsg"><div class="db-ei">📭</div>No data</div></td></tr>`; return; }
            document.getElementById('tbody').innerHTML = show.map(r => {
                const n = dbNorm(r.status);
                const d = (r.inspectionDate||r.createdAt) ? new Date(r.inspectionDate||r.createdAt).toLocaleDateString('en-US',{day:'2-digit',month:'2-digit'}) : '—';
                return `<tr>
                    <td><div class="tnum">${dbEsc(r.jobNumber||'#'+r.id)}</div></td>
                    <td><div class="tmain">${dbEsc(r.customerName||'—')}</div><div class="tsub">${dbEsc(r.productName||r.productCategory||'—')}</div></td>
                    <td><div style="font-size:12px">${dbEsc(r.inspectorName||r.inspectorId||'—')}</div></td>
                    <td><span class="badge b-${n}">${statusVI[n]||n}</span></td>
                    <td><div style="font-size:11px;color:var(--db-muted)">${d}</div></td>
                </tr>`;
            }).join('');
        }
        function dbErrTable(msg) { document.getElementById('tbody').innerHTML = `<tr><td colspan="5"><div class="db-errmsg"><div class="db-ei">⚠️</div>${msg}</div></td></tr>`; }

        // Filter tabs
        document.getElementById('ftabs').addEventListener('click', e => {
            const b = e.target.closest('.db-ftab'); if (!b) return;
            document.querySelectorAll('.db-ftab').forEach(x => x.classList.remove('on'));
            b.classList.add('on');
            dbRenderTable(b.dataset.f);
        });

        function dbRenderDonut(cmp, on, pen, can, total) {
            const C = 2 * Math.PI * 38;
            const segs = [
                { label:'Completed', count:cmp, color:'var(--db-accent)' },
                { label:'Ongoing',  count:on,  color:'var(--db-blue)'   },
                { label:'Pending',         count:pen, color:'var(--db-amber)'  },
                { label:'Cancelled',         count:can, color:'var(--db-red)'    },
            ].filter(s => s.count > 0);
            let cum = 0;
            const circles = (total > 0 ? segs : []).map(s => {
                const pct = s.count/total, dash = C*pct, gap = C-dash, rotate = -90+cum*360;
                cum += pct;
                return `<circle cx="50" cy="50" r="38" fill="none" stroke="${s.color}" stroke-width="11" stroke-dasharray="${dash.toFixed(2)} ${gap.toFixed(2)}" transform="rotate(${rotate.toFixed(2)} 50 50)"/>`;
            }).join('');
            const legend = segs.map(s => `<div class="li"><span class="ldot" style="background:${s.color}"></span><div style="flex:1"><div class="lname">${s.label}</div><div class="lval">${s.count}</div></div><span class="lpct">${total>0?Math.round(s.count/total*100):0}%</span></div>`).join('');
            document.getElementById('donut-wrap').innerHTML = `
                <svg width="100" height="100" viewBox="0 0 100 100" style="flex-shrink:0">
                    <circle cx="50" cy="50" r="38" fill="none" stroke="var(--db-border)" stroke-width="11"/>
                    ${circles||'<circle cx="50" cy="50" r="38" fill="none" stroke="var(--db-border)" stroke-width="11"/>'}
                    <text x="50" y="46" text-anchor="middle" fill="var(--db-text)" font-size="13" font-weight="800" font-family="Syne,sans-serif">${total}</text>
                    <text x="50" y="57" text-anchor="middle" fill="var(--db-muted)" font-size="7.5">total</text>
                </svg>
                <div class="dleg">${legend||'<div style="color:var(--db-muted);font-size:12px">No data available</div>'}</div>`;
        }

        function dbRenderBarChart(items) {
            const now = new Date();
            const mos = Array.from({length:12}, (_,i) => { const d = new Date(now.getFullYear(), now.getMonth()-11+i, 1); return {y:d.getFullYear(),m:d.getMonth(),lbl:d.toLocaleDateString('en-US',{month:'short'})}; });
            const cmpArr = mos.map(mo => items.filter(x => { const d=x.completedAt?new Date(x.completedAt):null; return d&&d.getFullYear()===mo.y&&d.getMonth()===mo.m&&dbNorm(x.status)==='completed'; }).length);
            const newArr = mos.map(mo => items.filter(x => { const d=x.createdAt?new Date(x.createdAt):null; return d&&d.getFullYear()===mo.y&&d.getMonth()===mo.m; }).length);
            const maxV = Math.max(...cmpArr,...newArr,1);
            document.getElementById('bar-chart').innerHTML = mos.map((mo,i) => {
                const hC = ((cmpArr[i]/maxV)*100).toFixed(1), hN = ((newArr[i]/maxV)*100).toFixed(1);
                return `<div class="bcol"><div class="bwrap"><div class="bar" style="background:var(--db-accent);opacity:.8;height:2px" data-h="${hC}" data-v="${cmpArr[i]}"></div><div class="bar" style="background:var(--db-blue);opacity:.7;height:2px" data-h="${hN}" data-v="${newArr[i]}"></div></div></div>`;
            }).join('');
            document.getElementById('bar-lbls').innerHTML = mos.map(mo => `<span style="font-size:8.5px;color:var(--db-muted);flex:1;text-align:center">${mo.lbl}</span>`).join('');
            requestAnimationFrame(() => setTimeout(() => { document.querySelectorAll('.bar').forEach(b => { b.style.height = b.dataset.h + '%'; }); }, 50));
        }

        function dbRenderActivity(items) {
            const sorted = [...items].sort((a,b)=>new Date(b.createdAt||0)-new Date(a.createdAt||0)).slice(0,7);
            const ico = { completed:['✅','rgba(0,212,170,.12)'], ongoing:['🔄','rgba(59,130,246,.12)'], pending:['⏳','rgba(245,158,11,.12)'], cancelled:['❌','rgba(239,68,68,.12)'], new:['📋','rgba(139,92,246,.12)'] };
            document.getElementById('afeed').innerHTML = sorted.length
                ? sorted.map(r => { const n=dbNorm(r.status),[ic,bg]=ico[n]||['📋','rgba(100,116,139,.12)']; return `<div class="aitem"><div class="aico" style="background:${bg}">${ic}</div><div class="atxt"><strong>${dbEsc(r.jobNumber||'#'+r.id)}</strong> — ${dbEsc(r.customerName||'N/A')} <span class="badge b-${n}" style="font-size:9px;padding:1px 6px;margin-left:4px">${statusVI[n]||n}</span><div class="atime">${dbEsc(r.inspectorName||r.inspectorId||'—')} · ${dbAgo(r.createdAt)}</div></div></div>`; }).join('')
                : '<div class="db-errmsg"><div class="db-ei">📭</div>No activity yet</div>';
        }

        function dbRenderTypes(items) {
            const total=items.length||1, cnt={DPI:0,PPT:0,PST:0};
            items.forEach(r => { const t=(r.inspectionType||'DPI').toUpperCase(); if(cnt[t]!==undefined) cnt[t]++; });
            const cols={DPI:'var(--db-accent)',PPT:'var(--db-blue)',PST:'var(--db-amber)'};
            const lbls={DPI:'DPI — During Production',PPT:'PPT — Pre-Production',PST:'PST — Pre-Shipment'};
            document.getElementById('tbreak').innerHTML = ['DPI','PPT','PST'].map(t => {
                const p=Math.round(cnt[t]/total*100);
                return `<div class="tbar-row"><div class="tbar-top"><span class="tbar-lbl">${lbls[t]}</span><span class="tbar-cnt" style="color:${cols[t]}">${cnt[t]}</span></div><div class="tbar-bg"><div class="tbar-fill" style="width:0%;background:${cols[t]}" data-w="${p}"></div></div><div class="tbar-pct">${p}% of total</div></div>`;
            }).join('');
            requestAnimationFrame(() => setTimeout(() => { document.querySelectorAll('.tbar-fill').forEach(f => { f.style.width = f.dataset.w + '%'; }); }, 80));
        }

        function dbRenderInspectors(items) {
            const map={};
            items.forEach(r => { const k=r.inspectorName||r.inspectorId; if(!k) return; if(!map[k]) map[k]={name:k,id:r.inspectorId||'',n:0}; map[k].n++; });
            const list=Object.values(map).sort((a,b)=>b.n-a.n).slice(0,6);
            const maxN=Math.max(...list.map(x=>x.n),1);
            const pal=['#00d4aa','#3b82f6','#f59e0b','#ef4444','#8b5cf6','#ec4899'];
            document.getElementById('ilist').innerHTML = list.length
                ? list.map((p,i) => { const c=pal[i%pal.length],ini=p.name.split(' ').map(w=>w[0]).join('').toUpperCase().slice(0,2); return `<div class="irow"><div class="iav" style="background:${c}">${ini}</div><div style="flex:1;min-width:0"><div class="iname">${dbEsc(p.name)}</div><div class="iid">${p.id}</div></div><div class="ibg"><div class="ifill" style="width:${Math.round(p.n/maxN*100)}%;background:${c}"></div></div><div class="inum" style="color:${c}">${p.n}</div></div>`; }).join('')
                : '<div class="db-errmsg"><div class="db-ei">👤</div>No inspectors yet</div>';
        }

        function dbThisMonth(items) { const n=new Date(); return items.filter(r => { const d=r.createdAt?new Date(r.createdAt):null; return d&&d.getMonth()===n.getMonth()&&d.getFullYear()===n.getFullYear(); }); }
        function dbAgo(ts) { if(!ts) return '—'; const m=Math.floor((Date.now()-new Date(ts))/60000); if(m<1) return 'Just now'; if(m<60) return m+' min ago'; const h=Math.floor(m/60); if(h<24) return h+' hr ago'; const d=Math.floor(h/24); if(d<30) return d+' days ago'; return new Date(ts).toLocaleDateString('en-US'); }
        function dbEsc(s) { return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
        function dbSetKpi(id, val, did, delta) { let c=0; const el=document.getElementById(id); const t=setInterval(()=>{ c=Math.min(c+Math.max(1,Math.floor(val/40)),val); el.textContent=c.toLocaleString('en-US'); if(c>=val) clearInterval(t); },20); document.getElementById(did).textContent=delta; }
        function dbSpin(on) { document.getElementById('ref-btn').style.animation = on ? 'dbSpin 1s linear infinite' : ''; }

        // Auto-refresh every 2 minutes
        setInterval(() => { if (BASE) dbLoadAll(); }, 120000);
    })();
