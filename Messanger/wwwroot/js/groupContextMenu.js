console.log('⚙️ groupContextMenu.js v2.3 loaded');

(function () {

    async function post(url, data) {
        const fm = new FormData();
        if (data) for (const [k, v] of Object.entries(data)) fm.append(k, v);
        const res = await fetch(url, { method: 'POST', body: fm });
        if (!res.ok) throw new Error(await res.text());
    }


    window.GroupApi = Object.assign({}, window.GroupApi ?? {}, {
        addMember: (gid, uid) => post(`/Group/${gid}/AddMember`, { userId: uid }),
        removeMember: (gid, uid) => post(`/Group/${gid}/RemoveMember`, { userId: uid }),
        rename: (gid, nm) => post(`/Group/${gid}/Rename`, { name: nm }),
        transferOwner: (gid, oid) => post(`/Group/${gid}/TransferOwner`, { newOwnerId: oid }),
        leave: (gid) => post(`/Group/${gid}/Leave`),
        delete: (gid) => post(`/Group/${gid}/Delete`),
        changeAvatar: (gid, file) => {
            const fm = new FormData();
            fm.append('avatar', file);
            return fetch(`/Group/${gid}/Avatar`, { method: 'POST', body: fm });
        }
    });


    if (window.__groupContextMenuInit) return;
    window.__groupContextMenuInit = true;

    document.addEventListener('DOMContentLoaded', () => {
        const cfg = window.chatConfig || {};
        if (!cfg.currentUserId) return;

        let menu = document.getElementById('group-context-menu');
        if (!menu) {
            menu = document.createElement('div');
            menu.id = 'group-context-menu';
            Object.assign(menu.style, {
                position: 'absolute',
                display: 'none',
                zIndex: 10000,
                background: '#fff',
                border: '1px solid #ddd',
                borderRadius: '4px',
                boxShadow: '0 2px 6px rgba(0,0,0,0.15)',
                padding: '0'
            });
            menu.innerHTML = `
        <ul style="list-style:none;margin:0;padding:8px">
          <li id="gm-info"   style="padding:4px;cursor:pointer">ℹ️ Інфо про групу</li>
          <li id="gm-rename" style="padding:4px;cursor:pointer">✏️ Перейменувати</li>
          <li id="gm-avatar" style="padding:4px;cursor:pointer">🖼️ Змінити аватар</li>
          <li id="gm-manage" style="padding:4px;cursor:pointer">👥 Керування учасниками</li>
          <li id="gm-leave"  style="padding:4px;cursor:pointer">🚪 Покинути групу</li>
          <li id="gm-delete" style="padding:4px;cursor:pointer;color:red">🗑️ Видалити групу</li>
        </ul>`;
            document.body.appendChild(menu);
        }

        let targetLi = null; 

        document.body.addEventListener('contextmenu', e => {
            const li = e.target.closest('.group-list .list-group-item');
            if (!li) return;

            e.preventDefault();
            targetLi = li;

            const role = li.dataset.role || 'Member';
            const isAdmin = role === 'Admin' || role === 'Owner';

   
            menu.querySelector('#gm-rename').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-avatar').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-manage').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-leave').style.display = role !== 'Owner' ? 'block' : 'none';
            menu.querySelector('#gm-delete').style.display = role === 'Owner' ? 'block' : 'none';


            menu.style.top = `${e.pageY}px`;
            menu.style.left = `${e.pageX}px`;
            menu.style.display = 'block';
        });

        document.addEventListener('click', e => { if (!menu.contains(e.target)) menu.style.display = 'none'; });
        document.addEventListener('keydown', e => { if (e.key === 'Escape') menu.style.display = 'none'; });

        const gidOf = () => targetLi?.dataset.groupId;

        menu.querySelector('#gm-info').onclick = async () => {
            menu.style.display = 'none';
            const gid = gidOf(); if (!gid) return;

            const gmTitle = document.getElementById('gm-modal-title');
            const gmList = document.getElementById('gm-member-list');
            if (!gmTitle || !gmList) return console.warn('Missing modal elements');

            gmTitle.textContent = targetLi.querySelector('span')?.textContent.trim() ?? '';
            try {
                const res = await fetch(`/Group/${gid}/Members`);
                const mems = await res.json();
                gmList.innerHTML = mems.map(m => `
          <li class="list-group-item d-flex justify-content-between align-items-center">
            <div>
              <img src="${m.avatar}" class="avatar me-2" style="width:32px;height:32px"/>
              <a href="#" class="gm-member-link" data-chat-id="${m.id}">${m.login}</a>
              <small class="text-muted">(${m.role})</small>
            </div>
            ${(m.role !== 'Owner' && cfg.currentUserId != m.id)
                        ? `<button class="btn btn-sm btn-outline-danger" data-remove="${m.id}">✖️</button>`
                        : ''}
          </li>`).join('');
                bootstrap.Modal.getOrCreateInstance('#groupManageModal').show();
            } catch (err) {
                console.error(err);
                gmList.innerHTML = '<li class="list-group-item text-danger">Не вдалося завантажити учасників</li>';
            }
        };

        menu.querySelector('#gm-rename').onclick = async () => {
            menu.style.display = 'none';
            const gid = gidOf(); if (!gid) return;
            const nameEl = targetLi.querySelector('span');
            const oldName = nameEl ? nameEl.textContent : '';
            const newName = prompt('Нова назва групи:', oldName);
            if (!newName || newName === oldName) return;

            try {
                await window.GroupApi.rename(gid, newName);
                if (nameEl) nameEl.textContent = newName;
                const modalTitle = document.getElementById('gm-modal-title');
                if (modalTitle) modalTitle.textContent = newName;
            } catch (err) {
                console.error('Rename error:', err);
                alert('Не вдалося перейменувати групу: ' + err.message);
            }
        };

        menu.querySelector('#gm-avatar').onclick = () => {
            menu.style.display = 'none';
            const gid = gidOf(); if (!gid) return;

            const inp = document.createElement('input');
            inp.type = 'file'; inp.accept = 'image/*';
            inp.onchange = async () => {
                const file = inp.files?.[0];
                if (!file) return;
                try {
                    await window.GroupApi.changeAvatar(gid, file);
                    targetLi.querySelector('img.avatar').src = URL.createObjectURL(file);
                } catch (err) {
                    alert(err);
                }
            };
            inp.click();
        };

        menu.querySelector('#gm-manage').onclick = () => menu.querySelector('#gm-info').click();

        menu.querySelector('#gm-leave').onclick = () => {
            const gid = gidOf(); if (!gid) return;
            if (!confirm('Покинути групу?')) return;
            window.GroupApi.leave(gid)
                .then(() => location.reload())
                .catch(err => alert(err));
        };

        menu.querySelector('#gm-delete').onclick = () => {
            const gid = gidOf(); if (!gid) return;
            if (!confirm('Дійсно видалити групу назавжди?')) return;
            window.GroupApi.delete(gid)
                .then(() => location.reload())
                .catch(err => alert(err));
        };

      
        const memberList = document.getElementById('gm-member-list');
        if (memberList) {

            memberList.addEventListener('click', e => {
                const a = e.target.closest('a.gm-member-link');
                if (!a) return;
                e.preventDefault();
                window.location.href = `/MessangerHome?chatId=${a.dataset.chatId}`;
            });

            memberList.addEventListener('click', e => {
                const btn = e.target.closest('button[data-remove]');
                if (!btn) return;
                const gid = gidOf(); if (!gid) return;
                window.GroupApi.removeMember(gid, btn.dataset.remove)
                    .then(() => btn.closest('li').remove())
                    .catch(err => alert(err));
            });
        }
        const searchBtn = document.getElementById('gm-search-btn');
        const searchInp = document.getElementById('gm-search-input');
        const searchRes = document.getElementById('gm-search-results');
        if (searchBtn && searchInp && searchRes) {
            searchBtn.onclick = async () => {
                const q = searchInp.value.trim();
                if (q.length < 2) return;
                const res = await fetch(`/Account/Search?q=${encodeURIComponent(q)}`);
                const users = await res.json();
                searchRes.innerHTML = users.map(u => `
          <li class="list-group-item d-flex justify-content-between align-items-center">
            <div><img src="${u.ava}" class="avatar me-2" style="width:32px;height:32px"/>${u.login}</div>
            <button class="btn btn-sm btn-outline-primary" data-add="${u.id}">➕</button>
          </li>`).join('');
            };

            searchRes.addEventListener('click', e => {
                const btn = e.target.closest('button[data-add]');
                if (!btn) return;
                const gid = gidOf(); if (!gid) return;
                window.GroupApi.addMember(gid, btn.dataset.add)
                    .then(() => btn.closest('li').remove())
                    .catch(err => alert(err));
            });
        }
    });
})();