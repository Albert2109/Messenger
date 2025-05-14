
(function () {
    document.addEventListener('DOMContentLoaded', () => {
        const cfg = window.chatConfig || {};
        const currentUserId = String(cfg.currentUserId || '');
        const currentGroupId = cfg.currentGroupId;
        if (!currentUserId) return;

        const isGroup = currentGroupId != null;
        const deleteUrlBase = isGroup
            ? '/Group/DeleteMessage'
            : '/MessangerHome/DeleteMessage';
        const editUrlBase = isGroup
            ? '/Group/EditMessage'
            : '/MessangerHome/EditMessage';

        const menu = document.createElement('div');
        menu.id = 'context-menu';
        Object.assign(menu.style, {
            position: 'absolute', display: 'none', zIndex: 10000,
            background: '#fff', border: '1px solid #ddd', borderRadius: '4px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.15)', padding: '0'
        });
        menu.innerHTML = `
      <ul style="list-style:none;margin:0;padding:0;">
        <li id="cm-edit"   style="padding:8px;cursor:pointer;">✏️ Редагувати</li>
        <li id="cm-delete" style="padding:8px;cursor:pointer;">🗑️ Видалити</li>
      </ul>`;
        document.body.append(menu);

        let targetWrapper = null;

        function showMenu(x, y, canEdit) {
            menu.style.top = `${y}px`;
            menu.style.left = `${x}px`;
            menu.style.display = 'block';
            const editLi = menu.querySelector('#cm-edit');
            const deleteLi = menu.querySelector('#cm-delete');
            if (editLi) editLi.style.display = canEdit ? 'block' : 'none';
            if (deleteLi) deleteLi.style.display = 'block';
        }

        function hideMenu() {
            menu.style.display = 'none';
            targetWrapper = null;
        }

        document.body.addEventListener('contextmenu', e => {
            const wrapper = e.target.closest('.message-wrapper');
            if (!wrapper) return;
            e.preventDefault();
            targetWrapper = wrapper;

            const msgUserId = wrapper.querySelector('img.avatar')?.dataset.userId;
            showMenu(e.pageX, e.pageY, msgUserId === currentUserId);
        });

        document.addEventListener('click', e => { if (!menu.contains(e.target)) hideMenu(); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape') hideMenu(); });

       
        menu.querySelector('#cm-delete')?.addEventListener('click', async () => {
            if (!targetWrapper) return hideMenu();
            const id = targetWrapper.dataset.messageId;
            hideMenu();
            try {
                let url = isGroup
                    ? `${deleteUrlBase}/${encodeURIComponent(id)}`
                    : `${deleteUrlBase}?id=${encodeURIComponent(id)}`;
                const res = await fetch(url, { method: 'POST' });
                if (!res.ok) throw new Error(await res.text());
                targetWrapper.remove();
            } catch (err) {
                console.error('Delete error:', err);
            }
        });

        
        menu.querySelector('#cm-edit')?.addEventListener('click', () => {
            if (!targetWrapper) return hideMenu();
            const wrapper = targetWrapper;
            hideMenu();

            const msgDiv = wrapper.querySelector('.message');
            if (!msgDiv) return;  
            const timeEl = msgDiv.querySelector('.message-time');
            const timeTxt = timeEl?.textContent || '';
            if (timeEl) timeEl.remove();

            const original = msgDiv.textContent.trim();
            msgDiv.innerHTML = '';

            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control';
            input.value = original;
            msgDiv.appendChild(input);
            input.focus();
            input.setSelectionRange(original.length, original.length);

            function cancelEdit() {
                msgDiv.textContent = original;
                msgDiv.insertAdjacentHTML('beforeend', `<div class="message-time">${timeTxt}</div>`);
            }

            async function saveEdit() {
                const newText = input.value.trim();
                const id = wrapper.dataset.messageId;
                const url = isGroup
                    ? `${editUrlBase}/${encodeURIComponent(id)}`
                    : editUrlBase;            

               
                const params = new URLSearchParams();
                if (!isGroup) params.append('id', id);
                params.append('newText', newText);

                console.log('POST', url, params.toString());
                const res = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: params.toString()
                });
                console.log('Status:', res.status, 'ok:', res.ok);
                if (!res.ok) {
                    console.error('Edit error:', await res.text());
                    cancelEdit();
                    return;
                }

                
                msgDiv.innerHTML =
                    `${newText}<div class="message-time">${timeTxt}</div>`;
            }

            input.addEventListener('keydown', e => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    saveEdit();
                } else if (e.key === 'Escape') {
                    cancelEdit();
                }
            });
            input.addEventListener('blur', cancelEdit);
        });
    });

})();
