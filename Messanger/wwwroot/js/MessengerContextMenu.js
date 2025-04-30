
(function () {
    const { currentUserAva } = window.chatConfig;
    const origin = window.location.origin;

    
    function makeLinks(text) {
        return text.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank">$1</a>');
    }

    
    const menu = document.createElement('div');
    menu.id = 'contextMenu';
    Object.assign(menu.style, {
        position: 'absolute', display: 'none', zIndex: 10000,
        background: '#fff', border: '1px solid #ddd', borderRadius: '4px',
        boxShadow: '0 2px 6px rgba(0,0,0,0.15)'
    });
    menu.innerHTML = `
        <ul style="list-style:none;margin:0;padding:0;">
            <li id="cm-edit" style="padding:8px;cursor:pointer;">✏️ Редагувати</li>
            <li id="cm-delete" style="padding:8px;cursor:pointer;">🗑️ Видалити</li>
        </ul>`;
    document.body.appendChild(menu);

    let targetWrapper = null;

   
    document.body.addEventListener('contextmenu', e => {
        const wrapper = e.target.closest('.message-wrapper');
        if (!wrapper) return;
        e.preventDefault();
        targetWrapper = wrapper;
        const canEdit = wrapper.dataset.hasText === 'true' && wrapper.querySelector('img.avatar')?.src.includes(currentUserAva);
        menu.querySelector('#cm-edit').style.display = canEdit ? 'block' : 'none';
        menu.style.top = `${e.pageY}px`;
        menu.style.left = `${e.pageX}px`;
        menu.style.display = 'block';
    });
    document.addEventListener('click', () => menu.style.display = 'none');

    
    menu.querySelector('#cm-delete').addEventListener('click', async () => {
        if (!targetWrapper) return;
        const id = targetWrapper.dataset.messageId;
        try {
            const res = await fetch(`${origin}${window.apiRoutes.deleteMessage}?id=${id}`, { method: 'POST' });
            if (res.ok) targetWrapper.remove();
            else console.error('Delete failed', await res.text());
        } catch (err) {
            console.error('Delete error', err);
        }
    });

    
    menu.querySelector('#cm-edit').addEventListener('click', () => {
        if (!targetWrapper) return;

        const messageDiv = targetWrapper.querySelector('.message');
        const timeDiv = messageDiv.querySelector('.message-time');
        const timeText = timeDiv ? timeDiv.innerText : '';
        if (timeDiv) timeDiv.remove();

        const oldText = messageDiv.innerText.trim();

        
        const inputEl = document.createElement('input');
        inputEl.id = 'inlineEdit';
        inputEl.className = 'form-control';
        inputEl.type = 'text';
        inputEl.value = oldText;

        const newTimeDiv = document.createElement('div');
        newTimeDiv.className = 'message-time';
        newTimeDiv.textContent = timeText;

        messageDiv.innerHTML = '';
        messageDiv.appendChild(inputEl);
        messageDiv.appendChild(newTimeDiv);

        inputEl.focus();
        inputEl.setSelectionRange(oldText.length, oldText.length);

        function onKey(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                save();
            } else if (e.key === 'Escape') {
                cancel();
            }
        }

        inputEl.addEventListener('keydown', onKey);
        inputEl.addEventListener('blur', cancel);

        async function save() {
            const newText = inputEl.value.trim();
            const id = targetWrapper.dataset.messageId;
            try {
                const res = await fetch(
                    `${origin}${window.apiRoutes.editMessage}?id=${id}&newText=${encodeURIComponent(newText)}`,
                    { method: 'POST' }
                );
                if (!res.ok) throw new Error(await res.text());
            } catch (err) {
                console.error('Edit save error', err);
            }
            finish(newText);
        }

        function cancel() {
            finish(oldText);
        }

        function finish(text) {
            inputEl.removeEventListener('keydown', onKey);
            inputEl.removeEventListener('blur', cancel);
            messageDiv.innerHTML = `${makeLinks(text)}<div class="message-time">${timeText}</div>`;
        }
    });
})();
