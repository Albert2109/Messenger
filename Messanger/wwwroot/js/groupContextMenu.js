
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        const cfg = window.chatConfig || {};
        if (!cfg.currentUserId) return;

       
        const menu = document.createElement('div');
        menu.id = 'group-context-menu';
        Object.assign(menu.style, {
            position: 'absolute', display: 'none', zIndex: 10000,
            background: '#fff', border: '1px solid #ddd',
            borderRadius: '4px', boxShadow: '0 2px 6px rgba(0,0,0,0.15)',
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
      </ul>
    `;
        document.body.appendChild(menu);

        let targetLi = null;

        document.body.addEventListener('contextmenu', e => {
            const li = e.target.closest('.group-list .list-group-item');
            if (!li) return;
            e.preventDefault();
            targetLi = li;

            const role = li.dataset.role || 'Member';
            const isAdmin = (role === 'Admin' || role === 'Owner');

            
            menu.querySelector('#gm-info').style.display = 'block';
            menu.querySelector('#gm-rename').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-avatar').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-manage').style.display = isAdmin ? 'block' : 'none';
            menu.querySelector('#gm-leave').style.display = (role !== 'Owner') ? 'block' : 'none';
            menu.querySelector('#gm-delete').style.display = (role === 'Owner') ? 'block' : 'none';

            menu.style.top = `${e.pageY}px`;
            menu.style.left = `${e.pageX}px`;
            menu.style.display = 'block';
        });

        document.addEventListener('click', e => {
            if (!menu.contains(e.target)) menu.style.display = 'none';
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') menu.style.display = 'none';
        });


       
        const manageModal = new bootstrap.Modal('#groupManageModal');
        const gmTitle = document.getElementById('gm-modal-title');
        const gmList = document.getElementById('gm-member-list');
        const searchInp = document.getElementById('gm-search-input');
        const searchBtn = document.getElementById('gm-search-btn');
        const searchRes = document.getElementById('gm-search-results');
        const leaveBtn = document.getElementById('gm-leave-btn');


        
        menu.querySelector('#gm-info').onclick = async () => {
            menu.style.display = 'none';
            const gid = targetLi.dataset.groupId;
            gmTitle.textContent = targetLi.querySelector('span').textContent.trim();
            try {
                const res = await fetch(`/Group/${gid}/Members`);
                const mems = await res.json();
                gmList.innerHTML = mems.map(m => `
          <li class="list-group-item d-flex justify-content-between align-items-center">
            <div>
              <img src="${m.avatar}" class="avatar me-2" style="width:32px;height:32px"/>
              <a href="#" class="gm-member-link text-decoration-none" data-chat-id="${m.id}">
                ${m.login}
              </a>
              <small class="text-muted">(${m.role})</small>
            </div>
            ${(m.role !== 'Owner' && cfg.currentUserId != m.id)
                        ? `<button class="btn btn-sm btn-outline-danger" data-remove="${m.id}">✖️</button>`
                        : ''}
          </li>`).join('');
            } catch {
                gmList.innerHTML = `<li class="list-group-item text-danger">
                              Не вдалося завантажити учасників
                            </li>`;
            }
            manageModal.show();
        };

        menu.querySelector('#gm-rename').onclick = () => {
            menu.style.display = 'none';
            const gid = targetLi.dataset.groupId;
            const newName = prompt('Нова назва групи:',
                targetLi.querySelector('span').textContent);
            if (!newName) return;
            window.GroupApi.rename(gid, newName)
                .then(() => {
                    targetLi.querySelector('span').textContent = newName;
                    if (manageModal._isShown) gmTitle.textContent = newName;
                })
                .catch(err => alert(err));
        };

        menu.querySelector('#gm-avatar').onclick = () => {
            menu.style.display = 'none';
            const gid = targetLi.dataset.groupId;
            const inp = document.createElement('input');
            inp.type = 'file';
            inp.accept = 'image/*';
            inp.onchange = () => {
                const file = inp.files[0];
                window.GroupApi.changeAvatar(gid, file)
                    .then(() => {
                        const img = targetLi.querySelector('img.avatar');
                        img.src = URL.createObjectURL(file);
                    })
                    .catch(err => alert(err));
            };
            inp.click();
        };

        menu.querySelector('#gm-manage').onclick = () => {
            menu.querySelector('#gm-info').click();
        };

        leaveBtn.onclick = () => {
            const gid = targetLi.dataset.groupId;
            if (!confirm('Покинути групу?')) return;
            window.GroupApi.leave(gid)
                .then(() => location.reload())
                .catch(err => alert(err));
        };

        menu.querySelector('#gm-delete').onclick = () => {
            menu.style.display = 'none';
            const gid = targetLi.dataset.groupId;
            if (!confirm('Дійсно видалити групу назавжди?')) return;
            window.GroupApi.delete(gid)
                .then(() => location.reload())
                .catch(err => alert(err));
        };


       
        gmList.addEventListener('click', e => {
            const a = e.target.closest('a.gm-member-link');
            if (!a) return;
            e.preventDefault();
            const uid = a.dataset.chatId;
            window.location.href = `/MessangerHome?chatId=${uid}`;
        });

        
        searchBtn.onclick = async () => {
            const q = searchInp.value.trim();
            if (q.length < 2) return;
            const res = await fetch(`/Account/Search?q=${encodeURIComponent(q)}`);
            const users = await res.json();
            searchRes.innerHTML = users.map(u => `
        <li class="list-group-item d-flex justify-content-between align-items-center">
          <div>
            <img src="${u.ava}" class="avatar me-2" style="width:32px;height:32px"/>
            ${u.login}
          </div>
          <button class="btn btn-sm btn-outline-primary" data-add="${u.id}">➕</button>
        </li>`).join('');
        };

        searchRes.addEventListener('click', e => {
            const btn = e.target.closest('button[data-add]');
            if (!btn) return;
            const gid = targetLi.dataset.groupId;
            const uid = btn.dataset.add;
            window.GroupApi.addMember(gid, uid)
                .then(() => {
                    const li = btn.closest('li');
                    const html = `
            <li class="list-group-item d-flex justify-content-between align-items-center">
              ${li.querySelector('div').outerHTML}
              <button class="btn btn-sm btn-outline-danger" data-remove="${uid}">✖️</button>
            </li>`;
                    gmList.insertAdjacentHTML('beforeend', html);
                    li.remove();
                })
                .catch(err => alert(err));
        });

        gmList.addEventListener('click', e => {
            const btn = e.target.closest('button[data-remove]');
            if (!btn) return;
            const gid = targetLi.dataset.groupId;
            const uid = btn.dataset.remove;
            window.GroupApi.removeMember(gid, uid)
                .then(() => btn.closest('li').remove())
                .catch(err => alert(err));
        });
    });
})();
