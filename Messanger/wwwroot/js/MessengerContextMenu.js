document.addEventListener('DOMContentLoaded', () => {
    const menu = document.createElement('div');
    menu.id = 'contextMenu';
    Object.assign(menu.style, {
        position: 'absolute',
        display: 'none',
        zIndex: 10000,
        background: '#fff',
        border: '1px solid #ddd',
        borderRadius: '4px',
        boxShadow: '0 2px 6px rgba(0,0,0,0.15)'
    });
    menu.innerHTML = `
    <ul style="list-style:none;margin:0;padding:0;">
      <li id="cm-edit" style="padding:8px;cursor:pointer;">✏️ Редагувати</li>
      <li id="cm-delete" style="padding:8px;cursor:pointer;">🗑️ Видалити</li>
    </ul>`;
    document.body.appendChild(menu);
    const messageForm = document.getElementById('messageForm');
    const messageInput = document.getElementById('messageInput');
    const editIdInput = document.getElementById('editMessageId');
    const sendMessageBtn = document.getElementById('sendMessageBtn');
    let targetWrapper = null;
    document.body.addEventListener('contextmenu', e => {
        const wrapper = e.target.closest('.message-wrapper');
        if (!wrapper) return;
        e.preventDefault();
        targetWrapper = wrapper;
        const hasText = wrapper.dataset.hasText === 'true';
        menu.querySelector('#cm-edit').style.display = hasText ? 'block' : 'none';
        menu.style.top = `${e.pageY}px`;
        menu.style.left = `${e.pageX}px`;
        menu.style.display = 'block';
    });
    document.addEventListener('click', () => menu.style.display = 'none');
    menu.querySelector('#cm-delete').addEventListener('click', () => {
        if (!targetWrapper) return;
        const id = targetWrapper.dataset.messageId;
        const url = `${window.apiRoutes.deleteMessage}?id=${id}`;
        fetch(url, { method: 'POST' })
            .then(r => {
                if (r.ok) targetWrapper.remove();
                else console.error('Delete failed', r.status, r.statusText);
            })
            .catch(err => console.error('Delete fetch error', err));
    });
    menu.querySelector('#cm-edit').addEventListener('click', () => {
        if (!targetWrapper) return;
        const id = targetWrapper.dataset.messageId;
        const textEl = targetWrapper.querySelector('.message-text');
        const oldText = textEl ? textEl.textContent.trim() : '';
        editIdInput.value = id;
        messageInput.value = oldText;
        sendMessageBtn.textContent = '💾';
        messageInput.focus();
    });
    messageForm.addEventListener('submit', async e => {
        e.preventDefault();
        const text = messageInput.value.trim();
        const editId = editIdInput.value;

        if (editId) {
            const url = `${window.apiRoutes.editMessage}?id=${editId}&newText=${encodeURIComponent(text)}`;
            try {
                const res = await fetch(url, { method: 'POST' });
                if (res.ok) {
                    const wrapper = document.querySelector(`.message-wrapper[data-message-id="${editId}"]`);
                    const textEl = wrapper.querySelector('.message-text');
                    if (textEl) textEl.innerHTML = makeLinks(text);
                } else {
                    console.error('Edit failed', await res.text());
                }
            } catch (err) {
                console.error('Edit fetch error', err);
            }
            editIdInput.value = '';
            sendMessageBtn.textContent = '▶️';
            messageInput.value = '';
        } else {
            messageForm.submit();
        }
    });
    function makeLinks(text) {
        return text.replace(/(https?:\/\/[^"]+)/g, '<a href="$1" target="_blank">$1</a>');
    }
});
