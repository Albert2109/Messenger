/* contextMenu.js v1.8.0 */
(function () {
    document.addEventListener('DOMContentLoaded', () => {
        const currentUserId = String(window.chatConfig?.currentUserId || '');
        if (!currentUserId) return;
        console.log('Loaded contextMenu.js v1.8.0');

        const deleteUrl = '/MessangerHome/DeleteMessage';
        const editUrl = '/MessangerHome/EditMessage';

        // Створюємо контекстне меню
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
            menu.querySelector('#cm-edit').style.display = canEdit ? 'block' : 'none';
            menu.querySelector('#cm-delete').style.display = 'block';
        }

        function hideMenu() {
            menu.style.display = 'none';
            targetWrapper = null;
        }

        // Обробка правого кліку на повідомленні
        document.body.addEventListener('contextmenu', e => {
            const wrapper = e.target.closest('.message-wrapper');
            if (!wrapper) return;
            e.preventDefault();
            targetWrapper = wrapper;

            const msgUserId = wrapper.querySelector('img.avatar')?.dataset.userId;
            const canEdit = msgUserId === currentUserId;
            showMenu(e.pageX, e.pageY, canEdit);
        });

        // Ховаємо меню при кліку поза ним або натисанні Esc
        document.addEventListener('click', e => {
            if (!menu.contains(e.target)) hideMenu();
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') hideMenu();
        });

        // Видалення повідомлення
        menu.querySelector('#cm-delete').addEventListener('click', async () => {
            if (!targetWrapper) return;
            const wrapper = targetWrapper;
            const id = wrapper.dataset.messageId;
            hideMenu();
            try {
                const res = await fetch(`${deleteUrl}?id=${encodeURIComponent(id)}`, { method: 'POST' });
                if (!res.ok) throw new Error(await res.text());
                wrapper.remove();
            } catch (err) {
                console.error('Delete error:', err);
            }
        });

        // Редагування повідомлення
        menu.querySelector('#cm-edit').addEventListener('click', () => {
            if (!targetWrapper) return;
            const wrapper = targetWrapper;
            hideMenu();

            const msgDiv = wrapper.querySelector('.message');
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

            // Зберегти редагування
            async function saveEdit() {
                const newText = input.value.trim();
                const id = wrapper.dataset.messageId;
                try {
                    const res = await fetch(
                        `${editUrl}?id=${encodeURIComponent(id)}&newText=${encodeURIComponent(newText)}`,
                        { method: 'POST' }
                    );
                    if (!res.ok) throw new Error(await res.text());
                    msgDiv.innerHTML = newText + `<div class="message-time">${timeTxt}</div>`;
                } catch (err) {
                    console.error('Edit error:', err);
                    msgDiv.textContent = original;
                    msgDiv.insertAdjacentHTML('beforeend', `<div class="message-time">${timeTxt}</div>`);
                }
            }

            // Відмінити редагування
            function cancelEdit() {
                msgDiv.textContent = original;
                msgDiv.insertAdjacentHTML('beforeend', `<div class="message-time">${timeTxt}</div>`);
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
